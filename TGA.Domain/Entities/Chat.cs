namespace TGA.Domain.Entities;

public class Chat : Entity
{
    public int TelegramAccountId { get; set; }

    public long PeerId { get; set; }
    
    public long? TopMessageId { get; set; }    
    public string? TopMessageText { get; set; } 
    public DateTime? TopMessageTime { get; set; }
    public bool? TopMessageIsOutgoing { get; set; }

    public string PeerType { get; set; } = "User";

    public DateTime? LastSyncedAt { get; set; }

    public bool HistoryLoaded { get; set; }

    public Contact? Contact { get; set; }
    public ContactProfile? ContactProfile { get; set; }
    public List<MessageRecord> Messages { get; set; } = [];
}