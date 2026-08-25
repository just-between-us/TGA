namespace TGA.Contract.DTOs.Llm;

public record LlmChatResult(
    string? Content,
    List<LlmToolCall>? ToolCalls,
    string? FinishReason,
    int? PromptTokens,
    int? CompletionTokens);