
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;
using TGA.Infrastructure.Import.Dto;

namespace TGA.Infrastructure.Import;

public class ExportImportService(
    IMessageStorageService messageStorage,
    IContactStorageService contactStorage,
    IAccountStorageService accountStorage,
    ILogger<ExportImportService> logger) : IExportImportService
{
    public async Task<List<ChatPreviewDto>> ParseAsync(string filePath, int accountId, CancellationToken ct = default)
    {
        var account = await accountStorage.GetByIdAsync(accountId)
            ?? throw new InvalidOperationException("Аккаунт для импорта не найден");

        var json = await File.ReadAllTextAsync(filePath, ct);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        var chats = ReadChats(document.RootElement, options);

        var result = new List<ChatPreviewDto>();

        foreach (var chat in chats)
        {
            ct.ThrowIfCancellationRequested();

            var chatName = string.IsNullOrWhiteSpace(chat.Name) ? $"User {chat.Id}" : chat.Name!;

            var messages = (chat.Messages ?? [])
                .Select(m => new
                {
                    Raw = m,
                    Text = CleanText(BuildMessageText(m))
                })
                .Where(m => m.Raw.Id > 0 && m.Raw.Date is not null && !string.IsNullOrWhiteSpace(m.Text))
                .Select(m => new MessagePreviewDto(
                    m.Raw.Id,
                    m.Raw.Date!.Value,
                    string.IsNullOrWhiteSpace(m.Raw.From) ? chatName : m.Raw.From!,
                    m.Text,
                    IsFromMe(m.Raw.FromId, m.Raw.From, account.TelegramUserId)))
                .OrderBy(m => m.Date)
                .ToList();

            result.Add(new ChatPreviewDto(chat.Id, chatName, chat.Type, IsPersonalChat(chat.Type), messages));
        }

        logger.LogInformation("Разобрано {Count} чатов из {File}", result.Count, filePath);
        return result;
    }

    public async Task<ImportSummaryDto> ImportAsync(
        List<ChatPreviewDto> chats, int accountId, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var personalChats = chats.Where(c => c.IsPersonalChat).ToList();
        int imported = 0, skipped = 0;

        foreach (var chat in personalChats)
        {
            ct.ThrowIfCancellationRequested();

            await contactStorage.UpsertAsync(accountId, chat.Id, chat.Name);
            progress?.Report($"Импорт чата: {chat.Name}");

            foreach (var msg in chat.Messages)
            {
                if (IsSystemMessage(msg.Text))
                {
                    skipped++;
                    continue;
                }

                var dto = new MessageDto(
                    (int)msg.Id,
                    msg.IsFromMe ? "Я" : chat.Name,
                    msg.Text,
                    msg.Date,
                    msg.IsFromMe,
                    chat.Id);

                var wasInserted = await messageStorage.AddMessageAsync(dto, accountId);
                if (wasInserted) imported++; else skipped++;
            }
        }

        logger.LogInformation("Импорт завершён: {Chats} чатов, {Imported} сообщений, {Skipped} пропущено",
            personalChats.Count, imported, skipped);

        return new ImportSummaryDto(personalChats.Count, imported, skipped);
    }

    private static bool IsFromMe(string? fromId, string? from, long myTelegramUserId)
    {
        if (!string.IsNullOrWhiteSpace(fromId))
        {
            var digits = Regex.Match(fromId, @"\d+").Value;
            if (long.TryParse(digits, out var parsedId))
                return parsedId == myTelegramUserId;
        }

        return from is not null && (from.Equals("Me", StringComparison.OrdinalIgnoreCase) ||
                                    from.Equals("Saved Messages", StringComparison.OrdinalIgnoreCase) ||
                                    from.Equals("Избранное", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSystemMessage(string text) =>
        text.Contains("присоединился") || text.Contains("покинул") ||
        text.Contains("изменил") || text.Contains("удалил");

    private static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        text = Regex.Replace(text, "<.*?>", string.Empty);
        text = text.Replace("\r\n", " ").Replace("\n", " ");

        while (text.Contains("  "))
            text = text.Replace("  ", " ");

        return text.Trim();
    }

    private static List<TelegramChatDto> ReadChats(JsonElement root, JsonSerializerOptions options)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Корень файла экспорта должен быть JSON-объектом.");

        if (TryGetPropertyIgnoreCase(root, "chats", out var chatsElement) &&
            chatsElement.ValueKind == JsonValueKind.Object &&
            TryGetPropertyIgnoreCase(chatsElement, "list", out var listElement) &&
            listElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<TelegramChatDto>>(listElement.GetRawText(), options) ?? [];
        }

        if (TryGetPropertyIgnoreCase(root, "messages", out var messagesElement) &&
            messagesElement.ValueKind == JsonValueKind.Array)
        {
            var chat = JsonSerializer.Deserialize<TelegramChatDto>(root.GetRawText(), options);
            return chat is null ? [] : [chat];
        }

        throw new InvalidOperationException(
            "Файл не похож на экспорт Telegram: не найден chats.list или messages.");
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool IsPersonalChat(string? type) => type?.ToLowerInvariant() is
        "personal_chat" or "saved_messages";

    private static string BuildMessageText(TelegramMessageDto message)
    {
        if (!string.IsNullOrWhiteSpace(message.Text)) return message.Text;

        var media = new[]
        {
            message.Photo is not null ? $"Фото: {message.Photo}" : null,
            message.Video is not null ? $"Видео: {message.Video}" : null,
            message.Audio is not null ? $"Аудио: {message.Audio}" : null,
            message.File is not null ? $"Файл: {message.File}" : null,
            message.VoiceMessage ? "Голосовое сообщение" : null
        }.Where(value => value is not null);

        return string.Join("; ", media!);
    }
}