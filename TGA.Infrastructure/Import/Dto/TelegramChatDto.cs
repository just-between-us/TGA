using System.Text.Json.Serialization;

namespace TGA.Infrastructure.Import.Dto;

public class TelegramChatDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }              

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("messages")]
    public List<TelegramMessageDto>? Messages { get; set; }

    [JsonIgnore]
    public bool IsPersonalChat => Type is "personal_chat" or "saved_messages";
}