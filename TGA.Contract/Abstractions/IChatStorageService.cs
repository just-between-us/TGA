using TGA.Contract.DTOs;

namespace TGA.Contract.Abstractions;

public interface IChatStorageService
{
    Task<int> UpsertDialogAsync(int accountId, long peerId, long? topMessageId);
    Task<int> GetOrCreateChatIdAsync(int accountId, long peerId);
    Task MarkHistoryLoadedAsync(int chatId);
    Task<List<ChatSummaryDto>> GetChatSummariesAsync(int accountId);
    Task<ChatSummaryDto?> GetByPeerAsync(int accountId, long peerId);
}