using System.Text.Json.Serialization;

namespace TGA.Infrastructure.Import.Dto;

public class TelegramMessageDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("from_id")]
    public string? FromId { get; set; }              // формат: "user123456789"

    [JsonPropertyName("text")]
    [JsonConverter(typeof(TelegramTextJsonConverter))]
    public string Text { get; set; } = string.Empty;
}