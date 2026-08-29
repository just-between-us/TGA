using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TL;
using WTelegram;

namespace TGA.Infrastructure.Telegram;

public class TelegramConnectionCheckService(
    TelegramClientFactory clientFactory,
    IAccountStorageService accountStorage,
    TelegramPeerResolver peerResolver,
    IConnectionStatusService connectionStatus,
    ILogger<TelegramConnectionCheckService> logger) : ITelegramConnectionCheckService
{
    public async Task<bool> SendConnectionCheckAsync()
    {
        var active = await accountStorage.GetActiveAccountAsync()
                     ?? throw new InvalidOperationException("Нет активного аккаунта");

        var client = clientFactory.GetCurrent();
        const long peerUserId = 777000L;
        var payload = $"TGA connectivity check {DateTime.UtcNow:O}";

        connectionStatus.SetConnecting();

        try
        {
            var peer = await peerResolver.ResolveInputPeerAsync(client, peerUserId);
            var sent = await client.SendMessageAsync(peer, payload);

            logger.LogInformation("Проверка Telegram-соединения прошла успешно");

            await TelegramMessageService.TryDeleteSentMessageAsync(client, sent.id);
            connectionStatus.SetConnected(active.DisplayName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Проверка Telegram-соединения завершилась с ошибкой");
            connectionStatus.SetDisconnected();
            return false;
        }
    }
}
