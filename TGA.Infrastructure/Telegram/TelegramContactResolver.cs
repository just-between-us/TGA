using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TL;
using WTelegram;

namespace TGA.Infrastructure.Telegram;

public class TelegramContactResolver(
    TelegramPeerDirectory peerDirectory,
    IContactStorageService contactStorage,
    IAccountStorageService accountStorage,
    ILogger<TelegramContactResolver> logger)
{
    public async Task<string> GetContactNameAsync(Client client, long userId)
    {
        if (userId == peerDirectory.CurrentUserId) return "Я";
        if (peerDirectory.TryGetUser(userId, out var cached)) return TelegramPeerDirectory.DisplayName(cached);

        try
        {
            var users = await client.Users_GetUsers([new InputUser(userId, 0)]);
            if (users.Length > 0 && users[0] is User user)
            {
                peerDirectory.RememberUser(userId, user);
                var name = TelegramPeerDirectory.DisplayName(user);

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

    public async Task<string?> ResolveUserDisplayNameAsync(Client client, long peerUserId)
    {
        var active = await accountStorage.GetActiveAccountAsync()
            ?? throw new InvalidOperationException("Нет активного аккаунта");

        try
        {
            var users = await client.Users_GetUsers([new InputUser(peerUserId, 0)]);
            if (users.Length == 0 || users[0] is not User user)
                return null;

            var name = TelegramPeerDirectory.DisplayName(user);
            await contactStorage.RenameAsync(active.Id, peerUserId, name);
            return name;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось применить имя для {PeerId}", peerUserId);
            return null;
        }
    }
}