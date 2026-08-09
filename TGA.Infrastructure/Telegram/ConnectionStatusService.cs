using TGA.Contract.Abstractions;
using TGA.Domain.Enums;

namespace TGA.Infrastructure.Telegram;

public class ConnectionStatusService : IConnectionStatusService
{
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public string? UserDisplayName { get; private set; }

    public event Action? StateChanged;

    public void SetConnecting()
    {
        State = ConnectionState.Connecting;
        Notify();
    }

    public void SetUpdating()
    {
        State = ConnectionState.Updating;
        Notify();
    }

    public void SetConnected(string userDisplayName)
    {
        State = ConnectionState.Connected;
        UserDisplayName = userDisplayName;
        Notify();
    }

    public void SetDisconnected()
    {
        State = ConnectionState.Disconnected;
        UserDisplayName = null;
        Notify();
    }

    private void Notify() => StateChanged?.Invoke();
}