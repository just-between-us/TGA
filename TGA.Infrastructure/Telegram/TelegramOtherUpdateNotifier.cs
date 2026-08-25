using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;
using TL;
using WTelegram;

namespace TGA.Infrastructure.Telegram;

public class TelegramOtherUpdateNotifier(ILogger<TelegramOtherUpdateNotifier> logger) : ITelegramOtherUpdateNotifier
{
    public event Action<TelegramOtherUpdateDto>? OtherUpdateReceived;

    public void Attach(Client client)
    {
        client.OnOther += HandleOtherAsync;
    }

    public void Detach(Client client)
    {
        client.OnOther -= HandleOtherAsync;
    }

    private Task HandleOtherAsync(IObject obj)
    {
        var typeName = obj.GetType().Name;
        var description = Describe(obj);

        logger.LogInformation("OnOther: {TypeName} — {Description}", typeName, description);

        var dto = new TelegramOtherUpdateDto(DateTime.Now, typeName, description);
        OtherUpdateReceived?.Invoke(dto);

        return Task.CompletedTask;
    }

    private static string Describe(IObject obj) => obj switch
    {
        UpdatesTooLong => "Слишком много пропущенных апдейтов — требуется полная пересинхронизация",
        UpdateShort updateShort => $"Короткий апдейт: {updateShort.update.GetType().Name}",
        _ => obj.ToString() ?? obj.GetType().Name
    };
}