using System.Text.Json;
using System.Text.Json.Serialization;

namespace TGA.Infrastructure.Llm;


internal class OpenAiRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = "";
    [JsonPropertyName("messages")] public List<OpenAiMessage> Messages { get; set; } = [];
    [JsonPropertyName("temperature")] public double? Temperature { get; set; }
    [JsonPropertyName("max_tokens")] public int? MaxTokens { get; set; }
    [JsonPropertyName("tools")] public List<OpenAiTool>? Tools { get; set; }
}

internal class OpenAiMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    [JsonPropertyName("content")] public string? Content { get; set; }
    [JsonPropertyName("tool_call_id")] public string? ToolCallId { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("tool_calls")] public List<OpenAiToolCall>? ToolCalls { get; set; }
}

internal class OpenAiTool
{
    [JsonPropertyName("type")] public string Type { get; set; } = "function";
    [JsonPropertyName("function")] public OpenAiFunctionDef Function { get; set; } = new();
}

internal class OpenAiFunctionDef
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("parameters")] public JsonElement? Parameters { get; set; }
}

internal class OpenAiToolCall
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "function";
    [JsonPropertyName("function")] public OpenAiToolCallFunction Function { get; set; } = new();
}

internal class OpenAiToolCallFunction
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("arguments")] public string Arguments { get; set; } = "";
}

internal class OpenAiResponse
{
    [JsonPropertyName("choices")] public List<OpenAiChoice> Choices { get; set; } = [];
    [JsonPropertyName("usage")] public OpenAiUsage? Usage { get; set; }
}

internal class OpenAiChoice
{
    [JsonPropertyName("message")] public OpenAiMessage? Message { get; set; }
    [JsonPropertyName("finish_reason")] public string? FinishReason { get; set; }
}

internal class OpenAiUsage
{
    [JsonPropertyName("prompt_tokens")] public int? PromptTokens { get; set; }
    [JsonPropertyName("completion_tokens")] public int? CompletionTokens { get; set; }
}