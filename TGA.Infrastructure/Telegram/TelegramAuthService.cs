using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TL;

namespace TGA.Infrastructure.Telegram;

public class TelegramAuthService : ITelegramAuthService
{
    private readonly TelegramClientFactory _clientFactory;
    private readonly IAccountStorageService _accountStorage;
    private readonly ITelegramMessageService _messageService;
    private readonly IConnectionStatusService _connectionStatus;
    private readonly TelegramLoginPrompt _loginPrompt;
    private readonly TelegramSessionRestorer _sessionRestorer;
    private readonly ILogger<TelegramAuthService> _logger;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    public TelegramAuthService(
        TelegramClientFactory clientFactory,
        IAccountStorageService accountStorage,
        ITelegramMessageService messageService,
        IConnectionStatusService connectionStatus,
        TelegramLoginPrompt loginPrompt,
        TelegramSessionRestorer sessionRestorer,
        ILogger<TelegramAuthService> logger)
    {
        _clientFactory = clientFactory;
        _accountStorage = accountStorage;
        _messageService = messageService;
        _connectionStatus = connectionStatus;
        _loginPrompt = loginPrompt;
        _sessionRestorer = sessionRestorer;
        _logger = logger;

        _loginPrompt.StepRequested += OnLoginStepRequested;
    }

    public AuthStep CurrentStep { get; private set; } = AuthStep.NotStarted;
    public string? ErrorMessage { get; private set; }
    public bool IsLoggedIn { get; private set; }

    public event Action? StateChanged;

    public Task StartLoginAsync() => StartAuthFlowAsync();

    public Task StartAddAccountAsync() => StartAuthFlowAsync();

    private async Task StartAuthFlowAsync()
    {
        ErrorMessage = null;
        CurrentStep = AuthStep.NotStarted;
        _connectionStatus.SetConnecting();
        Notify();

        await _authLock.WaitAsync();
        try
        {
            try
            {
                _messageService.StopMonitoring();
                _clientFactory.Reset();

                var client = _clientFactory.CreateNew(_loginPrompt.ConfigCallback);
                var user = await Task.Run(() => client.LoginUserIfNeeded());

                var sessionData = _clientFactory.GetCurrentSessionBytes();
                await _accountStorage.SaveAccountAsync(user.ID, GetDisplayName(user), user.phone, sessionData);

                IsLoggedIn = true;
                CurrentStep = AuthStep.Done;
                Notify();

                _connectionStatus.SetConnected(GetDisplayName(user));
                _messageService.StartMonitoring();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка входа в Telegram");
                ErrorMessage = ex.Message;
                CurrentStep = AuthStep.Error;
                _connectionStatus.SetDisconnected();
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
            _connectionStatus.SetConnecting();

            var sessionData = await _accountStorage.GetSessionDataAsync(accountId);
            if (sessionData is null || sessionData.Length == 0)
            {
                _logger.LogInformation("Сохранённой сессии для аккаунта {Id} нет, нужен обычный вход", accountId);
                _connectionStatus.SetDisconnected();
                return false;
            }

            try
            {
                _logger.LogInformation("Пытаюсь восстановить сессию для аккаунта {Id}", accountId);
                _messageService.StopMonitoring();
                _clientFactory.Reset();

                var result = await _sessionRestorer.TryRestoreAsync(accountId, sessionData, _loginPrompt.ConfigCallback);

                IsLoggedIn = true;
                CurrentStep = AuthStep.Done;
                _connectionStatus.SetConnected(
                    string.IsNullOrWhiteSpace(result.DisplayName) ? "Telegram" : result.DisplayName);
                Notify();

                _messageService.StartMonitoring();
                _logger.LogInformation("Сессия для аккаунта {Id} восстановлена или подготовлена", accountId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось восстановить сессию для аккаунта {Id}", accountId);
                _clientFactory.Reset();
                _connectionStatus.SetDisconnected();
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

    public void SubmitInput(string value) => _loginPrompt.SubmitInput(value);

    public async Task LogoutAsync()
    {
        _messageService.StopMonitoring();

        var active = await _accountStorage.GetActiveAccountAsync();
        if (active is not null)
        {
            try
            {
                await _clientFactory.GetCurrent().Auth_LogOut();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auth_LogOut завершился с ошибкой, продолжаю локальный сброс");
            }
        }

        _clientFactory.Reset();
        IsLoggedIn = false;
        CurrentStep = AuthStep.NotStarted;
        _connectionStatus.SetDisconnected();
        Notify();
    }

    public async Task SwitchAccountAsync(int accountId)
    {
        _messageService.StopMonitoring();
        _clientFactory.Reset();

        var restored = await RestoreSessionAsync(accountId);
        if (!restored)
        {
            await _accountStorage.SetActiveAsync(accountId);
            CurrentStep = AuthStep.NotStarted;
            Notify();
        }
    }

    private void OnLoginStepRequested(AuthStep step)
    {
        CurrentStep = step;
        Notify();
    }

    private static string GetDisplayName(User user) =>
        !string.IsNullOrEmpty(user.username)
            ? $"@{user.username}"
            : $"{user.first_name} {user.last_name}".Trim();

    private void Notify() => StateChanged?.Invoke();
}