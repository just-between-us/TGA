using Microsoft.Extensions.Options;
using TGA.Contract.Options;
using WTelegram;

namespace TGA.Infrastructure.Telegram;

public class TelegramClientFactory(
    IOptions<TelegramOptions> options,
    TelegramOtherUpdateNotifier otherUpdateNotifier
    )
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    private Client? _current;
    private MemoryStream? _sessionStream;

    public Client CreateNew(
        Func<string, string?> configCallback,
        byte[]? existingSessionData = null)
    {
        _lock.Wait();

        try
        {
            if (_current is not null && _sessionStream is not null && existingSessionData is null)
            {
                return _current;
            }
            
            if (_current is not null)
                otherUpdateNotifier.Detach(_current);
            
            _current?.Dispose();
            _sessionStream?.Dispose();

            _sessionStream = CreateExpandableStream(existingSessionData);

            _current = new Client(configCallback, _sessionStream);
            
            return _current;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static MemoryStream CreateExpandableStream(byte[]? data)
    {
        var stream = new MemoryStream();

        if (data is { Length: > 0 })
        {
            stream.Write(data, 0, data.Length);
            stream.Position = 0;
        }

        return stream;
    }

    public Client GetCurrent() =>
        _current ?? throw new InvalidOperationException(
            "Telegram-клиент ещё не инициализирован.");

    public byte[] GetCurrentSessionBytes() =>
        _sessionStream?.ToArray() ?? [];

    public void Reset()
    {
        _lock.Wait();

        try
        {
            _current?.Dispose();
            _current = null;

            _sessionStream?.Dispose();
            _sessionStream = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public string ApiId => options.Value.ApiId;
    public string ApiHash => options.Value.ApiHash;
}