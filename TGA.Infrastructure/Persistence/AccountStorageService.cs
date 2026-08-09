using Microsoft.EntityFrameworkCore;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;
using TGA.Domain.Entities;

namespace TGA.Infrastructure.Persistence;

public class AccountStorageService(
    IDbContextFactory<AppDbContext> dbFactory,
    ISessionEncryptor encryptor) : IAccountStorageService
{
    public async Task<List<AccountDto>> GetAllAccountsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Accounts
            .OrderByDescending(a => a.LastLoginAt)
            .Select(a => new AccountDto(a.Id, a.TelegramUserId, a.DisplayName, a.PhoneNumber, a.IsActive, a.LastLoginAt))
            .ToListAsync();
    }

    public async Task<AccountDto?> GetActiveAccountAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var active = await db.Accounts.FirstOrDefaultAsync(a => a.IsActive);
        return active is null ? null
            : new AccountDto(active.Id, active.TelegramUserId, active.DisplayName, active.PhoneNumber, active.IsActive, active.LastLoginAt);
    }

    public async Task<int> SaveAccountAsync(long telegramUserId, string displayName, string? phone, byte[] sessionData)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var existing = await db.Accounts.FirstOrDefaultAsync(a => a.TelegramUserId == telegramUserId);
        var encrypted = encryptor.Encrypt(sessionData);

        // деактивируем все остальные — активным может быть только один аккаунт
        await db.Accounts.Where(a => a.IsActive).ExecuteUpdateAsync(s => s.SetProperty(a => a.IsActive, false));

        if (existing is not null)
        {
            existing.SessionData = encrypted;
            existing.DisplayName = displayName;
            existing.PhoneNumber = phone;
            existing.IsActive = true;
            existing.LastLoginAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return existing.Id;
        }

        var account = new TelegramAccount
        {
            TelegramUserId = telegramUserId,
            DisplayName = displayName,
            PhoneNumber = phone,
            SessionData = encrypted,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    public async Task SetActiveAsync(int accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Accounts.Where(a => a.IsActive).ExecuteUpdateAsync(s => s.SetProperty(a => a.IsActive, false));
        await db.Accounts.Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.IsActive, true)
                .SetProperty(a => a.LastLoginAt, DateTime.UtcNow));
    }
    public async Task<AccountDto?> GetByIdAsync(int accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var acc = await db.Accounts.FindAsync(accountId);
        return acc is null ? null
            : new AccountDto(acc.Id, acc.TelegramUserId, acc.DisplayName, acc.PhoneNumber, acc.IsActive, acc.LastLoginAt);
    }
    public async Task DeleteAsync(int accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Accounts.Where(a => a.Id == accountId).ExecuteDeleteAsync();
        await db.Messages.Where(m => m.TelegramAccountId == accountId).ExecuteDeleteAsync();
    }

    public async Task<byte[]?> GetSessionDataAsync(int accountId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var account = await db.Accounts.FindAsync(accountId);
        return account is null ? null : encryptor.Decrypt(account.SessionData);
    }
}