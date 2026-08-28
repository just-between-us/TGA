using TGA.Domain.Enums;

namespace TGA.Contract.DTOs.Llm;

public record LlmRoleAssignmentDto(LlmUsageRole Role, int? ProviderSettingsId, string? ProviderSettingsName);