using TGA.Contract.DTOs;

namespace TGA.Contract.Abstractions;

public interface IMessageStorageService
{
    Task<bool> AddMessageAsync(MessageDto message, int accountId);
    Task<List<MessageDto>> GetMessagesAsync(int accountId, string? contactName = null);
    Task<List<string>> GetContactsAsync(int accountId);
    Task ClearAsync(int accountId);
    Task<List<ChatSummaryDto>> GetChatSummariesAsync(int accountId);
    Task<List<MessageDto>> GetMessagesByPeerAsync(int accountId, long peerUserId);
    Task<string> GetExamplesAsync(int accountId, int totalLimit = 200);
}