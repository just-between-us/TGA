using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TGA.Domain.Enums;
using TL;

namespace TGA.Infrastructure.Telegram;

public class TelegramHealthCheckService(
    TelegramClientFactory clientFactory,
    IConnectionStatusService connectionStatus,
    IAccountStorageService accountStorage,
    ILogger<TelegramHealthCheckService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(8);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken);

            if (connectionStatus.State != ConnectionState.Connected)
                continue; 

            try
            {
                var client = clientFactory.GetCurrent();

                var pingTask = client.Help_GetConfig();
                var completed = await Task.WhenAny(pingTask, Task.Delay(PingTimeout, stoppingToken));
                if (completed == pingTask)
                {
                    logger.LogInformation("Health-check получил ответ за {Timeout}с", PingTimeout.TotalSeconds);
                }
                if (completed != pingTask)
                {
                    logger.LogWarning("Health-check не получил ответ за {Timeout}с — соединение считается потерянным", PingTimeout.TotalSeconds);
                    connectionStatus.SetDisconnected();
                    continue;
                }
                await pingTask;
                connectionStatus.SetConnected(connectionStatus.UserDisplayName ?? "Telegram");
            }
            catch (InvalidOperationException)
            {
                // клиент ещё не инициализирован — нормальная ситуация до логина
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Health-check обнаружил обрыв соединения");
                connectionStatus.SetDisconnected();
            }
        }
    }
}