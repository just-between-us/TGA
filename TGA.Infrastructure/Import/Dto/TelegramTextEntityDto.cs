using System.Text.Json.Serialization;

namespace TGA.Infrastructure.Import.Dto;

public class TelegramTextEntityDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}