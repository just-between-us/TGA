namespace TGA.Contract.DTOs;

public record ChatSummaryDto(
    int ChatId,
    long PeerUserId,
    string DisplayName,
    byte[]? AvatarData,
    string LastMessageText,
    DateTime LastMessageTime,
    bool LastMessageIsOutgoing,
    int MessageCount,
    bool HasContact); 