namespace TGA.Contract.DTOs;

public record MessageStatisticsSourceDto(
    DateTime Time,
    bool IsOutgoing,
    long PeerUserId,
    string ContactName);
