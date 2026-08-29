namespace TGA.Contract.DTOs;

public record RuntimeSettingsDto(
    int DebounceSeconds,
    int MaxWaitExtensions,
    int HistoryContextSize,
    int MaxClarifications,
    int MaxToolIterations,
    int GhostStyleExampleCount);