namespace TGA.Domain.Entities;

public class Chat : Entity
{
    public int TelegramAccountId { get; set; }

    public long PeerId { get; set; }

    public string PeerType { get; set; } = "User";

    public long? TopMessageId { get; set; }    
    public DateTime? LastSyncedAt { get; set; }

    public bool HistoryLoaded { get; set; }

    public Contact? Contact { get; set; }
    public ContactProfile? ContactProfile { get; set; }
    public List<MessageRecord> Messages { get; set; } = [];
}