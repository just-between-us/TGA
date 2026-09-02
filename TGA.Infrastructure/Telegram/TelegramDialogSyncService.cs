using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TL;
using WTelegram;

namespace TGA.Infrastructure.Telegram;

public class TelegramDialogSyncService(
    TelegramPeerDirectory peerDirectory,
    IAccountStorageService accountStorage,
    IContactStorageService contactStorage,
    IChatStorageService chatStorage,
    ITelegramAvatarService avatarService,
    IConnectionStatusService connectionStatus,
    ILogger<TelegramDialogSyncService> logger)
{
    public async Task<int> SyncDialogsAsync(Client client, bool loadAvatars = true)
{
    var active = await accountStorage.GetActiveAccountAsync()
        ?? throw new InvalidOperationException("Нет активного аккаунта");
    peerDirectory.CurrentUserId = active.TelegramUserId;

    connectionStatus.SetUpdating();

    var allDialogs = await client.Messages_GetAllDialogs();

    var messagesById = new Dictionary<long, Message>();
    foreach (var m in allDialogs.Messages.OfType<Message>())
    {
        messagesById[m.id] = m;
    }

    var count = 0;

    foreach (var dialog in allDialogs.dialogs)
    {
        if (dialog.Peer is not PeerUser peerUser) continue;
        if (peerUser.user_id == peerDirectory.CurrentUserId) continue;

        var userOrChat = allDialogs.UserOrChat(dialog.Peer) as User;
        if (userOrChat == null || userOrChat.IsBot) continue;

        string? topText = null;
        DateTime? topTime = null;
        bool? topIsOutgoing = null;

        if (dialog.TopMessage != 0 && messagesById.TryGetValue(dialog.TopMessage, out var topMessage))
        {
            topText = TelegramMessageTextFormatter.BuildDisplayText(topMessage);
            topTime = topMessage.Date.ToLocalTime();
            topIsOutgoing = topMessage.flags.HasFlag(Message.Flags.out_);
        }

        await chatStorage.UpsertDialogAsync(
            active.Id, peerUser.user_id, dialog.TopMessage, topText, topTime, topIsOutgoing);

        if (allDialogs.UserOrChat(dialog.Peer) is User user)
        {
            peerDirectory.RememberUser(peerUser.user_id, user);
            peerDirectory.RememberPeer(peerUser.user_id, new InputPeerUser(user.ID, user.access_hash));
            peerDirectory.RememberDialogTopMessage(peerUser.user_id, dialog.TopMessage);
            await contactStorage.UpsertAsync(active.Id, peerUser.user_id, TelegramPeerDirectory.DisplayName(user));
            if (loadAvatars)
            {
                await avatarService.RefreshContactAsync(active.Id, user.ID, user.access_hash);
            }
        }

        count++;
    }

    connectionStatus.SetConnected(active.DisplayName);
    logger.LogInformation("Синхронизировано {Count} диалогов", count);
    return count;
}
}