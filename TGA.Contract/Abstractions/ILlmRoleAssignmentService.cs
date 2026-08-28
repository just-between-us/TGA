using TGA.Contract.DTOs.Llm;
using TGA.Domain.Enums;

namespace TGA.Contract.Abstractions;

public interface ILlmRoleAssignmentService
{
    Task<List<LlmRoleAssignmentDto>> GetAllAsync();

    Task<LlmProviderSettingsDto?> ResolveAsync(LlmUsageRole role);

    Task AssignAsync(LlmUsageRole role, int providerSettingsId);
    Task ClearAsync(LlmUsageRole role);
}