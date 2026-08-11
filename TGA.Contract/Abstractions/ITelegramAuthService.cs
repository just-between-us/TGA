namespace TGA.Contract.Abstractions;

public enum AuthStep { NotStarted, WaitingPhone, WaitingCode, WaitingPassword, Done, Error }

public interface ITelegramAuthService
{
    AuthStep CurrentStep { get; }
    string? ErrorMessage { get; }
    bool IsLoggedIn { get; }

    event Action? StateChanged;

    Task StartLoginAsync();
    Task StartAddAccountAsync();
    void SubmitInput(string value);

    Task LogoutAsync();

    Task SwitchAccountAsync(int accountId);

    Task<bool> RestoreSessionAsync(int accountId);
}