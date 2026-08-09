namespace TGA.Domain.Entities;

public class MessageRecord
{
    public int TelegramMessageId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime Time { get; set; }
    public bool IsOutgoing { get; set; }
    public long PeerUserId { get; set; }
    public int TelegramAccountId { get; set; }

    public int ChatId { get; set; }
    public Chat? Chat { get; set; }
}