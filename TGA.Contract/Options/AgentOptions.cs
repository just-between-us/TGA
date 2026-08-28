namespace TGA.Contract.Options;

public class AgentOptions
{
    public const string SectionName = "Agent";
    public int MaxClarifications { get; set; } = 2;
    public int MaxToolIterations { get; set; } = 6; 
    public int GhostStyleExampleCount { get; set; } = 12;
}