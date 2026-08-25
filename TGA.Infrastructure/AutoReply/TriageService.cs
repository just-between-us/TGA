using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;
using TGA.Contract.DTOs.Llm;
using TGA.Domain.Enums;

namespace TGA.Infrastructure.AutoReply;

public partial class TriageService(ILlmClient llmClient, ILogger<TriageService> logger) : ITriageService
{
    private const string SystemPrompt = """
        Ты — модуль триажа для личного Telegram-автоответчика. Тебе присылают контекст переписки
        и новые сообщения от собеседника, на которые ещё не ответили. Реши, что делать:

        - "reply" — стоит ответить прямо сейчас (вопрос, ожидание реакции, обращение к пользователю).
        - "wait" — собеседник, похоже, не закончил мысль (сообщение обрывается, разбито на части,
          явно продолжается) — стоит подождать следующее сообщение прежде чем отвечать.
        - "skip" — отвечать не нужно (эмоциональная реакция вроде "хахаха", вежливая формула вроде
          "спокойной ночи", сообщение не требует ответа по существу).

        Отвечай СТРОГО в формате JSON, без пояснений вокруг:
        {"action": "reply" | "wait" | "skip", "reason": "краткое обоснование в одно предложение"}
        """;

    public async Task<TriageResult> EvaluateAsync(
        long peerUserId,
        IReadOnlyList<MessageDto> pendingMessages,
        IReadOnlyList<MessageDto> recentHistory,
        ContactProfileDto? profile,
        CancellationToken ct = default)
    {
        var request = new LlmChatRequest(
            Messages:
            [
                new LlmChatMessage(LlmRole.System, SystemPrompt),
                new LlmChatMessage(LlmRole.User, BuildUserPrompt(pendingMessages, recentHistory, profile))
            ],
            Temperature: 0.1); // детерминированная классификация, без приколов от ии

        try
        {
            var result = await llmClient.CompleteAsync(request, ct: ct);
            return ParseResult(result.Content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Триаж для peer={PeerId} завершился с ошибкой, пропускаю", peerUserId);
            return new TriageResult(TriageAction.Skip, $"Ошибка триажа: {ex.Message}");
        }
    }

    private static string BuildUserPrompt(
        IReadOnlyList<MessageDto> pending, IReadOnlyList<MessageDto> history, ContactProfileDto? profile)
    {
        var sb = new StringBuilder();

        if (profile is not null)
        {
            sb.AppendLine($"Собеседник: {profile.DisplayName}");
            if (!string.IsNullOrWhiteSpace(profile.Notes))
                sb.AppendLine($"Заметки: {profile.Notes}");
            if (!string.IsNullOrWhiteSpace(profile.CommunicationStyle))
                sb.AppendLine($"Стиль общения: {profile.CommunicationStyle}");
            if (!string.IsNullOrWhiteSpace(profile.AutoReplyInstructions))
                sb.AppendLine($"Инструкция для автоответа: {profile.AutoReplyInstructions}");
            sb.AppendLine();
        }

        sb.AppendLine("Последние сообщения переписки:");
        foreach (var m in history)
            sb.AppendLine($"{(m.IsOutgoing ? "Я" : profile?.DisplayName ?? "Собеседник")}: {m.Text}");

        sb.AppendLine();
        sb.AppendLine("Новые сообщения, на которые ещё не ответили:");
        foreach (var m in pending)
            sb.AppendLine($"- {m.Text}");

        return sb.ToString();
    }

    private TriageResult ParseResult(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new TriageResult(TriageAction.Skip, "Пустой ответ LLM");

        var cleaned = CodeFenceRegex().Replace(raw, "").Trim();

        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            var actionStr = doc.RootElement.GetProperty("action").GetString() ?? "skip";
            var reason = doc.RootElement.TryGetProperty("reason", out var r) ? r.GetString() : null;

            var action = actionStr.Trim().ToLowerInvariant() switch
            {
                "reply" => TriageAction.Reply,
                "wait" => TriageAction.Wait,
                _ => TriageAction.Skip
            };

            return new TriageResult(action, reason);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Не удалось разобрать ответ триажа: {Raw}", raw);
            return new TriageResult(TriageAction.Skip, $"Не удалось разобрать JSON: {ex.Message}");
        }
    }

    [GeneratedRegex(@"```json|```")]
    private static partial Regex CodeFenceRegex();
}