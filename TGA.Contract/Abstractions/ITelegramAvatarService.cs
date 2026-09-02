namespace TGA.Contract.Abstractions;

public interface ITelegramAvatarService
{
    Task RefreshAsync(int accountId, long telegramUserId, long accessHash);
    Task RefreshContactAsync(int accountId, long telegramUserId, long accessHash);
}
