using TGA.Contract.DTOs;

namespace TGA.Contract.Abstractions;

public interface ITelegramMessageService
{
    event Action<MessageDto>? OnNewMessageReceived;
    event Action<long>? OnUserTyping;

    Task<int> SyncDialogsAsync(bool loadAvatars = true);
    Task<List<MessageDto>> LoadChatHistoryAsync(long peerUserId, int limit, long offsetMessageId = 0);

    void StartMonitoring();
    void StopMonitoring();
    void SubscribeToTyping(long peerUserId);
    bool IsMonitoring { get; }

    Task<MessageDto> SendMessageAsync(long peerUserId, string text);
    Task<string?> ResolveUserDisplayNameAsync(long peerUserId);
    Task<int> SyncTelegramContactsAsync();
}