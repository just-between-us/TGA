namespace TGA.Contract.DTOs;

public record AccountDto(
    int Id,
    long TelegramUserId,
    string DisplayName,
    string? PhoneNumber,
    bool IsActive,
    DateTime? LastLoginAt);