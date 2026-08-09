namespace TGA.Contract.DTOs;

public record ChatPreviewDto(
    long Id,
    string Name,
    string Type,
    bool IsPersonalChat,
    List<MessagePreviewDto> Messages)
{
    public int MyMessagesCount => Messages.Count(m => m.IsFromMe);
    public int OtherMessagesCount => Messages.Count(m => !m.IsFromMe);
}