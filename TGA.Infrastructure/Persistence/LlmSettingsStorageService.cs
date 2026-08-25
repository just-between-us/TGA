using System.Text;
using Microsoft.EntityFrameworkCore;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs.Llm;
using TGA.Domain.Entities;
using TGA.Domain.Enums;

namespace TGA.Infrastructure.Persistence;

public class LlmSettingsStorageService(
    IDbContextFactory<AppDbContext> dbFactory,
    ISessionEncryptor encryptor) : ILlmSettingsStorageService
{
    public async Task<List<LlmProviderSettingsDto>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var entities = await db.LlmProviderSettings
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();

        return entities.Select(ToDto).ToList();
    }

    public async Task<LlmProviderSettingsDto?> GetActiveAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var active = await db.LlmProviderSettings.FirstOrDefaultAsync(s => s.IsActive);
        return active is null ? null : ToDto(active);
    }

    public async Task<int> SaveAsync(
        string name, LlmProvider provider, string baseUrl, string apiKey,
        string model, string? systemPrompt, double temperature)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var existing = await db.LlmProviderSettings.FirstOrDefaultAsync(s => s.Name == name);
        var encrypted = encryptor.Encrypt(Encoding.UTF8.GetBytes(apiKey));

        // активным может быть только один профиль — тот же принцип, что у аккаунтов
        await db.LlmProviderSettings.Where(s => s.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));

        if (existing is not null)
        {
            existing.Provider = provider;
            existing.BaseUrl = baseUrl;
            existing.ApiKeyEncrypted = encrypted;
            existing.Model = model;
            existing.SystemPrompt = systemPrompt;
            existing.Temperature = temperature;
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return existing.Id;
        }

        var entity = new LlmProviderSettings
        {
            Name = name,
            Provider = provider,
            BaseUrl = baseUrl,
            ApiKeyEncrypted = encrypted,
            Model = model,
            SystemPrompt = systemPrompt,
            Temperature = temperature,
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        };
        db.LlmProviderSettings.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    public async Task SetActiveAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.LlmProviderSettings.Where(s => s.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        await db.LlmProviderSettings.Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true));
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.LlmProviderSettings.Where(s => s.Id == id).ExecuteDeleteAsync();
    }

    private LlmProviderSettingsDto ToDto(LlmProviderSettings e) => new(
        e.Id, e.Name, e.Provider, e.BaseUrl,
        Encoding.UTF8.GetString(encryptor.Decrypt(e.ApiKeyEncrypted)),
        e.Model, e.SystemPrompt, e.Temperature, e.IsActive, e.UpdatedAt);
}