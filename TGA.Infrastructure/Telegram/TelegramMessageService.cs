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
    IChatStorageService chatStorage,
    TelegramPeerDirectory peerDirectory,
    TelegramPeerResolver peerResolver,
    TelegramContactResolver contactResolver,
    TelegramDialogSyncService dialogSyncService,
    TelegramContactSyncService contactSyncService,
    ILogger<TelegramMessageService> logger) : ITelegramMessageService
{
    public event Action<MessageDto>? OnNewMessageReceived;
    public bool IsMonitoring { get; private set; }

    public async Task<int> SyncDialogsAsync()
    {
        var client = RequireClient();
        return await dialogSyncService.SyncDialogsAsync(client);
    }

    public async Task<List<MessageDto>> LoadChatHistoryAsync(long peerUserId, int limit, long offsetMessageId = 0)
    {
        var active = await accountStorage.GetActiveAccountAsync()
            ?? throw new InvalidOperationException("Нет активного аккаунта");
        peerDirectory.CurrentUserId = active.TelegramUserId;

        var client = RequireClient();
        var inputPeer = await peerResolver.ResolveInputPeerAsync(client, peerUserId);
        var (peerUserIdForLog, accessHashForLog, peerKind) = TelegramPeerResolver.DescribePeer(inputPeer);

        logger.LogInformation(
            "Загрузка истории: peerUserId={PeerUserId}, peerKind={PeerKind}, userId={UserId}, accessHash={AccessHash}, limit={Limit}, offset={Offset}",
            peerUserId, peerKind, peerUserIdForLog, accessHashForLog, limit, offsetMessageId);

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
        catch (Exception ex) when (TelegramPeerResolver.IsPeerInvalid(ex))
        {
            var description = TelegramPeerResolver.DescribePeer(inputPeer);
            logger.LogWarning(ex,
                "Telegram отклонил peer для {PeerId}. peerKind={PeerKind}, userId={UserId}, accessHash={AccessHash}; возвращаем локальные сообщения",
                peerUserId, description.PeerKind, description.UserId, description.AccessHash);
            return await storage.GetMessagesByPeerAsync(active.Id, peerUserId);
        }
        catch (Exception ex)
        {
            var description = TelegramPeerResolver.DescribePeer(inputPeer);
            logger.LogWarning(ex,
                "Ошибка загрузки истории для {PeerId}. peerKind={PeerKind}, userId={UserId}, accessHash={AccessHash}",
                peerUserId, description.PeerKind, description.UserId, description.AccessHash);
            return await storage.GetMessagesByPeerAsync(active.Id, peerUserId);
        }

        return await storage.GetMessagesByPeerAsync(active.Id, peerUserId);
    }
    public async Task<int> SyncTelegramContactsAsync()
    {
        var client = RequireClient();
        return await contactSyncService.SyncAsync(client);
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
        var peer = await peerResolver.ResolveInputPeerAsync(client, peerUserId);

        var sentMessage = await client.SendMessageAsync(peer, text);

        var dto = new MessageDto(
            Id: sentMessage.id,
            ContactName: "Я",
            Text: sentMessage.message ?? text,
            Time: sentMessage.Date.ToLocalTime(),
            IsOutgoing: true,
            PeerUserId: peerUserId);

        await storage.AddMessageAsync(dto, active.Id);
        OnNewMessageReceived?.Invoke(dto);

        return dto;
    }

    public async Task<string?> ResolveUserDisplayNameAsync(long peerUserId)
    {
        var client = RequireClient();
        return await contactResolver.ResolveUserDisplayNameAsync(client, peerUserId);
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

        peerDirectory.RememberMessagePeer(peerUser.user_id, message);

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
        var contactName = await contactResolver.GetContactNameAsync(client, userId);
        var text = TelegramMessageTextFormatter.BuildDisplayText(message);

        return new MessageDto(
            message.id, contactName, text,
            message.Date.ToLocalTime(), isOutgoing, peerUser.user_id);
    }

    internal static async Task TryDeleteSentMessageAsync(Client client, int messageId)
    {
        try
        {
            await client.Messages_DeleteMessages([messageId], revoke: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Не удалось удалить тестовое сообщение: {ex.Message}");
        }
    }

    private Client RequireClient() => clientFactory.GetCurrent();
}