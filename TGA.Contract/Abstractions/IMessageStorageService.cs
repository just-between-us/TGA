using TGA.Contract.DTOs;

namespace TGA.Contract.Abstractions;

public interface IMessageStorageService
{
    Task<bool> AddMessageAsync(MessageDto message, int accountId);
    Task<List<MessageDto>> GetMessagesAsync(int accountId, string? contactName = null);
    Task<List<MessageDto>> GetMessagesByPeerAsync(int accountId, long peerUserId);
    Task ClearAsync(int accountId);
    Task<string> GetExamplesAsync(int accountId, int totalLimit = 200);
    Task<List<MessageDto>> SearchAsync(
        int accountId, long? peerUserId, DateTime? from, DateTime? to, string? containsText, int limit);
}