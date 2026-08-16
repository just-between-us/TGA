using Microsoft.Extensions.Logging;
using TL;
using WTelegram;

namespace TGA.Infrastructure.Telegram;

public class TelegramPeerResolver(TelegramPeerDirectory peerDirectory, ILogger<TelegramPeerResolver> logger)
{
    public async Task<InputPeer> ResolveInputPeerAsync(Client client, long peerUserId)
    {
        if (peerDirectory.TryGetPeer(peerUserId, out var cachedPeer))
            return cachedPeer;

        if (peerDirectory.TryGetUser(peerUserId, out var cachedUser) && cachedUser.access_hash != 0)
        {
            var resolvedPeer = new InputPeerUser(cachedUser.ID, cachedUser.access_hash);
            peerDirectory.RememberPeer(peerUserId, resolvedPeer);
            return resolvedPeer;
        }

        try
        {
            var users = await client.Users_GetUsers([new InputUser(peerUserId, 0)]);
            if (users.Length > 0 && users[0] is User user)
            {
                peerDirectory.RememberUser(peerUserId, user);
                if (user.access_hash != 0)
                {
                    var resolvedPeer = new InputPeerUser(user.ID, user.access_hash);
                    peerDirectory.RememberPeer(peerUserId, resolvedPeer);
                    return resolvedPeer;
                }

                logger.LogWarning(
                    "Пользователь {PeerUserId} найден, но access_hash равен 0. Пробуем fallback из последнего сообщения.",
                    peerUserId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось получить input peer для {PeerId}", peerUserId);
        }

        if (peerDirectory.TryGetMessagePeer(peerUserId, out var recentMessage) && recentMessage.peer_id is PeerUser)
        {
            var peerHash = 0L;
            if (peerDirectory.TryGetUser(peerUserId, out var cachedUserForPeer) && cachedUserForPeer.access_hash != 0)
                peerHash = cachedUserForPeer.access_hash;

            var messagePeer = new InputPeerUserFromMessage
            {
                peer = new InputPeerUser(peerUserId, peerHash),
                msg_id = (int)recentMessage.id,
                user_id = peerUserId
            };

            peerDirectory.RememberPeer(peerUserId, messagePeer);
            logger.LogInformation(
                "Использую InputPeerUserFromMessage для {PeerUserId} из сообщения {MessageId} с accessHash={AccessHash}",
                peerUserId, recentMessage.id, peerHash);
            return messagePeer;
        }

        return new InputPeerUser(peerUserId, 0);
    }

    public static bool IsPeerInvalid(Exception ex) =>
        ex.Message.Contains("PEER_ID_INVALID", StringComparison.OrdinalIgnoreCase);

    public static (long UserId, long AccessHash, string PeerKind) DescribePeer(InputPeer peer) => peer switch
    {
        InputPeerUser p => (p.user_id, p.access_hash, nameof(InputPeerUser)),
        InputPeerUserFromMessage p => (p.user_id, 0, nameof(InputPeerUserFromMessage)),
        _ => (0, 0, peer.GetType().Name)
    };
}