using TL;

namespace TGA.Infrastructure.Telegram;

public class TelegramPeerDirectory
{
    private readonly Dictionary<long, User> _userCache = new();
    private readonly Dictionary<long, InputPeer> _peerCache = new();
    private readonly Dictionary<long, Message> _messagePeerCache = new();
    private readonly Dictionary<long, long> _dialogTopMessageCache = new();

    public long CurrentUserId { get; set; }

    public bool TryGetUser(long peerUserId, out User user) => _userCache.TryGetValue(peerUserId, out user!);
    public void RememberUser(long peerUserId, User user) => _userCache[peerUserId] = user;

    public bool TryGetPeer(long peerUserId, out InputPeer peer) => _peerCache.TryGetValue(peerUserId, out peer!);
    public void RememberPeer(long peerUserId, InputPeer peer) => _peerCache[peerUserId] = peer;

    public bool TryGetMessagePeer(long peerUserId, out Message message) => _messagePeerCache.TryGetValue(peerUserId, out message!);
    public void RememberMessagePeer(long peerUserId, Message message) => _messagePeerCache[peerUserId] = message;

    public void RememberDialogTopMessage(long peerUserId, long topMessageId) => _dialogTopMessageCache[peerUserId] = topMessageId;

    public static string DisplayName(User user)
    {
        var full = $"{user.first_name} {user.last_name}".Trim();
        if (!string.IsNullOrEmpty(full)) return full;
        return !string.IsNullOrEmpty(user.username) ? $"@{user.username}" : $"User {user.ID}";
    }
}