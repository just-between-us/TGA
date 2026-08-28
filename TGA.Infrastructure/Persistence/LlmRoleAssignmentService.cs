using Microsoft.EntityFrameworkCore;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs.Llm;
using TGA.Domain.Entities;
using TGA.Domain.Enums;

namespace TGA.Infrastructure.Persistence;

public class LlmRoleAssignmentService(
    IDbContextFactory<AppDbContext> dbFactory,
    ILlmSettingsStorageService settingsStorage) : ILlmRoleAssignmentService
{
    public async Task<List<LlmRoleAssignmentDto>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var assignments = await db.LlmRoleAssignments
            .Include(a => a.LlmProviderSettings)
            .ToListAsync();

        return Enum.GetValues<LlmUsageRole>()
            .Select(role =>
            {
                var a = assignments.FirstOrDefault(x => x.Role == role);
                return new LlmRoleAssignmentDto(role, a?.LlmProviderSettingsId, a?.LlmProviderSettings?.Name);
            })
            .ToList();
    }

    public async Task<LlmProviderSettingsDto?> ResolveAsync(LlmUsageRole role)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var assignment = await db.LlmRoleAssignments.FirstOrDefaultAsync(a => a.Role == role);

        if (assignment?.LlmProviderSettingsId is not { } profileId)
            return await settingsStorage.GetActiveAsync();

        var all = await settingsStorage.GetAllAsync();
        return all.FirstOrDefault(s => s.Id == profileId) ?? await settingsStorage.GetActiveAsync();
    }

    public async Task AssignAsync(LlmUsageRole role, int providerSettingsId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.LlmRoleAssignments.FirstOrDefaultAsync(a => a.Role == role);

        if (existing is not null)
        {
            existing.LlmProviderSettingsId = providerSettingsId;
        }
        else
        {
            db.LlmRoleAssignments.Add(new LlmRoleAssignment { Role = role, LlmProviderSettingsId = providerSettingsId });
        }

        await db.SaveChangesAsync();
    }

    public async Task ClearAsync(LlmUsageRole role)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.LlmRoleAssignments.Where(a => a.Role == role).ExecuteDeleteAsync();
    }
}