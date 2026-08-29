namespace TGA.Contract.Abstractions;

public interface ITelegramConnectionCheckService
{
    Task<bool> SendConnectionCheckAsync();
}
