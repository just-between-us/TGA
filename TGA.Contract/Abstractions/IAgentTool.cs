namespace TGA.Contract.Abstractions;

public record AgentToolContext(int TelegramAccountId, long PeerUserId);

public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    string ParametersJsonSchema { get; }
    Task<string> ExecuteAsync(AgentToolContext context, string argumentsJson, CancellationToken ct);
}