using Microsoft.Extensions.Logging;
using TGA.Contract.Abstractions;
using TL;
using WTelegram;

namespace TGA.Infrastructure.Telegram;

public class TelegramAvatarService(
    TelegramClientFactory clientFactory,
    IAccountStorageService accountStorage,
    ILogger<TelegramAvatarService> logger) : ITelegramAvatarService
{
    public async Task RefreshAsync(int accountId, long telegramUserId, long accessHash)
    {
        try
        {
            var client = clientFactory.GetCurrent();
            var photos = await client.Photos_GetUserPhotos(
                user_id: new InputUser(telegramUserId, accessHash),
                offset: 0,
                max_id: 0,
                limit: 1);

            if (photos is not Photos_Photos photosResult || photosResult.photos.Length == 0)
            {
                await accountStorage.UpdateAvatarAsync(accountId, null);
                return;
            }

            if (photosResult.photos[0] is not Photo photo)
            {
                return;
            }

            await using var memoryStream = new MemoryStream();
            await client.DownloadFileAsync(photo, memoryStream);
            await accountStorage.UpdateAvatarAsync(accountId, memoryStream.ToArray());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось загрузить аватар Telegram для аккаунта {AccountId}", accountId);
        }
    }
}
