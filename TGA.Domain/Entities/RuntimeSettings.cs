namespace TGA.Domain.Entities;

public class RuntimeSettings : Entity
{
    public int DebounceSeconds { get; set; } = 7;
    public int MaxWaitExtensions { get; set; } = 3;
    public int HistoryContextSize { get; set; } = 15;

    public int MaxClarifications { get; set; } = 2;
    public int MaxToolIterations { get; set; } = 6;
    public int GhostStyleExampleCount { get; set; } = 12;

    public DateTime UpdatedAt { get; set; }
}