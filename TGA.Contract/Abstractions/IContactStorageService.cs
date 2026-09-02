using TGA.Contract.DTOs;

namespace TGA.Contract.Abstractions;

public interface IContactStorageService
{
    Task UpsertAsync(int accountId, long peerUserId, string displayName);
    Task RenameAsync(int accountId, long peerUserId, string newName);
    Task<List<ContactDto>> GetAllAsync(int accountId);
    Task UpdateAvatarAsync(int accountId, long peerUserId, byte[]? avatarData);
    Task<string?> GetDisplayNameAsync(int accountId, long peerUserId);
    Task DeleteAsync(int accountId, long peerUserId);
}