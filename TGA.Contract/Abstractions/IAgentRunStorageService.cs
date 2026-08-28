using TGA.Contract.DTOs.Llm;
using TGA.Domain.Enums;

namespace TGA.Contract.Abstractions;

public interface IAgentRunStorageService
{
    Task<AgentRunDto?> GetActiveRunAsync(int accountId, long peerUserId);

    Task<int> UpsertAsync(
        int accountId, long peerUserId, AgentRunState state,
        int clarificationCount, List<LlmChatMessage> messages, AutoReplyMode mode);

    Task MarkCompletedAsync(int accountId, long peerUserId);
    Task MarkFailedAsync(int accountId, long peerUserId);
    
    Task CancelAsync(int accountId, long peerUserId); // владелец вмешался вручную
}