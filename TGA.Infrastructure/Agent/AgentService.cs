using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;
using TGA.Contract.DTOs.Llm;
using TGA.Contract.Options;
using TGA.Domain.Enums;

namespace TGA.Infrastructure.Agent;

public class AgentService(
    ILlmClient llmClient,
    ILlmRoleAssignmentService roleAssignments,
    IAgentRunStorageService runStorage,
    IEnumerable<IAgentTool> tools,
    ITelegramMessageService telegramMessageService,
    IMessageStorageService messageStorage,
    IRuntimeSettingsStorageService settingsStorage,
    ILogger<AgentService> logger) : IAgentService
{
    private const string AskClarifyingToolName = "ask_clarifying_question";

    public async Task StartAsync(
        int accountId, long peerUserId, IReadOnlyList<MessageDto> triggerMessages,
        IReadOnlyList<MessageDto> recentHistory, ContactProfileDto profile, CancellationToken ct = default)
    {
        var settings = await settingsStorage.GetAsync();
        var systemPrompt = await BuildSystemPromptAsync(accountId, profile, settings, ct);

        logger.LogInformation("Агент взял в работу: {AgentTask}", recentHistory.OrderByDescending(x => x.Time).FirstOrDefault());
        logger.LogInformation("Системный промпт: {systemPrompt}", systemPrompt);
        
        var messages = new List<LlmChatMessage>
        {
            new(LlmRole.System, systemPrompt),
            new(LlmRole.User, BuildInitialUserPrompt(triggerMessages, recentHistory, profile))
        };
        
        logger.LogInformation("Сообщений для контекста: {messages}", messages.Count);
        

        await RunLoopAsync(accountId, peerUserId, messages, clarificationCount: 0, profile.Mode, settings, ct);
    }

    public async Task ResumeAsync(
        int accountId, long peerUserId, IReadOnlyList<MessageDto> newMessages, CancellationToken ct = default)
    {
        var run = await runStorage.GetActiveRunAsync(accountId, peerUserId);
        if (run is not { State: AgentRunState.WaitingClarification })
        {
            logger.LogWarning("Нет ожидающего уточнения агент-рана для peer={PeerId}, игнорирую резюме", peerUserId);
            return;
        }

        var settings = await settingsStorage.GetAsync();
        
        var messages = new List<LlmChatMessage>(run.Messages)
        {
            new(LlmRole.User, string.Join("\n", newMessages.Select(m => m.Text)))
        };

        await RunLoopAsync(accountId, peerUserId, messages, run.ClarificationCount, run.Mode, settings, ct);
    }

    private async Task RunLoopAsync(
        int accountId, long peerUserId, List<LlmChatMessage> messages,
        int clarificationCount, AutoReplyMode mode, RuntimeSettingsDto settings, CancellationToken ct)
    {
        var llmSettings = await roleAssignments.ResolveAsync(LlmUsageRole.Agent);
        if (llmSettings is null)
        {
            logger.LogWarning("Для роли Agent не настроен LLM-провайдер, peer={PeerId}", peerUserId);
            return;
        }

        for (var iteration = 0; iteration < settings.MaxToolIterations; iteration++)
        {
            var toolDefs = BuildToolDefinitions(clarificationCount >= settings.MaxClarifications);
            var request = new LlmChatRequest(messages, llmSettings.Model, llmSettings.Temperature, Tools: toolDefs);

            logger.LogInformation("Запускаю {iteration} итерацию агентного цикла", iteration++);
            
            LlmChatResult result;
            
            try
            {
                result = await llmClient.CompleteAsync(request, llmSettings, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Агент упал на peer={PeerId}", peerUserId);
                await runStorage.MarkFailedAsync(accountId, peerUserId);
                return;
            }

            // финальный текстовый ответ —> (модель не вызвала ни одного инструмента)
            if (result.ToolCalls is not { Count: > 0 })
            {
                await telegramMessageService.SendMessageAsync(peerUserId, result.Content ?? "Не знаю, что ответить."); //TODO: финальный fallback (llm не смогла ответить) -> в настройки
                
                logger.LogInformation("(Агент решил не вызывать инструмент) Ответил {peerUserId}: {result.Content}", peerUserId, result.Content);
                
                await runStorage.MarkCompletedAsync(accountId, peerUserId);
                return;
            }

            messages.Add(new LlmChatMessage(LlmRole.Assistant, result.Content, ToolCalls: result.ToolCalls));

            var askClarify = result.ToolCalls.FirstOrDefault(tc => tc.Name == AskClarifyingToolName);

            foreach (var toolCall in result.ToolCalls)
            {
                if (toolCall.Name == AskClarifyingToolName)
                {
                    messages.Add(new LlmChatMessage(
                        LlmRole.Tool, "Вопрос отправлен собеседнику, жду ответа.",
                        ToolCallId: toolCall.Id, Name: toolCall.Name));
                    continue;
                }

                var tool = tools.FirstOrDefault(t => t.Name == toolCall.Name);
                var toolResult = tool is null
                    ? $"Инструмент {toolCall.Name} не найден."
                    : await tool.ExecuteAsync(new AgentToolContext(accountId, peerUserId), toolCall.ArgumentsJson, ct);

                messages.Add(new LlmChatMessage(LlmRole.Tool, toolResult, ToolCallId: toolCall.Id, Name: toolCall.Name));
            }

            if (askClarify is not null)
            {
                var question = ExtractClarifyingQuestion(askClarify.ArgumentsJson);
                await telegramMessageService.SendMessageAsync(peerUserId, question);

                logger.LogInformation("Задаю уточняющий вопрос: {question}", question);
                
                clarificationCount++;
                await runStorage.UpsertAsync(accountId, peerUserId, AgentRunState.WaitingClarification, clarificationCount, messages, mode);
                return; // ждём ответ пользователя — продолжится через ResumeAsync
            }
        }

        logger.LogWarning("Агент для peer={PeerId} исчерпал лимит итераций без финального ответа", peerUserId);
        
        await telegramMessageService.SendMessageAsync(peerUserId, "Не смог разобраться с этим, давай вернёмся к этому позже."); //TODO: лимитный fallback -> в настройки
        await runStorage.MarkFailedAsync(accountId, peerUserId);
    }

    private List<LlmToolDefinition> BuildToolDefinitions(bool clarificationsExhausted)
    {
        var defs = tools.Select(t => new LlmToolDefinition(t.Name, t.Description, t.ParametersJsonSchema)).ToList();

        if (!clarificationsExhausted)
        {
            defs.Add(new LlmToolDefinition(
                AskClarifyingToolName,
                "Задать собеседнику уточняющий вопрос прямо в чате, если для ответа не хватает данных, " +
                "которые не удалось найти инструментами поиска. Формулируй вопрос в стиле, уместном для этого собеседника.",
                """{"type":"object","properties":{"question":{"type":"string"}},"required":["question"]}"""));
        }

        return defs;
    }

    private static string ExtractClarifyingQuestion(string argsJson)
    {
        using var doc = JsonDocument.Parse(argsJson);
        return doc.RootElement.TryGetProperty("question", out var q) ? q.GetString() ?? "Уточни, пожалуйста." : "Уточни, пожалуйста."; //TODO: если вопрос был вызван, но без текста fallback -> в настройки
    }

    private async Task<string> BuildSystemPromptAsync(int accountId, ContactProfileDto profile, RuntimeSettingsDto settings, CancellationToken ct)
    {
        var sb = new StringBuilder();

        if (profile.Mode == AutoReplyMode.Ghost)
        {
            sb.AppendLine("""
                Ты отвечаешь ВМЕСТО пользователя, максимально имитируя его собственный стиль письма: длину сообщений,
                пунктуацию, обороты речи. Никогда не представляйся ассистентом.
                Если не хватает информации — задай уточняющий вопрос так, как задал бы сам пользователь: коротко,
                неформально, без канцелярита. Если данных всё ещё не хватает после уточнений — ответь в духе
                "не понял, объясни нормально", не выдумывай.
                """); //TODO: системный промпт для призрака -> в настройки

            var ownMessages = await messageStorage.SearchAsync(
                accountId, peerUserId: null, from: null, to: null, containsText: null,
                limit: settings.GhostStyleExampleCount * 3); // с запасом, дальше отфильтруем только исходящие //TODO: объём контекстных сообщений для few-shot -> в настройки
            
            var examples = ownMessages.Where(m => m.IsOutgoing).Select(m => m.Text) //TODO: few-shot (12) -> в настройки
                .Take(settings.GhostStyleExampleCount).ToList();

            if (examples.Count > 0)
            {
                sb.AppendLine("Примеры реальных сообщений пользователя (ориентируйся на их стиль):");
                foreach (var ex in examples) sb.AppendLine($"- {ex}"); 
            }
        }
        else
        {
            sb.AppendLine("""
                Ты — личный ассистент пользователя, отвечаешь его собеседнику в Telegram от его имени с его согласия.
                Отвечай по существу, вежливо и по делу. Если не хватает информации — задай уточняющий вопрос.
                Если данных всё ещё не хватает после уточнений — честно скажи, что не можешь ответить, и предложи
                переформулировать вопрос или дождаться самого пользователя.
                """); //TODO: системный промпт для агента -> в настройки
        }

        sb.AppendLine();
        sb.AppendLine("НИКОГДА не придумывай факты, даты или события, которых не находил через инструменты поиска.");
        sb.AppendLine("Если инструмент поиска ничего не вернул — так и скажи, не додумывай.");

        if (!string.IsNullOrWhiteSpace(profile.Notes))
            sb.AppendLine($"Заметки о собеседнике: {profile.Notes}");
        if (!string.IsNullOrWhiteSpace(profile.BehaviorProfile))
            sb.AppendLine($"Манера собеседника: {profile.BehaviorProfile}");
        if (!string.IsNullOrWhiteSpace(profile.AutoReplyInstructions))
            sb.AppendLine($"Доп. инструкция для автоответа: {profile.AutoReplyInstructions}");

        return sb.ToString();
    }

    private static string BuildInitialUserPrompt(
        IReadOnlyList<MessageDto> trigger, IReadOnlyList<MessageDto> history, ContactProfileDto profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Последние сообщения переписки:");
        foreach (var m in history)
            sb.AppendLine($"[{m.Time:yyyy-MM-dd HH:mm}] {(m.IsOutgoing ? "Я" : profile.DisplayName)}: {m.Text}");

        sb.AppendLine();
        sb.AppendLine("Новые сообщения, на которые нужно отреагировать:");
        foreach (var m in trigger)
            sb.AppendLine($"- {m.Text}");

        return sb.ToString();
    }
}