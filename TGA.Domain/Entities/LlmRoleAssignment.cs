using TGA.Domain.Enums;

namespace TGA.Domain.Entities;

public class LlmRoleAssignment : Entity
{
    public LlmUsageRole Role { get; set; }
    public int? LlmProviderSettingsId { get; set; }
    public LlmProviderSettings? LlmProviderSettings { get; set; }
}