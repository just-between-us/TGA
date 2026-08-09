namespace TGA.Domain.Entities;


public class ContactProfile : Entity
{
    public int ContactId { get; set; }
    public Contact? Contact { get; set; }

    public int? ChatId { get; set; }
    public Chat? Chat { get; set; }

    public string? Notes { get; set; }
    public string? BehaviorProfile { get; set; }
    public string? CommunicationStyle { get; set; }

    public bool AutoReplyEnabled { get; set; }
    public string? AutoReplyInstructions { get; set; }

    public DateTime UpdatedAt { get; set; }
}