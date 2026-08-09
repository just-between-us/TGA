using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TGA.Infrastructure.Import.Dto;

namespace TGA.Infrastructure.Import;

public class TelegramTextJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString() ?? string.Empty;

        if (reader.TokenType != JsonTokenType.StartArray)
            return string.Empty;

        var sb = new StringBuilder();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                sb.Append(reader.GetString());
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                var entity = JsonSerializer.Deserialize<TelegramTextEntityDto>(ref reader, options);
                if (entity is not null)
                    sb.Append(entity.Text);
            }
        }

        return sb.ToString();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}