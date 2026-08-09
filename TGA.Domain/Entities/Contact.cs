namespace TGA.Domain.Entities;

public class Contact : Entity
{
    public long PeerUserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int TelegramAccountId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? ChatId { get; set; }
    public Chat? Chat { get; set; }
}