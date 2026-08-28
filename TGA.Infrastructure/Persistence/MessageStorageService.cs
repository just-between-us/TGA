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

        var chatId = await db.Chats
            .Where(c => c.TelegramAccountId == accountId && c.PeerId == message.PeerUserId)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();

        if (chatId == 0)
        {
            var chat = new Chat { TelegramAccountId = accountId, PeerId = message.PeerUserId, PeerType = "User" };
            db.Chats.Add(chat);
            await db.SaveChangesAsync();
            chatId = chat.Id;
        }

        var exists = await db.Messages.AnyAsync(m => m.ChatId == chatId && m.TelegramMessageId == message.Id);
        if (exists) return false;

        db.Messages.Add(new MessageRecord
        {
            TelegramAccountId = accountId,
            ChatId = chatId,
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
    public async Task<List<MessageDto>> SearchAsync(
        int accountId, long? peerUserId, DateTime? from, DateTime? to, string? containsText, int limit)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Messages.Where(m => m.TelegramAccountId == accountId);

        if (peerUserId is { } pid) query = query.Where(m => m.PeerUserId == pid);
        if (from is { } f) query = query.Where(m => m.Time >= f);
        if (to is { } t) query = query.Where(m => m.Time <= t);
        if (!string.IsNullOrWhiteSpace(containsText)) query = query.Where(m => m.Text.Contains(containsText));

        var results = await query.OrderByDescending(m => m.Time).Take(limit).ToListAsync();
        return results.Select(m => new MessageDto(
            m.TelegramMessageId, m.ContactName, m.Text, m.Time, m.IsOutgoing, m.PeerUserId)).ToList();
    }
}