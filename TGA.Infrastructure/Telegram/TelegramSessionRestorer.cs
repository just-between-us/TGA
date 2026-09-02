using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TL;
using WTelegram;

namespace TGA.Infrastructure.Telegram;

public record SessionRestoreResult(bool Success, string? DisplayName);

public class TelegramSessionRestorer(
    TelegramClientFactory clientFactory,
    IAccountStorageService accountStorage,
    ITelegramAvatarService avatarService,
    ILogger<TelegramSessionRestorer> logger)
{
    public async Task<SessionRestoreResult> TryRestoreAsync(
        int accountId, byte[] sessionData, Func<string, string?> interactiveFallback)
    {
        var account = await accountStorage.GetByIdAsync(accountId);

        var callback = BuildRestoreCallback(account?.PhoneNumber, interactiveFallback);

        var client = clientFactory.CreateNew(callback, sessionData);
        var user = await client.LoginUserIfNeeded();
        var displayName = GetDisplayName(user);

        await accountStorage.SetActiveAsync(accountId);
        await accountStorage.UpdateSessionDataAsync(accountId, clientFactory.GetCurrentSessionBytes());
        await avatarService.RefreshAsync(accountId, user.ID, user.access_hash);

        return new SessionRestoreResult(true, displayName);
    }

    private static Func<string, string?> BuildRestoreCallback(string? knownPhone, Func<string, string?> fallback) =>
        what => what switch
        {
            "phone_number" when !string.IsNullOrWhiteSpace(knownPhone) => knownPhone,
            _ => fallback(what)
        };

    private static string GetDisplayName(User user) =>
        !string.IsNullOrEmpty(user.username)
            ? $"@{user.username}"
            : $"{user.first_name} {user.last_name}".Trim();
}