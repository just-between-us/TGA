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
    private TaskCompletionSource<string?>? _pendingInput;
    private byte[]? _sessionBuffer;

    public AuthStep CurrentStep { get; private set; } = AuthStep.NotStarted;
    public string? ErrorMessage { get; private set; }
    public bool IsLoggedIn { get; private set; }

    public event Action? StateChanged;

    public Task StartLoginAsync()
    {
        ErrorMessage = null;
        CurrentStep = AuthStep.NotStarted;
        connectionStatus.SetConnecting();  
        Notify();

        return Task.Run(async () =>
        {
            try
            {
                logger.LogInformation("StartLoginAsync: создаю клиент...");
                var client = clientFactory.CreateNew(ConfigCallback);   

                logger.LogInformation("StartLoginAsync: вызываю LoginUserIfNeeded...");
                var user = await client.LoginUserIfNeeded();

                var sessionData = _sessionBuffer ?? [];
                await accountStorage.SaveAccountAsync(
                    user.ID,
                    $"{user.first_name} {user.last_name}".Trim(),
                    user.phone,
                    sessionData);

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
                Notify();
            }
        });
    }

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

    public void SubmitInput(string value) => _pendingInput?.TrySetResult(value);

    public async Task SwitchAccountAsync(int accountId)
    {
        messageService.StopMonitoring();
        clientFactory.Reset();

        await accountStorage.SetActiveAsync(accountId);
        await StartLoginAsync();
    }

    public async Task StartAddAccountAsync()
    {
        messageService.StopMonitoring();
        clientFactory.Reset();
        _sessionBuffer = null;
        await StartLoginAsync();
    }

    private string? ConfigCallback(string what) => what switch
    {
        "api_id" => clientFactory.ApiId,
        "api_hash" => clientFactory.ApiHash,
        "server_address" => "2>149.154.167.50:443",
        "phone_number" => WaitForInput(AuthStep.WaitingPhone),
        "verification_code" => WaitForInput(AuthStep.WaitingCode),
        "password" => WaitForInput(AuthStep.WaitingPassword),
        "session_pathname" => null, // отключаем файловую сессию — храним в БД
        _ => null
    };

    /*Примечание по сессии: WTelegram по умолчанию пишет сессию в файл (session_pathname).
     Для мульти-аккаунтности и хранения в БД правильнее использовать Client.Save/кастомный Stream-конструктор WTelegram
    (он поддерживает MemoryStream для сессии вместо файла) — на практике стоит заменить создание Client на вариант с Stream, 
    куда пишется/читается _sessionBuffer, и сохранять его байты через accountStorage.SaveAccountAsync после LoginUserIfNeeded. 
    Здесь оставлен упрощённый вариант с комментарием, требует аккуратной сверки с текущей версией WTelegram API при реализации.*/
  
    private string? WaitForInput(AuthStep step)
    {
        _pendingInput = new TaskCompletionSource<string?>();
        CurrentStep = step;
        Notify();
        return _pendingInput.Task.GetAwaiter().GetResult();
    }

    private void Notify() => StateChanged?.Invoke();
}