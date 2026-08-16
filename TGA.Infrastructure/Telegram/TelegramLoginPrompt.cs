using TGA.Contract.Abstractions;

namespace TGA.Infrastructure.Telegram;

public class TelegramLoginPrompt(TelegramClientFactory clientFactory)
{
    private TaskCompletionSource<string?>? _pendingInput;

    public event Action<AuthStep>? StepRequested;

    public string? ConfigCallback(string what) => what switch
    {
        "api_id" => clientFactory.ApiId,
        "api_hash" => clientFactory.ApiHash,
        "server_address" => "2>149.154.167.50:443",
        "phone_number" => WaitForInput(AuthStep.WaitingPhone),
        "verification_code" => WaitForInput(AuthStep.WaitingCode),
        "password" => WaitForInput(AuthStep.WaitingPassword),
        _ => null
    };

    public void SubmitInput(string value) => _pendingInput?.TrySetResult(value);

    private string? WaitForInput(AuthStep step)
    {
        _pendingInput = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        StepRequested?.Invoke(step);
        return _pendingInput.Task.GetAwaiter().GetResult();
    }
}