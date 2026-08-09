namespace TGA.Contract.DTOs;

public record MessagePreviewDto(long Id, DateTime Date, string From, string Text, bool IsFromMe);  