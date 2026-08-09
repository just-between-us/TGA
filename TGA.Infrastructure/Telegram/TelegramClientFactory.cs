using Microsoft.Extensions.Options;
using TGA.Contract.Options;
using WTelegram;

namespace TGA.Infrastructure.Telegram;

public class TelegramClientFactory(IOptions<TelegramOptions> options)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Client? _current;

    public Client CreateNew(Func<string, string?> configCallback)
    {
        _lock.Wait();
        try
        {
            _current?.Dispose();
            _current = new Client(configCallback);
            return _current;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Client GetCurrent()
    {
        _lock.Wait();
        try
        {
            return _current ?? throw new InvalidOperationException(
                "Telegram-клиент ещё не инициализирован — нужно сначала выполнить вход.");
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Reset()
    {
        _lock.Wait();
        try
        {
            _current?.Dispose();
            _current = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public string ApiId => options.Value.ApiId;
    public string ApiHash => options.Value.ApiHash;
}