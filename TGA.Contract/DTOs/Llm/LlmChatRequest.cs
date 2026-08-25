namespace TGA.Contract.DTOs.Llm;

public record LlmChatRequest(
    List<LlmChatMessage> Messages,
    string? Model = null,
    double? Temperature = null,
    int? MaxTokens = null,
    List<LlmToolDefinition>? Tools = null);