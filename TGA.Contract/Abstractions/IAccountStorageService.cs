using TGA.Contract.DTOs;

namespace TGA.Contract.Abstractions;

public interface IAccountStorageService
{
    Task<List<AccountDto>> GetAllAccountsAsync();
    Task<AccountDto?> GetActiveAccountAsync();
    Task<int> SaveAccountAsync(long telegramUserId, string displayName, string? phone, byte[] sessionData);
    Task SetActiveAsync(int accountId);
    Task DeleteAsync(int accountId);
    Task<byte[]?> GetSessionDataAsync(int accountId);
    Task<AccountDto?> GetByIdAsync(int accountId);
    Task UpdateSessionDataAsync(int accountId, byte[] sessionData);
    Task UpdateAvatarAsync(int accountId, byte[]? avatarData);
}