using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs.Llm;
using TGA.Domain.Enums;

namespace TGA.Infrastructure.Llm;

public class OpenAiCompatibleLlmClient(
    IHttpClientFactory httpClientFactory,
    ILlmSettingsStorageService settingsStorage,
    ILogger<OpenAiCompatibleLlmClient> logger) : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<LlmChatResult> CompleteAsync(
        LlmChatRequest request, LlmProviderSettingsDto? settingsOverride = null, CancellationToken ct = default)
    {
        var settings = settingsOverride ?? await settingsStorage.GetActiveAsync()
            ?? throw new InvalidOperationException(
                "Нет активного LLM-провайдера. Настройте его на странице настроек.");

        var client = httpClientFactory.CreateClient("llm");
        client.Timeout = TimeSpan.FromSeconds(100);

        var payload = BuildPayload(request, settings);
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(settings.BaseUrl))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        var response = await client.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("LLM-провайдер вернул ошибку {Status}: {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"LLM-провайдер вернул {(int)response.StatusCode}: {body}");
        }

        return ParseResponse(body);
    }

    private static object BuildPayload(LlmChatRequest request, LlmProviderSettingsDto settings)
    {
        var wireMessages = request.Messages.Select(m => new
        {
            role = ToWireRole(m.Role),
            content = m.Content,
            tool_call_id = m.ToolCallId,
            name = m.Name,
            tool_calls = m.ToolCalls?.Select(tc => new
            {
                id = tc.Id,
                type = "function",
                function = new { name = tc.Name, arguments = tc.ArgumentsJson }
            }).ToList()
        }).ToList();

        var wireTools = request.Tools?.Select(t => new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = JsonDocument.Parse(t.ParametersJsonSchema).RootElement
            }
        }).ToList();

        return new
        {
            model = request.Model ?? settings.Model,
            messages = wireMessages,
            temperature = request.Temperature ?? settings.Temperature,
            max_tokens = request.MaxTokens,
            tools = wireTools
        };
    }

    private static LlmChatResult ParseResponse(string body)
    {
        var parsed = JsonSerializer.Deserialize<OpenAiResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Не удалось разобрать ответ LLM-провайдера.");

        var choice = parsed.Choices.FirstOrDefault();
        var message = choice?.Message;

        var toolCalls = message?.ToolCalls?.Select(tc =>
            new LlmToolCall(tc.Id, tc.Function.Name, tc.Function.Arguments)).ToList();

        return new LlmChatResult(
            message?.Content,
            toolCalls,
            choice?.FinishReason,
            parsed.Usage?.PromptTokens,
            parsed.Usage?.CompletionTokens);
    }

    private static string ToWireRole(LlmRole role) => role switch
    {
        LlmRole.System => "system",
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        LlmRole.Tool => "tool",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static string BuildEndpoint(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        return trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/chat/completions";
    }
}