using TGA.Contract.DTOs;
using TGA.Contract.DTOs.Llm;

namespace TGA.Contract.Abstractions;

public interface ITriageService
{
    Task<TriageResult> EvaluateAsync(
        long peerUserId,
        IReadOnlyList<MessageDto> pendingMessages,
        IReadOnlyList<MessageDto> recentHistory,
        ContactProfileDto? profile,
        CancellationToken ct = default);
}