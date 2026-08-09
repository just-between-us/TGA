
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

        var export = JsonSerializer.Deserialize<TelegramExportDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        }) ?? throw new InvalidOperationException("Не удалось разобрать файл экспорта");

        var result = new List<ChatPreviewDto>();

        foreach (var chat in export.Chats.List)
        {
            ct.ThrowIfCancellationRequested();

            var chatName = string.IsNullOrWhiteSpace(chat.Name) ? $"User {chat.Id}" : chat.Name!;

            var messages = chat.Messages
                .Select(m => new
                {
                    Raw = m,
                    Text = CleanText(m.Text)
                })
                .Where(m => !string.IsNullOrWhiteSpace(m.Text))
                .Select(m => new MessagePreviewDto(
                    m.Raw.Id,
                    m.Raw.Date,
                    string.IsNullOrWhiteSpace(m.Raw.From) ? chatName : m.Raw.From!,
                    m.Text,
                    IsFromMe(m.Raw.FromId, account.TelegramUserId)))
                .OrderBy(m => m.Date)
                .ToList();

            result.Add(new ChatPreviewDto(chat.Id, chatName, chat.Type, chat.IsPersonalChat, messages));
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

    private static bool IsFromMe(string? fromId, long myTelegramUserId)
    {
        if (string.IsNullOrEmpty(fromId)) return false;
        var digits = Regex.Match(fromId, @"\d+").Value;
        return long.TryParse(digits, out var parsedId) && parsedId == myTelegramUserId;
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
}