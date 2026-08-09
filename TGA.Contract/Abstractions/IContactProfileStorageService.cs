using TGA.Contract.DTOs;

namespace TGA.Contract.Abstractions;

public interface IContactProfileStorageService
{
    Task<List<ContactProfileDto>> GetAllAsync(int accountId);
    Task<ContactProfileDto?> GetByPeerAsync(int accountId, long peerUserId);

    Task SaveAsync(
        int accountId,
        long peerUserId,
        string? notes,
        string? behaviorProfile,
        string? communicationStyle,
        bool autoReplyEnabled,
        string? autoReplyInstructions);
}