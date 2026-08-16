namespace TGA.Contract.DTOs;

public record LogEntryDto(DateTime Timestamp, string Level, string Category, string Message, string? Exception);