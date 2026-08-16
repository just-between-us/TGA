using TL;

namespace TGA.Infrastructure.Telegram;

public static class TelegramMessageTextFormatter
{
    public static string BuildDisplayText(Message message)
    {
        if (!string.IsNullOrWhiteSpace(message.message))
            return message.message.Trim();

        if (message.media is null)
            return "[сообщение без текста]";

        var mediaTypeName = message.media.GetType().Name;

        if (mediaTypeName.Contains("Video", StringComparison.OrdinalIgnoreCase))
            return "[видео]";

        if (mediaTypeName.Contains("Voice", StringComparison.OrdinalIgnoreCase))
            return "[голосовое сообщение]";

        if (mediaTypeName.Contains("Audio", StringComparison.OrdinalIgnoreCase))
            return "[аудио]";

        return message.media switch
        {
            MessageMediaPhoto => "[фото]",
            MessageMediaDocument document when document.document is Document documentFile &&
                                               documentFile.mime_type?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true =>
                message.grouped_id != 0 ? "[альбом фото]" : "[фото]",
            MessageMediaDocument document when document.document is Document documentFile &&
                                               documentFile.mime_type?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true =>
                "[видео]",
            MessageMediaDocument => "[файл]",
            MessageMediaGeo or MessageMediaGeoLive => "[геолокация]",
            MessageMediaContact => "[контакт]",
            MessageMediaWebPage => "[веб-страница]",
            MessageMediaPoll => "[опрос]",
            _ => $"[{mediaTypeName}]"
        };
    }
}