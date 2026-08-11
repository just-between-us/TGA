using Microsoft.EntityFrameworkCore;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;
using TGA.Domain.Entities;

namespace TGA.Infrastructure.Persistence;

public class ContactStorageService(IDbContextFactory<AppDbContext> dbFactory) : IContactStorageService
{
    public async Task UpsertAsync(int accountId, long peerUserId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return;

        await using var db = await dbFactory.CreateDbContextAsync();

        var chat = await db.Chats.FirstOrDefaultAsync(c =>
            c.TelegramAccountId == accountId && c.PeerId == peerUserId);

        if (chat is null)
        {
            chat = new Chat { TelegramAccountId = accountId, PeerId = peerUserId, PeerType = "User" };
            db.Chats.Add(chat);
            await db.SaveChangesAsync(); // нужен Id чата для привязки
        }

        var existing = await db.Contacts.FirstOrDefaultAsync(c =>
            c.TelegramAccountId == accountId && c.PeerUserId == peerUserId);

        if (existing is null)
        {
            db.Contacts.Add(new Contact
            {
                TelegramAccountId = accountId,
                PeerUserId = peerUserId,
                DisplayName = displayName,
                ChatId = chat.Id,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.ChatId ??= chat.Id;
            if (IsGenericName(existing.DisplayName) && !IsGenericName(displayName))
            {
                existing.DisplayName = displayName;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task<List<ContactDto>> GetAllAsync(int accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Contacts
            .Where(c => c.TelegramAccountId == accountId)
            .OrderBy(c => c.DisplayName)
            .Select(c => new ContactDto(c.PeerUserId, c.DisplayName))
            .ToListAsync();
    }
    public async Task RenameAsync(int accountId, long peerUserId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;

        await using var db = await dbFactory.CreateDbContextAsync();

        var chat = await db.Chats.FirstOrDefaultAsync(c =>
            c.TelegramAccountId == accountId && c.PeerId == peerUserId);

        if (chat is null)
        {
            chat = new Chat { TelegramAccountId = accountId, PeerId = peerUserId, PeerType = "User" };
            db.Chats.Add(chat);
            await db.SaveChangesAsync(); 
        }

        var contact = await db.Contacts.FirstOrDefaultAsync(c =>
            c.TelegramAccountId == accountId && c.PeerUserId == peerUserId);

        if (contact is null)
        {
            db.Contacts.Add(new Contact
            {
                PeerUserId = peerUserId,
                DisplayName = newName,
                TelegramAccountId = accountId,
                UpdatedAt = DateTime.UtcNow,
                ChatId = chat.Id,
                Chat = chat   
            });
        }
        else
        {
            contact.DisplayName = newName;
            contact.UpdatedAt = DateTime.UtcNow;
            contact.ChatId ??= chat.Id;
        }

        await db.SaveChangesAsync();
    }

    public async Task<string?> GetDisplayNameAsync(int accountId, long peerUserId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Contacts
            .Where(c => c.TelegramAccountId == accountId && c.PeerUserId == peerUserId)
            .Select(c => c.DisplayName)
            .FirstOrDefaultAsync();
    }

    private static bool IsGenericName(string name) => name.StartsWith("User ");
}