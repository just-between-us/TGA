namespace TGA.Contract.DTOs;

public record AccountDto(
    int Id,
    long TelegramUserId,
    string DisplayName,
    string? PhoneNumber,
    byte[]? AvatarData,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt);