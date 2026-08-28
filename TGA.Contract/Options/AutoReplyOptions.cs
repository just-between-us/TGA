namespace TGA.Contract.Options;

public class AutoReplyOptions
{
    
    public const string SectionName = "AutoReply";
    public int DebounceSeconds { get; set; } = 7;
    public int MaxWaitExtensions { get; set; } = 3;
    public int HistoryContextSize { get; set; } = 15;
}