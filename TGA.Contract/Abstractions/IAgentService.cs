using TGA.Contract.DTOs;

namespace TGA.Contract.Abstractions;

public interface IAgentService
{
    Task StartAsync(
        int accountId, long peerUserId, IReadOnlyList<MessageDto> triggerMessages,
        IReadOnlyList<MessageDto> recentHistory, ContactProfileDto profile, CancellationToken ct = default);

    Task ResumeAsync(
        int accountId, long peerUserId, IReadOnlyList<MessageDto> newMessages, CancellationToken ct = default);
}