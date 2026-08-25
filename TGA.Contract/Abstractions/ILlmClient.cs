using TGA.Contract.DTOs.Llm;

namespace TGA.Contract.Abstractions;

public interface ILlmClient
{
    Task<LlmChatResult> CompleteAsync(
        LlmChatRequest request,
        LlmProviderSettingsDto? settingsOverride = null,
        CancellationToken ct = default);
}