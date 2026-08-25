using TGA.Domain.Enums;

namespace TGA.Contract.DTOs.Llm;

public record LlmProviderSettingsDto(
    int Id,
    string Name,
    LlmProvider Provider,
    string BaseUrl,
    string ApiKey,
    string Model,
    string? SystemPrompt,
    double Temperature,
    bool IsActive,
    DateTime UpdatedAt);