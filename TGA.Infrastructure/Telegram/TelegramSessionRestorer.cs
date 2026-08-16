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

        var loginTask = Task.Run(() => client.LoginUserIfNeeded());
        var completed = await Task.WhenAny(loginTask, Task.Delay(LoginTimeout));

        string? displayName;

        if (completed == loginTask)
        {
            var user = await loginTask;
            displayName = GetDisplayName(user);
        }
        else
        {
            logger.LogInformation(
                "Логин для аккаунта {Id} ещё не завершён, продолжаю использовать сохранённую сессию", accountId);

            var account = await accountStorage.GetByIdAsync(accountId);
            displayName = account?.DisplayName;
        }

        await accountStorage.SetActiveAsync(accountId);
        await accountStorage.UpdateSessionDataAsync(accountId, clientFactory.GetCurrentSessionBytes());

        return new SessionRestoreResult(true, displayName);
    }

    private static string GetDisplayName(User user) =>
        !string.IsNullOrEmpty(user.username)
            ? $"@{user.username}"
            : $"{user.first_name} {user.last_name}".Trim();
}