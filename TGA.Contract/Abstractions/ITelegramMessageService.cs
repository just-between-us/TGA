using TGA.Contract.DTOs;

namespace TGA.Contract.Abstractions;

public interface ITelegramMessageService
{
    event Action<MessageDto>? OnNewMessageReceived;

    Task<int> SyncDialogsAsync();
    Task<List<MessageDto>> LoadChatHistoryAsync(long peerUserId, int limit, long offsetMessageId = 0);

    void StartMonitoring();
    void StopMonitoring();
    bool IsMonitoring { get; }

    Task<MessageDto> SendMessageAsync(long peerUserId, string text);
    Task<string?> ResolveUserDisplayNameAsync(long peerUserId);
    Task<int> SyncTelegramContactsAsync();
}