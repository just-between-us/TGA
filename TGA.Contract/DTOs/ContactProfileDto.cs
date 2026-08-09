namespace TGA.Contract.DTOs;

public record ContactProfileDto(
    long PeerUserId,
    string DisplayName,
    string? Notes,
    string? BehaviorProfile,
    string? CommunicationStyle,
    bool AutoReplyEnabled,
    string? AutoReplyInstructions,
    DateTime? UpdatedAt);