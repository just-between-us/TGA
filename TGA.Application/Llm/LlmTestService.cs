using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs.Llm;
using TGA.Domain.Enums;

namespace TGA.Application.Llm;

public class LlmTestService(
    ILlmClient? llmClient,
    ILogger<LlmTestService> logger)
{
    public async Task<string> SendTestMessageAsync(
        List<LlmChatMessage> history,
        string userMessage,
        LlmProviderSettingsDto settings,
        CancellationToken ct = default)
    {
        var messages = new List<LlmChatMessage>();

        if (!string.IsNullOrWhiteSpace(settings.SystemPrompt))
            messages.Add(new LlmChatMessage(LlmRole.System, settings.SystemPrompt));

        messages.AddRange(history);
        messages.Add(new LlmChatMessage(LlmRole.User, userMessage));
        
        logger.LogInformation("Запрос отправлен {settings.Model}, с температурой {settings.Temperature}, текст сообщения: {messages}", settings.Model, settings.Temperature, Truncate(messages));

        var request = new LlmChatRequest(messages, settings.Model, settings.Temperature);
        var result = await llmClient.CompleteAsync(request, settings, ct);

        logger.LogInformation("Пришёл ответ: {result}", Truncate(result));
        
        return result.Content ?? "(пустой ответ)";
    }
    
    private static string Truncate(List<LlmChatMessage> messages, int reduce = 10)
    {
        var text =  string.Join(" ", messages.Select(m => m.Content));
        if (string.IsNullOrEmpty(text)) return text;
        if  (text.Length < reduce) return text;
        return text.Length <= 10 ? text : text.Substring(0, reduce) + "...";
    }
    public static string Truncate(LlmChatResult messages, int reduce = 10)
    {
        var text = messages.Content;
        if (string.IsNullOrEmpty(text)) return text;
        if  (text.Length < reduce) return text;
        return text.Length <= 10 ? text : text.Substring(0, reduce) + "...";
    }
}