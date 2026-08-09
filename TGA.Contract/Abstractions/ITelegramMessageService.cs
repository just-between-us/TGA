using TGA.Contract.DTOs;

namespace TGA.Contract.Abstractions;

public interface ITelegramMessageService
{
    event Action<MessageDto>? OnNewMessageReceived;

    Task LoadRecentPersonalMessagesAsync(int messagesPerDialog);
    Task<int> SyncDialogsAsync();
    Task<List<MessageDto>> LoadChatHistoryAsync(long peerUserId, int limit);
    void StartMonitoring();
    void StopMonitoring();
    bool IsMonitoring { get; }
    Task<string?> ResolveUserDisplayNameAsync(long peerUserId);
    Task<int> SyncContactsAsync(); 
    Task<MessageDto> SendMessageAsync(long peerUserId, string text);
}