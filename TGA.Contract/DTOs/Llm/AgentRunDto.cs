using TGA.Domain.Enums;

namespace TGA.Contract.DTOs.Llm;

public record AgentRunDto(
    int Id, int TelegramAccountId, long PeerUserId, AgentRunState State,
    int ClarificationCount, List<LlmChatMessage> Messages, AutoReplyMode Mode,
    DateTime CreatedAt, DateTime UpdatedAt);