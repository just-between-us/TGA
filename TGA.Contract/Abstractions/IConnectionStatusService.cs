using TGA.Domain.Enums;

namespace TGA.Contract.Abstractions;

public interface IConnectionStatusService
{
    ConnectionState State { get; }
    string? UserDisplayName { get; }
    event Action? StateChanged;
    void SetConnecting();
    void SetUpdating();
    void SetConnected(string userDisplayName);
    void SetDisconnected();
}