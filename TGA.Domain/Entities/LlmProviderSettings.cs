using TGA.Domain.Enums;

namespace TGA.Domain.Entities;

public class LlmProviderSettings : Entity
{
    public string Name { get; set; } = "Default";
    public LlmProvider Provider { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public byte[] ApiKeyEncrypted { get; set; } = [];
    public string Model { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public double Temperature { get; set; } = 0.7;
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}