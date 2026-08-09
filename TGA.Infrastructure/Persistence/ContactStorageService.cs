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

        var existing = await db.Contacts.FirstOrDefaultAsync(c =>
            c.TelegramAccountId == accountId && c.PeerUserId == peerUserId);

        if (existing is null)
        {
            db.Contacts.Add(new Contact
            {
                TelegramAccountId = accountId,
                PeerUserId = peerUserId,
                DisplayName = displayName,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else if (IsGenericName(existing.DisplayName) && !IsGenericName(displayName))
        {
            existing.DisplayName = displayName;
            existing.UpdatedAt = DateTime.UtcNow;
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

        var contact = await db.Contacts.FirstOrDefaultAsync(c =>
            c.TelegramAccountId == accountId && c.PeerUserId == peerUserId);

        if (contact is null)
        {
            db.Contacts.Add(new Contact
            {
                TelegramAccountId = accountId,
                PeerUserId = peerUserId,
                DisplayName = newName,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            contact.DisplayName = newName;
            contact.UpdatedAt = DateTime.UtcNow;
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