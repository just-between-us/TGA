using Microsoft.EntityFrameworkCore;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;
using TGA.Domain.Entities;

namespace TGA.Infrastructure.Persistence;

public class ContactProfileStorageService(IDbContextFactory<AppDbContext> dbFactory) : IContactProfileStorageService
{
    public async Task<List<ContactProfileDto>> GetAllAsync(int accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var query =
            from contact in db.Contacts
            where contact.TelegramAccountId == accountId
            join profile in db.ContactProfiles on contact.Id equals profile.ContactId into profiles
            from profile in profiles.DefaultIfEmpty()
            orderby contact.DisplayName
            select new ContactProfileDto(
                contact.PeerUserId,
                contact.DisplayName,
                profile != null ? profile.Notes : null,
                profile != null ? profile.BehaviorProfile : null,
                profile != null ? profile.CommunicationStyle : null,
                profile != null && profile.AutoReplyEnabled,
                profile != null ? profile.AutoReplyInstructions : null,
                profile != null ? profile.UpdatedAt : (DateTime?)null);

        return await query.ToListAsync();
    }

    public async Task<ContactProfileDto?> GetByPeerAsync(int accountId, long peerUserId)
    {
        var all = await GetAllAsync(accountId); 
        return all.FirstOrDefault(c => c.PeerUserId == peerUserId);
    }

    public async Task SaveAsync(
        int accountId, long peerUserId,
        string? notes, string? behaviorProfile, string? communicationStyle,
        bool autoReplyEnabled, string? autoReplyInstructions)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var contact = await db.Contacts.FirstOrDefaultAsync(c =>
            c.TelegramAccountId == accountId && c.PeerUserId == peerUserId)
            ?? throw new InvalidOperationException("Контакт не найден — сначала синхронизируйте контакты");

        var profile = await db.ContactProfiles.FirstOrDefaultAsync(p => p.ContactId == contact.Id);

        if (profile is null)
        {
            profile = new ContactProfile { ContactId = contact.Id };
            db.ContactProfiles.Add(profile);
        }

        profile.Notes = notes;
        profile.BehaviorProfile = behaviorProfile;
        profile.CommunicationStyle = communicationStyle;
        profile.AutoReplyEnabled = autoReplyEnabled;
        profile.AutoReplyInstructions = autoReplyInstructions;
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }
}