using System.Text.Json;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;
using TGA.Infrastructure.Telegram;
using TL;

namespace TGA.Infrastructure.Agent.Tools;

public class RemoteMessageSearchTool(
    TelegramClientFactory clientFactory,
    TelegramPeerResolver peerResolver,
    IAccountStorageService accountStorage,
    IMessageStorageService messageStorage,
    TelegramContactResolver contactResolver) : IAgentTool
{
    public string Name => "search_telegram_remote";
    public string Description =>
        "Ищет сообщения напрямую на серверах Telegram по тексту в текущем чате с этим собеседником. " +
        "Используй, ТОЛЬКО если search_local_messages ничего не нашёл, а сообщения могут быть старше локальной истории.";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "query": {"type": "string", "description": "текст для поиска"}
          },
          "required": ["query"]
        }
        """;

    public async Task<string> ExecuteAsync(AgentToolContext context, string argumentsJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(argumentsJson);
        var query = doc.RootElement.GetProperty("query").GetString() ?? "";

        var client = clientFactory.GetCurrent();
        var inputPeer = await peerResolver.ResolveInputPeerAsync(client, context.PeerUserId);
        var result = await client.Messages_Search(inputPeer, query, limit: 30);

        var active = await accountStorage.GetActiveAccountAsync();
        if (active is null) return "Нет активного аккаунта.";

        var imported = new List<string>();
        foreach (var messageBase in result.Messages)
        {
            if (messageBase is not Message message) continue;
            if (message.peer_id is not PeerUser peerUser) continue;

            var isOutgoing = message.flags.HasFlag(Message.Flags.out_);
            var contactName = await contactResolver.GetContactNameAsync(client, peerUser.user_id);
            var text = TelegramMessageTextFormatter.BuildDisplayText(message);

            var dto = new MessageDto(message.id, contactName, text, message.Date.ToLocalTime(), isOutgoing, peerUser.user_id);
            await messageStorage.AddMessageAsync(dto, active.Id); // дедуп по (ChatId, TelegramMessageId), как и в остальном коде
            imported.Add($"[{dto.Time:yyyy-MM-dd HH:mm}] {(dto.IsOutgoing ? "Я" : dto.ContactName)}: {dto.Text}");
        }

        return imported.Count == 0
            ? "На серверах Telegram по этому запросу ничего не найдено."
            : string.Join("\n", imported);
    }
}