using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;

namespace TGA.Infrastructure.Telegram;

public class TelegramSessionRestoreHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<TelegramSessionRestoreHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Yield();

            using var scope = scopeFactory.CreateScope();

            var accountStorage =
                scope.ServiceProvider.GetRequiredService<IAccountStorageService>();

            var authService =
                scope.ServiceProvider.GetRequiredService<ITelegramAuthService>();

            var messageService =
                scope.ServiceProvider.GetRequiredService<ITelegramMessageService>();

            var active = await accountStorage.GetActiveAccountAsync();

            if (active is null)
            {
                logger.LogInformation(
                    "Активного аккаунта нет — восстановление сессии пропущено");

                return;
            }

            logger.LogInformation(
                "Пробую восстановить сессию для {Name}",
                active.DisplayName);

            var restored = await authService.RestoreSessionAsync(active.Id);

            if (!restored)
            {
                logger.LogWarning(
                    "Не удалось восстановить сессию для {Name}",
                    active.DisplayName);

                return;
            }

            try
            {
                await messageService.SyncDialogsAsync();

                logger.LogInformation(
                    "Синхронизация диалогов после восстановления завершена");
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Не удалось синхронизировать диалоги после восстановления сессии");
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ошибка фонового восстановления Telegram-сессии");
        }
    }
}