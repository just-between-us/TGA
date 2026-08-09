using System.Text.Json.Serialization;

namespace TGA.Infrastructure.Import.Dto;

public class TelegramChatListDto
{
    [JsonPropertyName("list")]
    public List<TelegramChatDto> List { get; set; } = [];
}