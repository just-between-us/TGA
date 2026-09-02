using TGA.Domain.Enums;

namespace TGA.Contract.DTOs;

public record ContactProfileDto(
    long PeerUserId,
    string DisplayName,
    byte[]? AvatarData,
    string? Notes,
    string? BehaviorProfile,
    string? CommunicationStyle,
    bool AutoReplyEnabled,
    string? AutoReplyInstructions,
    AutoReplyMode Mode,
    DateTime? UpdatedAt);