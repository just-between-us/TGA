using Microsoft.EntityFrameworkCore;
using System.Text;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;
using TGA.Domain.Entities;


namespace TGA.Infrastructure.Persistence;

public class MessageStorageService(IDbContextFactory<AppDbContext> dbFactory) : IMessageStorageService
{
    public async Task<bool> AddMessageAsync(MessageDto message, int accountId)
    {
        if (string.IsNullOrEmpty(message.Text)) return false;

        await using var db = await dbFactory.CreateDbContextAsync();

        var exists = await db.Messages.AnyAsync(m =>
            m.TelegramAccountId == accountId &&
            m.TelegramMessageId == message.Id &&
            m.PeerUserId == message.PeerUserId);

        if (exists) return false;

        db.Messages.Add(new MessageRecord
        {
            TelegramAccountId = accountId,
            TelegramMessageId = message.Id,
            ContactName = message.ContactName,
            Text = message.Text,
            Time = message.Time,
            IsOutgoing = message.IsOutgoing,
            PeerUserId = message.PeerUserId
        });

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<List<MessageDto>> GetMessagesAsync(int accountId, string? contactName = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var query = db.Messages.Where(m => m.TelegramAccountId == accountId);
        if (!string.IsNullOrEmpty(contactName))
            query = query.Where(m => EF.Functions.Like(m.ContactName, $"%{contactName}%"));

        var records = await query.OrderByDescending(m => m.Time).ToListAsync();

        return records.Select(m => new MessageDto(
            m.TelegramMessageId, m.ContactName, m.Text, m.Time, m.IsOutgoing, m.PeerUserId)).ToList();
    }

    public async Task<List<string>> GetContactsAsync(int accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Messages
            .Where(m => m.TelegramAccountId == accountId)
            .Select(m => m.ContactName)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    public async Task ClearAsync(int accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var toRemove = db.Messages.Where(m => m.TelegramAccountId == accountId);
        db.Messages.RemoveRange(toRemove);
        await db.SaveChangesAsync();
    }

    public async Task<string> GetExamplesAsync(int accountId, int totalLimit = 200)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var sorted = await db.Messages
            .Where(m => m.TelegramAccountId == accountId)
            .OrderByDescending(m => m.Time)
            .Take(totalLimit)
            .ToListAsync();
        sorted.Reverse();

        var sb = new StringBuilder();
        foreach (var m in sorted.Where(m => m.ContactName != "Telegram" && !m.Text.StartsWith('!')))
            sb.AppendLine($"{m.ContactName}: {m.Text}");

        return sb.ToString();
    }
    
    
    public async Task<List<ChatSummaryDto>> GetChatSummariesAsync(int accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var contacts = await db.Contacts
            .Where(c => c.TelegramAccountId == accountId)
            .ToListAsync();

        var lastMessages = await db.Messages
            .Where(m => m.TelegramAccountId == accountId)
            .GroupBy(m => m.PeerUserId)
            .Select(g => new
            {
                PeerUserId = g.Key,
                Count = g.Count(),
                Last = g.OrderByDescending(m => m.Time).First()
            })
            .ToDictionaryAsync(x => x.PeerUserId);

        return contacts
            .Select(c =>
            {
                lastMessages.TryGetValue(c.PeerUserId, out var info);
                return new ChatSummaryDto(
                    c.PeerUserId,
                    c.DisplayName,
                    info?.Last.Text ?? "Нажмите, чтобы загрузить историю",
                    info?.Last.Time ?? DateTime.MinValue,
                    info?.Last.IsOutgoing ?? false,
                    info?.Count ?? 0);
            })
            .OrderByDescending(c => c.LastMessageTime)
            .ToList();
    }

    public async Task<List<MessageDto>> GetMessagesByPeerAsync(int accountId, long peerUserId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var records = await db.Messages
            .Where(m => m.TelegramAccountId == accountId && m.PeerUserId == peerUserId)
            .OrderBy(m => m.Time)
            .ToListAsync();

        return records.Select(m => new MessageDto(
            m.TelegramMessageId, m.ContactName, m.Text, m.Time, m.IsOutgoing, m.PeerUserId)).ToList();
    }
}