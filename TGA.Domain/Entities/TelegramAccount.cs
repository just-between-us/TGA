namespace TGA.Domain.Entities;

public class TelegramAccount : Entity
{
    public long TelegramUserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public byte[] SessionData { get; set; } = [];   
    public byte[]? AvatarData { get; set; }
    public bool IsActive { get; set; }               
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}