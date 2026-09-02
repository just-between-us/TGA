using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TL;
using WTelegram;

namespace TGA.Infrastructure.Telegram;

public class TelegramContactSyncService(
    TelegramPeerDirectory peerDirectory,
    IAccountStorageService accountStorage,
    IContactStorageService contactStorage,
    ITelegramAvatarService avatarService,
    ILogger<TelegramContactSyncService> logger)
{
    public async Task<int> SyncAsync(Client client)
    {
        var active = await accountStorage.GetActiveAccountAsync()
            ?? throw new InvalidOperationException("Нет активного аккаунта");
        peerDirectory.CurrentUserId = active.TelegramUserId;

        var result = await client.Contacts_GetContacts();

        if (result is not Contacts_Contacts contacts)
        {
            logger.LogInformation("Контакт-лист Telegram не изменился с прошлого запроса");
            return 0;
        }

        var count = 0;

        foreach (var contact in contacts.contacts)
        {
            if (contact.user_id == peerDirectory.CurrentUserId) continue;
            if (!contacts.users.TryGetValue(contact.user_id, out var user)) continue;
            if (user.IsBot) continue;

            peerDirectory.RememberUser(contact.user_id, user);
            if (user.access_hash != 0)
                peerDirectory.RememberPeer(contact.user_id, new InputPeerUser(user.ID, user.access_hash));

            await contactStorage.UpsertAsync(active.Id, contact.user_id, TelegramPeerDirectory.DisplayName(user));
            await avatarService.RefreshContactAsync(active.Id, user.ID, user.access_hash);
            count++;
        }

        logger.LogInformation("Импортировано {Count} контактов из адресной книги Telegram", count);
        return count;
    }
}