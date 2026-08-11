using Microsoft.EntityFrameworkCore;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;
using TGA.Domain.Entities;

namespace TGA.Infrastructure.Persistence;

public class ChatStorageService(IDbContextFactory<AppDbContext> dbFactory) : IChatStorageService
{
    public async Task<int> UpsertDialogAsync(int accountId, long peerId, long? topMessageId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var chat = await db.Chats.FirstOrDefaultAsync(c =>
            c.TelegramAccountId == accountId && c.PeerId == peerId);

        if (chat is null)
        {
            chat = new Chat
            {
                TelegramAccountId = accountId,
                PeerId = peerId,
                PeerType = "User",
                TopMessageId = topMessageId,
                LastSyncedAt = DateTime.UtcNow
            };
            db.Chats.Add(chat);
        }
        else
        {
            chat.TopMessageId = topMessageId ?? chat.TopMessageId;
            chat.LastSyncedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return chat.Id;
    }

    public async Task<int> GetOrCreateChatIdAsync(int accountId, long peerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var existingId = await db.Chats
            .Where(c => c.TelegramAccountId == accountId && c.PeerId == peerId)
            .Select(c => c.Id)
            .FirstOrDefaultAsync();

        if (existingId != 0) return existingId;

        var chat = new Chat { TelegramAccountId = accountId, PeerId = peerId, PeerType = "User" };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();
        return chat.Id;
    }

    public async Task MarkHistoryLoadedAsync(int chatId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Chats.Where(c => c.Id == chatId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.HistoryLoaded, true));
    }

    public async Task<List<ChatSummaryDto>> GetChatSummariesAsync(int accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var chats = await db.Chats
            .Where(c => c.TelegramAccountId == accountId)
            .Include(c => c.Contact)
            .ToListAsync();

        var lastMessages = await db.Messages
            .Where(m => m.TelegramAccountId == accountId)
            .GroupBy(m => m.ChatId)
            .Select(g => new
            {
                ChatId = g.Key,
                Count = g.Count(),
                Last = g.OrderByDescending(m => m.Time).First()
            })
            .ToDictionaryAsync(x => x.ChatId);

        return chats
            .Select(c =>
            {
                var displayName = c.Contact?.DisplayName ?? $"User {c.PeerId}";

                if (lastMessages.TryGetValue(c.Id, out var info))
                {
                    return new ChatSummaryDto(
                        c.Id, c.PeerId, displayName,
                        info.Last.Text, info.Last.Time, info.Last.IsOutgoing,
                        info.Count, c.Contact is not null);
                }

                if (c.TopMessageText is not null)
                {
                    return new ChatSummaryDto(
                        c.Id, c.PeerId, displayName,
                        c.TopMessageText, c.TopMessageTime ?? c.LastSyncedAt ?? DateTime.MinValue,
                        c.TopMessageIsOutgoing ?? false,
                        0, c.Contact is not null);
                }

                return new ChatSummaryDto(
                    c.Id, c.PeerId, displayName,
                    "Нажмите, чтобы загрузить историю", c.LastSyncedAt ?? DateTime.MinValue, false,
                    0, c.Contact is not null);
            })
            .OrderByDescending(c => c.LastMessageTime)
            .ToList();
    }

    public async Task<ChatSummaryDto?> GetByPeerAsync(int accountId, long peerId)
    {
        var all = await GetChatSummariesAsync(accountId);
        return all.FirstOrDefault(c => c.PeerUserId == peerId);
    }
    
    public async Task<int> UpsertDialogAsync(
        int accountId, long peerId, long? topMessageId,
        string? topMessageText, DateTime? topMessageTime, bool? topMessageIsOutgoing)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var chat = await db.Chats.FirstOrDefaultAsync(c =>
            c.TelegramAccountId == accountId && c.PeerId == peerId);

        if (chat is null)
        {
            chat = new Chat
            {
                TelegramAccountId = accountId,
                PeerId = peerId,
                PeerType = "User",
                TopMessageId = topMessageId,
                TopMessageText = topMessageText,
                TopMessageTime = topMessageTime,
                TopMessageIsOutgoing = topMessageIsOutgoing,
                LastSyncedAt = DateTime.UtcNow
            };
            db.Chats.Add(chat);
        }
        else
        {
            chat.TopMessageId = topMessageId ?? chat.TopMessageId;
            if (topMessageText is not null)
            {
                chat.TopMessageText = topMessageText;
                chat.TopMessageTime = topMessageTime;
                chat.TopMessageIsOutgoing = topMessageIsOutgoing;
            }
            chat.LastSyncedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return chat.Id;
    }
    
}