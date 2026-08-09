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
    ILogger<TelegramMessageService> logger) : ITelegramMessageService
{
    private long _myUserId;
    private readonly Dictionary<long, User> _userCache = new();

    public event Action<MessageDto>? OnNewMessageReceived;
    public bool IsMonitoring { get; private set; }

    public async Task LoadRecentPersonalMessagesAsync(int messagesPerDialog)
    {
        var active = await accountStorage.GetActiveAccountAsync()
            ?? throw new InvalidOperationException("Нет активного аккаунта");
        _myUserId = active.TelegramUserId;

        connectionStatus.SetUpdating(); 
        
        var client = RequireClient();
        var dialogs = await client.Messages_GetDialogs();

        foreach (var dialog in dialogs.Dialogs)
        {
            if (dialog.Peer is not PeerUser peerUser) continue;

            try
            {
                var inputPeer = new InputPeerUser(peerUser.user_id, 0);
                var history = await client.Messages_GetHistory(inputPeer, limit: messagesPerDialog);
                var chats = await client.Messages_GetAllChats();
                foreach (var messageBase in history.Messages)
                {
                    if (messageBase is not Message message) continue;
                    if (string.IsNullOrWhiteSpace(message.message)) continue;

                    var dto = await ConvertToDto(client, message);
                    if (dto is null) continue;

                    await storage.AddMessageAsync(dto, active.Id);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Ошибка загрузки диалога {PeerId}", dialog.Peer.ID);
            }
        }
        connectionStatus.SetConnected(active.DisplayName);
    }
    
    public async Task<int> SyncDialogsAsync()
    {
        var active = await accountStorage.GetActiveAccountAsync()
                     ?? throw new InvalidOperationException("Нет активного аккаунта");
        _myUserId = active.TelegramUserId;

        connectionStatus.SetUpdating();

        var client = RequireClient();
        var dialogs = await client.Messages_GetDialogs();

        var count = 0;

        foreach (var dialog in dialogs.Dialogs)
        {
            if (dialog.Peer is not PeerUser peerUser) continue;
            if (peerUser.user_id == _myUserId) continue;

            if (dialogs.UserOrChat(dialog.Peer) is not User user) continue;

            _userCache[peerUser.user_id] = user;
            var name = DisplayName(user);

            await contactStorage.UpsertAsync(active.Id, peerUser.user_id, name);
            count++;
        }

        connectionStatus.SetConnected(active.DisplayName);
        logger.LogInformation("Синхронизировано {Count} диалогов", count);
        return count;
    }
    
    public async Task<List<MessageDto>> LoadChatHistoryAsync(long peerUserId, int limit)
    {
        var active = await accountStorage.GetActiveAccountAsync()
                     ?? throw new InvalidOperationException("Нет активного аккаунта");
        _myUserId = active.TelegramUserId;

        var client = RequireClient();
        var inputPeer = new InputPeerUser(peerUserId, 0);

        try
        {
            var history = await client.Messages_GetHistory(inputPeer, limit: limit);

            foreach (var messageBase in history.Messages)
            {
                if (messageBase is not Message message) continue;
                if (string.IsNullOrWhiteSpace(message.message)) continue;

                var dto = await ConvertToDto(client, message);
                if (dto is null) continue;

                await storage.AddMessageAsync(dto, active.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ошибка загрузки истории для {PeerId}", peerUserId);
        }

        return await storage.GetMessagesByPeerAsync(active.Id, peerUserId);
    }
    
    public async Task<int> SyncContactsAsync()
    {
        logger.LogInformation("Начинаю синхронизацию контактов");
        
        var active = await accountStorage.GetActiveAccountAsync()
                     ?? throw new InvalidOperationException("Нет активного аккаунта");

        var client = RequireClient();
        var dialogs = await client.Messages_GetDialogs();
        var count = 0;

        foreach (var dialog in dialogs.Dialogs)
        {
            if (dialog.Peer is not PeerUser peerUser) continue;
            if (peerUser.user_id == active.TelegramUserId) continue; 

            var name = await GetContactNameAsync(client, peerUser.user_id);
            await contactStorage.UpsertAsync(active.Id, peerUser.user_id, name);
            count++;
        }

        return count;
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
        var peer = new InputPeerUser(peerUserId, 0);

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
        if (message.peer_id is not PeerUser) return;

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

        return new MessageDto(
            message.id, contactName, message.message ?? string.Empty,
            message.Date.ToLocalTime(), isOutgoing, peerUser.user_id);
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
            logger.LogWarning(ex, "Не удалось разрешить имя для {PeerId}", peerUserId);
            return null;
        }
    }
    private static string DisplayName(User user)
    {
        var full = $"{user.first_name} {user.last_name}".Trim();
        if (!string.IsNullOrEmpty(full)) return full;
        return !string.IsNullOrEmpty(user.username) ? $"@{user.username}" : $"User {user.ID}";
    }

    private Client RequireClient() => clientFactory.GetCurrent();
}