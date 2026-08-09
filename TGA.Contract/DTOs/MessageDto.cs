namespace TGA.Contract.DTOs;

public record MessageDto(
    int Id,
    string ContactName,
    string Text,
    DateTime Time,
    bool IsOutgoing,
    long PeerUserId);