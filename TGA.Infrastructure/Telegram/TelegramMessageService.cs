using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;
using TL;
using WTelegram;

namespace TGA.Infrastructure.Telegram;

public class TelegramMessageService(
    TelegramClientFactory clientFactory,
    IMessageStorageService storage,
    IAccountStorageService accountStorage,
    IContactStorageService contactStorage,
    IConnectionStatusService connectionStatus, 
    IChatStorageService chatStorage,  
    ILogger<TelegramMessageService> logger) : ITelegramMessageService
{
    private long _myUserId;
    private readonly Dictionary<long, User> _userCache = new();
    private readonly Dictionary<long, InputPeer> _peerCache = new();
    private readonly Dictionary<long, Message> _messagePeerCache = new();
    private readonly Dictionary<long, long> _dialogTopMessageCache = new();

    public event Action<MessageDto>? OnNewMessageReceived;
    public bool IsMonitoring { get; private set; }
    
    public async Task<int> SyncDialogsAsync()
    {
        var active = await accountStorage.GetActiveAccountAsync()
                     ?? throw new InvalidOperationException("Нет активного аккаунта");
        _myUserId = active.TelegramUserId;

        connectionStatus.SetUpdating();

        var client = RequireClient();
        var allDialogs = await client.Messages_GetAllDialogs();

        var messagesById = allDialogs.Messages
            .OfType<Message>()
            .ToDictionary(m => (long)m.id);

        var count = 0;

        foreach (var dialog in allDialogs.dialogs)  
        {
            
            if (dialog.Peer is not PeerUser peerUser) continue;
            if (peerUser.user_id == _myUserId) continue;

            var userOrChat = allDialogs.UserOrChat(dialog.Peer) as User;
            if (userOrChat == null || userOrChat.IsBot) continue;

            string? topText = null;
            DateTime? topTime = null;
            bool? topIsOutgoing = null;
            
            if (dialog.TopMessage != 0 && messagesById.TryGetValue(dialog.TopMessage, out var topMessage))
            {
                topText = BuildDisplayText(topMessage);
                topTime = topMessage.Date.ToLocalTime();
                topIsOutgoing = topMessage.flags.HasFlag(Message.Flags.out_);
            }
            
            await chatStorage.UpsertDialogAsync(
                active.Id, peerUser.user_id, dialog.TopMessage, topText, topTime, topIsOutgoing);

            if (allDialogs.UserOrChat(dialog.Peer) is User user)
            {
                _userCache[peerUser.user_id] = user;
                _peerCache[peerUser.user_id] = new InputPeerUser(user.ID, user.access_hash);
                _dialogTopMessageCache[peerUser.user_id] = dialog.TopMessage;
                await contactStorage.UpsertAsync(active.Id, peerUser.user_id, DisplayName(user));
            }

            count++;
        }

        connectionStatus.SetConnected(active.DisplayName);
        logger.LogInformation("Синхронизировано {Count} диалогов", count);
        return count;
    }
    
    public async Task<List<MessageDto>> LoadChatHistoryAsync(long peerUserId, int limit, long offsetMessageId = 0)
    {
        var active = await accountStorage.GetActiveAccountAsync()
                     ?? throw new InvalidOperationException("Нет активного аккаунта");
        _myUserId = active.TelegramUserId;

        var client = RequireClient();
        var inputPeer = await ResolveInputPeerAsync(client, peerUserId);
        var (peerUserIdForLog, accessHashForLog, peerKind) = DescribePeer(inputPeer);

        logger.LogInformation(
            "Загрузка истории: peerUserId={PeerUserId}, peerKind={PeerKind}, userId={UserId}, accessHash={AccessHash}, limit={Limit}, offset={Offset}",
            peerUserId,
            peerKind,
            peerUserIdForLog,
            accessHashForLog,
            limit,
            offsetMessageId);

        try
        {
            var history = await client.Messages_GetHistory(
                inputPeer, limit: limit, offset_id: (int)offsetMessageId);

            foreach (var messageBase in history.Messages)
            {
                if (messageBase is not Message message) continue;

                var dto = await ConvertToDto(client, message);
                if (dto is null) continue;

                await storage.AddMessageAsync(dto, active.Id);
            }

            var chatId = await chatStorage.GetOrCreateChatIdAsync(active.Id, peerUserId);
            await chatStorage.MarkHistoryLoadedAsync(chatId);
        }
        catch (Exception ex) when (IsPeerInvalid(ex))
        {
            var description = DescribePeer(inputPeer);
            logger.LogWarning(ex,
                "Telegram отклонил peer для {PeerId}. peerKind={PeerKind}, userId={UserId}, accessHash={AccessHash}; возвращаем локальные сообщения",
                peerUserId,
                description.PeerKind,
                description.UserId,
                description.AccessHash);
            return await storage.GetMessagesByPeerAsync(active.Id, peerUserId);
        }
        catch (Exception ex)
        {
            var description = DescribePeer(inputPeer);
            logger.LogWarning(ex,
                "Ошибка загрузки истории для {PeerId}. peerKind={PeerKind}, userId={UserId}, accessHash={AccessHash}",
                peerUserId,
                description.PeerKind,
                description.UserId,
                description.AccessHash);
            return await storage.GetMessagesByPeerAsync(active.Id, peerUserId);
        }

        return await storage.GetMessagesByPeerAsync(active.Id, peerUserId);
    }
    
    public void StartMonitoring()
    {
        if (IsMonitoring) return;
        var client = clientFactory.GetCurrent();  
        client.OnUpdates += HandleUpdates;
        IsMonitoring = true;
    }

    public void StopMonitoring()
    {
        if (!IsMonitoring) return;
        var client = clientFactory.GetCurrent();   
        client.OnUpdates -= HandleUpdates;
        IsMonitoring = false;
    }

    public async Task<MessageDto> SendMessageAsync(long peerUserId, string text)
    {
        var active = await accountStorage.GetActiveAccountAsync()
                     ?? throw new InvalidOperationException("Нет активного аккаунта");

        var client = RequireClient();
        var peer = await ResolveInputPeerAsync(client, peerUserId);

        var sentMessage = await client.SendMessageAsync(peer, text);

        var dto = new MessageDto(
            Id: sentMessage.id,
            ContactName:"Я",
            Text: sentMessage.message ?? text,
            Time: sentMessage.Date.ToLocalTime(),
            IsOutgoing: true,
            PeerUserId: peerUserId);

        await storage.AddMessageAsync(dto, active.Id);

        OnNewMessageReceived?.Invoke(dto);

        return dto;
    }

    private async Task HandleUpdates(UpdatesBase updates)
    {
        foreach (var update in updates.UpdateList)
        {
            if (update is UpdateNewMessage { message: Message message })
                await ProcessNewMessage(message);
        }
    }

    private async Task ProcessNewMessage(Message message)
    {
        if (message.peer_id is not PeerUser peerUser) return;

        _messagePeerCache[peerUser.user_id] = message;

        var active = await accountStorage.GetActiveAccountAsync();
        if (active is null) return;

        var client = RequireClient();
        var dto = await ConvertToDto(client, message);
        if (dto is null) return;

        await storage.AddMessageAsync(dto, active.Id);
        OnNewMessageReceived?.Invoke(dto);
    }

    private async Task<MessageDto?> ConvertToDto(Client client, Message message)
    {
        if (message.peer_id is not PeerUser peerUser) return null;

        var isOutgoing = message.flags.HasFlag(Message.Flags.out_);
        var userId = isOutgoing ? peerUser.user_id : message.Peer.ID;
        var contactName = await GetContactNameAsync(client, userId);
        var text = BuildDisplayText(message);

        return new MessageDto(
            message.id, contactName, text,
            message.Date.ToLocalTime(), isOutgoing, peerUser.user_id);
    }

    private static string BuildDisplayText(Message message)
    {
        if (!string.IsNullOrWhiteSpace(message.message))
            return message.message.Trim();

        if (message.media is null)
            return "[сообщение без текста]";

        var mediaTypeName = message.media.GetType().Name;

        if (mediaTypeName.Contains("Video", StringComparison.OrdinalIgnoreCase))
            return "[видео]";

        if (mediaTypeName.Contains("Voice", StringComparison.OrdinalIgnoreCase))
            return "[голосовое сообщение]";

        if (mediaTypeName.Contains("Audio", StringComparison.OrdinalIgnoreCase))
            return "[аудио]";

        return message.media switch
        {
            MessageMediaPhoto => "[фото]",
            MessageMediaDocument document when document.document is Document documentFile &&
                                              documentFile.mime_type?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true =>
                message.grouped_id != 0 ? "[альбом фото]" : "[фото]",
            MessageMediaDocument document when document.document is Document documentFile &&
                                              documentFile.mime_type?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true =>
                "[видео]",
            MessageMediaDocument => "[файл]",
            MessageMediaGeo or MessageMediaGeoLive => "[геолокация]",
            MessageMediaContact => "[контакт]",
            MessageMediaWebPage => "[веб-страница]",
            MessageMediaPoll => "[опрос]",
            _ => $"[{mediaTypeName}]"
        };
    }

    private async Task<string> GetContactNameAsync(Client client, long userId)
    {
        if (userId == _myUserId) return "Я";
        if (_userCache.TryGetValue(userId, out var cached)) return DisplayName(cached);

        try
        {
            var users = await client.Users_GetUsers([new InputUser(userId, 0)]);
            if (users.Length > 0 && users[0] is User user)
            {
                _userCache[userId] = user;
                var name = DisplayName(user);

                var active = await accountStorage.GetActiveAccountAsync();
                if (active is not null)
                    await contactStorage.UpsertAsync(active.Id, userId, name); 

                return name;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось получить пользователя {UserId}", userId);
        }

        return $"User {userId}";
    }
    
    public async Task<string?> ResolveUserDisplayNameAsync(long peerUserId)
    {
        var active = await accountStorage.GetActiveAccountAsync()
                     ?? throw new InvalidOperationException("Нет активного аккаунта");

        var client = RequireClient();
        try
        {
            var users = await client.Users_GetUsers([new InputUser(peerUserId, 0)]);
            if (users.Length == 0 || users[0] is not User user) 
                return null;

            var name = DisplayName(user);
            await contactStorage.RenameAsync(active.Id, peerUserId, name); 
            return name;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось применить имя для {PeerId}", peerUserId);
            return null;
        }
    }
    private static string DisplayName(User user)
    {
        var full = $"{user.first_name} {user.last_name}".Trim();
        if (!string.IsNullOrEmpty(full)) return full;
        return !string.IsNullOrEmpty(user.username) ? $"@{user.username}" : $"User {user.ID}";
    }

    private async Task<InputPeer> ResolveInputPeerAsync(Client client, long peerUserId)
    {
        if (_peerCache.TryGetValue(peerUserId, out var cachedPeer))
            return cachedPeer;

        if (_userCache.TryGetValue(peerUserId, out var cachedUser) && cachedUser.access_hash != 0)
        {
            var resolvedPeer = new InputPeerUser(cachedUser.ID, cachedUser.access_hash);
            _peerCache[peerUserId] = resolvedPeer;
            return resolvedPeer;
        }

        try
        {
            var users = await client.Users_GetUsers([new InputUser(peerUserId, 0)]);
            if (users.Length > 0 && users[0] is User user)
            {
                _userCache[peerUserId] = user;
                if (user.access_hash != 0)
                {
                    var resolvedPeer = new InputPeerUser(user.ID, user.access_hash);
                    _peerCache[peerUserId] = resolvedPeer;
                    return resolvedPeer;
                }

                logger.LogWarning(
                    "Пользователь {PeerUserId} найден, но access_hash равен 0. Пробуем fallback из последнего сообщения.",
                    peerUserId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось получить input peer для {PeerId}", peerUserId);
        }

        if (_messagePeerCache.TryGetValue(peerUserId, out var recentMessage) && recentMessage.peer_id is PeerUser)
        {
            var peerHash = 0L;
            if (_userCache.TryGetValue(peerUserId, out var cachedUserForPeer) && cachedUserForPeer.access_hash != 0)
                peerHash = cachedUserForPeer.access_hash;

            var messagePeer = new InputPeerUserFromMessage
            {
                peer = new InputPeerUser(peerUserId, peerHash),
                msg_id = (int)recentMessage.id,
                user_id = peerUserId
            };

            _peerCache[peerUserId] = messagePeer;
            logger.LogInformation(
                "Использую InputPeerUserFromMessage для {PeerUserId} из сообщения {MessageId} с accessHash={AccessHash}",
                peerUserId,
                recentMessage.id,
                peerHash);
            return messagePeer;
        }

        return new InputPeerUser(peerUserId, 0);
    }

    private async Task<InputPeer?> BuildFallbackPeerAsync(Client client, long peerUserId)
    {
        if (_dialogTopMessageCache.TryGetValue(peerUserId, out var topMessageId) && topMessageId != 0)
        {
            var user = _userCache.TryGetValue(peerUserId, out var cachedUser) ? cachedUser : null;
            if (user is not null && user.access_hash != 0)
            {
                var fallbackPeer = new InputPeerUserFromMessage
                {
                    peer = new InputPeerUser(user.ID, user.access_hash),
                    msg_id = (int)topMessageId,
                    user_id = peerUserId
                };

                logger.LogInformation(
                    "Собираю fallback-peer для {PeerUserId} из topMessage={TopMessageId}",
                    peerUserId,
                    topMessageId);
                return fallbackPeer;
            }

            try
            {
                var users = await client.Users_GetUsers([new InputUser(peerUserId, 0)]);
                if (users.Length > 0 && users[0] is User resolvedUser && resolvedUser.access_hash != 0)
                {
                    _userCache[peerUserId] = resolvedUser;
                    return new InputPeerUserFromMessage
                    {
                        peer = new InputPeerUser(resolvedUser.ID, resolvedUser.access_hash),
                        msg_id = (int)topMessageId,
                        user_id = peerUserId
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось построить fallback-peer для {PeerUserId}", peerUserId);
            }
        }

        return null;
    }

    private static bool IsPeerInvalid(Exception ex) => ex.Message.Contains("PEER_ID_INVALID", StringComparison.OrdinalIgnoreCase);

    private static (long UserId, long AccessHash, string PeerKind) DescribePeer(InputPeer peer)
    {
        return peer switch
        {
            InputPeerUser inputPeerUser => (inputPeerUser.user_id, inputPeerUser.access_hash, nameof(InputPeerUser)),
            InputPeerUserFromMessage inputPeerUserFromMessage => (inputPeerUserFromMessage.user_id, 0, nameof(InputPeerUserFromMessage)),
            _ => (0, 0, peer.GetType().Name)
        };
    }

    private Client RequireClient() => clientFactory.GetCurrent();
}