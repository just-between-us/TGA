using TGA.Contract.DTOs;

namespace TGA.Contract.Abstractions;

public interface IRuntimeSettingsStorageService
{
    Task<RuntimeSettingsDto> GetAsync();

    Task SaveAsync(
        int debounceSeconds, int maxWaitExtensions, int historyContextSize,
        int maxClarifications, int maxToolIterations, int ghostStyleExampleCount);
}