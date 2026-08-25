using TGA.Contract.DTOs;

namespace TGA.Contract.Abstractions;

public interface ITelegramOtherUpdateNotifier
{
    event Action<TelegramOtherUpdateDto>? OtherUpdateReceived;
}