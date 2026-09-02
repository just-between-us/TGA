using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TL;
using WTelegram;

namespace TGA.Infrastructure.Telegram;

public class TelegramAvatarService(
    TelegramClientFactory clientFactory,
    IAccountStorageService accountStorage,
    IContactStorageService contactStorage,
    ILogger<TelegramAvatarService> logger) : ITelegramAvatarService
{
    public async Task RefreshAsync(int accountId, long telegramUserId, long accessHash)
    {
        try
        {
            var client = clientFactory.GetCurrent();
            var avatarData = await DownloadUserAvatarAsync(client, telegramUserId, accessHash);
            await accountStorage.UpdateAvatarAsync(accountId, avatarData);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось загрузить аватар Telegram для аккаунта {AccountId}", accountId);
        }
    }

    public async Task RefreshContactAsync(int accountId, long telegramUserId, long accessHash)
    {
        try
        {
            var client = clientFactory.GetCurrent();
            var avatarData = await DownloadUserAvatarAsync(client, telegramUserId, accessHash);
            await contactStorage.UpdateAvatarAsync(accountId, telegramUserId, avatarData);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Не удалось загрузить аватар Telegram для контакта {PeerUserId} аккаунта {AccountId}",
                telegramUserId, accountId);
        }
    }

    private static async Task<byte[]?> DownloadUserAvatarAsync(Client client, long telegramUserId, long accessHash)
    {
        var photos = await client.Photos_GetUserPhotos(
            user_id: new InputUser(telegramUserId, accessHash),
            offset: 0,
            max_id: 0,
            limit: 1);

        if (photos is not Photos_Photos photosResult || photosResult.photos.Length == 0)
            return null;

        if (photosResult.photos[0] is not Photo photo)
            return null;

        await using var memoryStream = new MemoryStream();
        await client.DownloadFileAsync(photo, memoryStream);
        return memoryStream.ToArray();
    }
}
