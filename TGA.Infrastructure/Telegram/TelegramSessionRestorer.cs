using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TL;
using WTelegram;

namespace TGA.Infrastructure.Telegram;

public record SessionRestoreResult(bool Success, string? DisplayName);

public class TelegramSessionRestorer(
    TelegramClientFactory clientFactory,
    IAccountStorageService accountStorage,
    ILogger<TelegramSessionRestorer> logger)
{
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(10);

    public async Task<SessionRestoreResult> TryRestoreAsync(
        int accountId, byte[] sessionData, Func<string, string?> configCallback)
    {
        var client = clientFactory.CreateNew(configCallback, sessionData);
        var user = await client.LoginUserIfNeeded();
        var displayName = GetDisplayName(user);

        await accountStorage.SetActiveAsync(accountId);
        await accountStorage.UpdateSessionDataAsync(accountId, clientFactory.GetCurrentSessionBytes());

        return new SessionRestoreResult(true, displayName);
    }

    private static string GetDisplayName(User user) =>
        !string.IsNullOrEmpty(user.username)
            ? $"@{user.username}"
            : $"{user.first_name} {user.last_name}".Trim();
}