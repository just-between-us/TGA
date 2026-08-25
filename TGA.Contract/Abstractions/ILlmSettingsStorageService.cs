using TGA.Contract.DTOs.Llm;
using TGA.Domain.Enums;

namespace TGA.Contract.Abstractions;

public interface ILlmSettingsStorageService
{
    Task<List<LlmProviderSettingsDto>> GetAllAsync();
    Task<LlmProviderSettingsDto?> GetActiveAsync();

    Task<int> SaveAsync(
        string name, LlmProvider provider, string baseUrl, string apiKey,
        string model, string? systemPrompt, double temperature);

    Task SetActiveAsync(int id);
    Task DeleteAsync(int id);
}