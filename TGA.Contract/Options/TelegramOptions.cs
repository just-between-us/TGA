namespace TGA.Contract.Options;

public class TelegramOptions
{
    public const string SectionName = "Telegram";
    public required string ApiId { get; set; }
    public required string ApiHash { get; set; }
}