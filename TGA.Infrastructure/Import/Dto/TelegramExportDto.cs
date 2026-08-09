using System.Text.Json.Serialization;

namespace TGA.Infrastructure.Import.Dto;

public class TelegramExportDto
{
    [JsonPropertyName("about")]
    public string About { get; set; } = string.Empty;

    [JsonPropertyName("chats")]
    public TelegramChatListDto Chats { get; set; } = new();
}