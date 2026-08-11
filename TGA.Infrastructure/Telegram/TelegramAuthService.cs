using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TL;

namespace TGA.Infrastructure.Telegram;

public class TelegramAuthService(
    TelegramClientFactory clientFactory,
    IAccountStorageService accountStorage,
    ITelegramMessageService messageService,
    IConnectionStatusService connectionStatus,
    ILogger<TelegramAuthService> logger) : ITelegramAuthService
{
    private readonly SemaphoreSlim _authLock = new(1, 1);
    private TaskCompletionSource<string?>? _pendingInput;

    public AuthStep CurrentStep { get; private set; } = AuthStep.NotStarted;
    public string? ErrorMessage { get; private set; }
    public bool IsLoggedIn { get; private set; }

    public event Action? StateChanged;

    public Task StartLoginAsync() => StartAuthFlowAsync(isAddAccount: false);

    public Task StartAddAccountAsync() => StartAuthFlowAsync(isAddAccount: true);

    private async Task StartAuthFlowAsync(bool isAddAccount)
    {
        ErrorMessage = null;
        CurrentStep = AuthStep.NotStarted;
        connectionStatus.SetConnecting();
        Notify();

        await _authLock.WaitAsync();
        try
        {
            try
            {
                messageService.StopMonitoring();
                clientFactory.Reset();

                var client = clientFactory.CreateNew(ConfigCallback);
                var user = await Task.Run(() => client.LoginUserIfNeeded());
                
                var sessionData = clientFactory.GetCurrentSessionBytes();

                if (isAddAccount)
                {
                    await accountStorage.SaveAccountAsync(
                        user.ID,
                        $"{user.first_name} {user.last_name}".Trim(),
                        user.phone,
                        sessionData);
                }
                else
                {
                    await accountStorage.SaveAccountAsync(
                        user.ID,
                        $"{user.first_name} {user.last_name}".Trim(),
                        user.phone,
                        sessionData);
                }

                IsLoggedIn = true;
                CurrentStep = AuthStep.Done;
                Notify();

                var displayName = !string.IsNullOrEmpty(user.username)
                    ? $"@{user.username}"
                    : $"{user.first_name} {user.last_name}".Trim();

                connectionStatus.SetConnected(displayName);
                messageService.StartMonitoring();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка входа в Telegram");
                ErrorMessage = ex.Message;
                CurrentStep = AuthStep.Error;
                connectionStatus.SetDisconnected();
                Notify();
            }
        }
        finally
        {
            _authLock.Release();
        }
    }

    public async Task<bool> RestoreSessionAsync(int accountId)
    {
        await _authLock.WaitAsync();
        try
        {
            connectionStatus.SetConnecting();

            var sessionData = await accountStorage.GetSessionDataAsync(accountId);
            if (sessionData is null || sessionData.Length == 0)
            {
                logger.LogInformation("Сохранённой сессии для аккаунта {Id} нет, нужен обычный вход", accountId);
                connectionStatus.SetDisconnected();
                return false;
            }

            try
            {
                logger.LogInformation("Пытаюсь восстановить сессию для аккаунта {Id}", accountId);
                messageService.StopMonitoring();
                clientFactory.Reset();

                var client = clientFactory.CreateNew(ConfigCallback, sessionData);

                var loginTask = Task.Run(() => client.LoginUserIfNeeded());
                var completed = await Task.WhenAny(loginTask, Task.Delay(TimeSpan.FromSeconds(10)));

                string? displayName = null;
                if (completed == loginTask)
                {
                    var user = await loginTask;
                    displayName = !string.IsNullOrEmpty(user.username)
                        ? $"@{user.username}"
                        : $"{user.first_name} {user.last_name}".Trim();
                }
                else
                {
                    logger.LogInformation(
                        "Логин для аккаунта {Id} ещё не завершён, продолжаю использовать сохранённую сессию",
                        accountId);

                    var account = await accountStorage.GetByIdAsync(accountId);
                    displayName = account?.DisplayName;
                }

                await accountStorage.SetActiveAsync(accountId);
                await accountStorage.UpdateSessionDataAsync(accountId, clientFactory.GetCurrentSessionBytes());

                IsLoggedIn = true;
                CurrentStep = AuthStep.Done;

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    connectionStatus.SetConnected(displayName);
                }
                else
                {
                    connectionStatus.SetConnected("Telegram");
                }

                Notify();

                messageService.StartMonitoring();
                logger.LogInformation("Сессия для аккаунта {Id} восстановлена или подготовлена", accountId);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Не удалось восстановить сессию для аккаунта {Id}", accountId);
                clientFactory.Reset();
                connectionStatus.SetDisconnected();
                CurrentStep = AuthStep.NotStarted;
                Notify();
                return false;
            }
        }
        finally
        {
            _authLock.Release();
        }
    }

    public void SubmitInput(string value) => _pendingInput?.TrySetResult(value);

    public async Task LogoutAsync()
    {
        messageService.StopMonitoring();

        var active = await accountStorage.GetActiveAccountAsync();
        if (active is not null)
        {
            try
            {
                var client = clientFactory.GetCurrent();
                await client.Auth_LogOut();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Auth_LogOut завершился с ошибкой, продолжаю локальный сброс");
            }
        }

        clientFactory.Reset();
        IsLoggedIn = false;
        CurrentStep = AuthStep.NotStarted;
        connectionStatus.SetDisconnected();
        Notify();
    }

    public async Task SwitchAccountAsync(int accountId)
    {
        messageService.StopMonitoring();
        clientFactory.Reset();

        var restored = await RestoreSessionAsync(accountId);

        if (!restored)
        {
            await accountStorage.SetActiveAsync(accountId);
            CurrentStep = AuthStep.NotStarted;
            Notify();
        }
    }


    private string? ConfigCallback(string what) => what switch
    {
        "api_id" => clientFactory.ApiId,
        "api_hash" => clientFactory.ApiHash,
        "server_address" => "2>149.154.167.50:443",
        "phone_number" => WaitForInput(AuthStep.WaitingPhone),
        "verification_code" => WaitForInput(AuthStep.WaitingCode),
        "password" => WaitForInput(AuthStep.WaitingPassword),
        _ => null
    };

    private string? WaitForInput(AuthStep step)
    {
        _pendingInput = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CurrentStep = step;
        Notify();
        return _pendingInput.Task.GetAwaiter().GetResult();
    }

    private void Notify() => StateChanged?.Invoke();
}