using Microsoft.EntityFrameworkCore;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;

namespace TGA.Infrastructure.Persistence;


public class RuntimeSettingsStorageService(IDbContextFactory<AppDbContext> dbFactory) : IRuntimeSettingsStorageService
{
    public async Task<RuntimeSettingsDto> GetAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var entity = await db.RuntimeSettings.FirstOrDefaultAsync();
        if (entity is null)
        {
            entity = new Domain.Entities.RuntimeSettings { UpdatedAt = DateTime.UtcNow };
            db.RuntimeSettings.Add(entity);
            await db.SaveChangesAsync();
        }

        return ToDto(entity);
    }

    public async Task SaveAsync(
        int debounceSeconds, int maxWaitExtensions, int historyContextSize,
        int maxClarifications, int maxToolIterations, int ghostStyleExampleCount)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var entity = await db.RuntimeSettings.FirstOrDefaultAsync();
        if (entity is null)
        {
            entity = new Domain.Entities.RuntimeSettings();
            db.RuntimeSettings.Add(entity);
        }

        entity.DebounceSeconds = debounceSeconds;
        entity.MaxWaitExtensions = maxWaitExtensions;
        entity.HistoryContextSize = historyContextSize;
        entity.MaxClarifications = maxClarifications;
        entity.MaxToolIterations = maxToolIterations;
        entity.GhostStyleExampleCount = ghostStyleExampleCount;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    private static RuntimeSettingsDto ToDto(Domain.Entities.RuntimeSettings e) => new(
        e.DebounceSeconds, e.MaxWaitExtensions, e.HistoryContextSize,
        e.MaxClarifications, e.MaxToolIterations, e.GhostStyleExampleCount);
}