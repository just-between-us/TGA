using TGA.Domain.Enums;

namespace TGA.Domain.Entities;

public class AgentRun : Entity
{
    public int TelegramAccountId { get; set; }
    public long PeerUserId { get; set; }
    public AgentRunState State { get; set; }
    public int ClarificationCount { get; set; }

    public string MessagesJson { get; set; } = "[]";

    public AutoReplyMode Mode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}