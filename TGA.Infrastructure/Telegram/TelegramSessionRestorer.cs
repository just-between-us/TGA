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

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var loginTask = client.LoginUserIfNeeded();
        var completed = await Task.WhenAny(loginTask, Task.Delay(LoginTimeout));

        string? displayName;

        if (completed == loginTask)
        {
            var user = await loginTask;
            logger.LogInformation("LoginUserIfNeeded для {Id} завершился за {Elapsed}мс", accountId, stopwatch.ElapsedMilliseconds);
            displayName = GetDisplayName(user);
        }
        else
        {
            logger.LogInformation(
                "Логин для аккаунта {Id} ещё не завершён после {Elapsed}мс, продолжаю использовать сохранённую сессию",
                accountId, stopwatch.ElapsedMilliseconds);

            var account = await accountStorage.GetByIdAsync(accountId);
            displayName = account?.DisplayName;

            _ = loginTask.ContinueWith(t =>
            {
                logger.LogInformation(
                    "Отложенный логин для {Id} фактически завершился за {Elapsed}мс, IsFaulted={Faulted}",
                    accountId, stopwatch.ElapsedMilliseconds, t.IsFaulted);
                if (t.IsFaulted)
                    logger.LogWarning(t.Exception, "Отложенный логин для аккаунта {Id} завершился с ошибкой", accountId);
            }, TaskScheduler.Default);
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