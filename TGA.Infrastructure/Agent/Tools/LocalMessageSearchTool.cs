using System.Text.Json;
using TGA.Contract.Abstractions;

namespace TGA.Infrastructure.Agent.Tools;

public class LocalMessageSearchTool(IMessageStorageService storage) : IAgentTool
{
    public string Name => "search_local_messages";
    public string Description =>
        "Ищет сообщения в уже сохранённой локальной истории переписки по тексту и/или диапазону дат. " +
        "chat_scope='this_chat' — искать только в текущем чате с этим собеседником, 'all_chats' — по всем чатам аккаунта.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "query": {"type": "string", "description": "подстрока для поиска в тексте, необязательно"},
            "date_from": {"type": "string", "description": "ISO 8601 дата начала диапазона, необязательно"},
            "date_to": {"type": "string", "description": "ISO 8601 дата конца диапазона, необязательно"},
            "chat_scope": {"type": "string", "enum": ["this_chat", "all_chats"]}
          },
          "required": ["chat_scope"]
        }
        """;

    public async Task<string> ExecuteAsync(AgentToolContext context, string argumentsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(argumentsJson);
        var root = doc.RootElement;

        var query = root.TryGetProperty("query", out var q) ? q.GetString() : null;
        var from = root.TryGetProperty("date_from", out var f) && DateTime.TryParse(f.GetString(), out var fd) ? fd : (DateTime?)null;
        var to = root.TryGetProperty("date_to", out var t) && DateTime.TryParse(t.GetString(), out var td) ? td : (DateTime?)null;
        var scope = root.TryGetProperty("chat_scope", out var s) ? s.GetString() : "this_chat";

        var peerFilter = scope == "all_chats" ? (long?)null : context.PeerUserId;
        var results = await storage.SearchAsync(context.TelegramAccountId, peerFilter, from, to, query, limit: 30);

        if (results.Count == 0) return "Ничего не найдено в локальной истории.";

        return string.Join("\n", results.Select(m =>
            $"[{m.Time:yyyy-MM-dd HH:mm}] {(m.IsOutgoing ? "Я" : m.ContactName)}: {m.Text}"));
    }
}