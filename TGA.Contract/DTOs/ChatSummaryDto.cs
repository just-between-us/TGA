namespace TGA.Contract.DTOs;

public record ChatSummaryDto(
    long PeerUserId,
    string DisplayName,
    string LastMessageText,
    DateTime LastMessageTime,
    bool LastMessageIsOutgoing,
    int MessageCount);