using TGA.Domain.Enums;

namespace TGA.Contract.DTOs.Llm;


public record LlmChatMessage(
    LlmRole Role,
    string? Content,
    string? ToolCallId = null,
    string? Name = null,
    List<LlmToolCall>? ToolCalls = null);