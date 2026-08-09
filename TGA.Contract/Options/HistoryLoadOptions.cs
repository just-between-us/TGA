namespace TGA.Contract.Options;

public class HistoryLoadOptions
{
    
    public const string SectionName = "HistoryLoad";
    public int DefaultMessagesPerDialog { get; set; } = 5;
    public int MaxMessagesPerDialog { get; set; } = 100;
}