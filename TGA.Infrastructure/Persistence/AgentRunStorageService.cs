using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs.Llm;
using TGA.Domain.Entities;
using TGA.Domain.Enums;

namespace TGA.Infrastructure.Persistence;

public class AgentRunStorageService(IDbContextFactory<AppDbContext> dbFactory) : IAgentRunStorageService
{
    public async Task<AgentRunDto?> GetActiveRunAsync(int accountId, long peerUserId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var run = await db.AgentRuns.FirstOrDefaultAsync(r =>
            r.TelegramAccountId == accountId && r.PeerUserId == peerUserId &&
            (r.State == AgentRunState.Running || r.State == AgentRunState.WaitingClarification));

        return run is null ? null : ToDto(run);
    }

    public async Task<int> UpsertAsync(
        int accountId, long peerUserId, AgentRunState state,
        int clarificationCount, List<LlmChatMessage> messages, AutoReplyMode mode)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.AgentRuns.FirstOrDefaultAsync(r =>
            r.TelegramAccountId == accountId && r.PeerUserId == peerUserId);

        var messagesJson = JsonSerializer.Serialize(messages);

        if (existing is not null)
        {
            existing.State = state;
            existing.ClarificationCount = clarificationCount;
            existing.MessagesJson = messagesJson;
            existing.Mode = mode;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return existing.Id;
        }

        var entity = new AgentRun
        {
            TelegramAccountId = accountId,
            PeerUserId = peerUserId,
            State = state,
            ClarificationCount = clarificationCount,
            MessagesJson = messagesJson,
            Mode = mode,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.AgentRuns.Add(entity);
        await db.SaveChangesAsync();
        return entity.Id;
    }

    public Task MarkCompletedAsync(int accountId, long peerUserId) => SetStateAsync(accountId, peerUserId, AgentRunState.Completed);
    public Task MarkFailedAsync(int accountId, long peerUserId) => SetStateAsync(accountId, peerUserId, AgentRunState.Failed);
    public Task CancelAsync(int accountId, long peerUserId) => SetStateAsync(accountId, peerUserId, AgentRunState.Completed);

    private async Task SetStateAsync(int accountId, long peerUserId, AgentRunState state)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.AgentRuns.Where(r => r.TelegramAccountId == accountId && r.PeerUserId == peerUserId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.State, state)
                .SetProperty(r => r.UpdatedAt, DateTime.UtcNow));
    }

    private static AgentRunDto ToDto(AgentRun r) => new(
        r.Id, r.TelegramAccountId, r.PeerUserId, r.State, r.ClarificationCount,
        JsonSerializer.Deserialize<List<LlmChatMessage>>(r.MessagesJson) ?? [],
        r.Mode, r.CreatedAt, r.UpdatedAt);
}