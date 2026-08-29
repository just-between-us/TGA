using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TGA.Contract.Abstractions;
using TGA.Contract.DTOs;
using TGA.Contract.Options;
using TGA.Domain.Enums;

namespace TGA.Infrastructure.AutoReply;

public class AutoReplyDebounceService(
    IAccountStorageService accountStorage,
    IContactProfileStorageService profileStorage,
    IMessageStorageService messageStorage,
    ITriageService triageService,
    IAgentService agentService,
    IAgentRunStorageService agentRunStorage,
    IRuntimeSettingsStorageService settingsStorage, 
    ILogger<AutoReplyDebounceService> logger)
{
    private readonly ConcurrentDictionary<long, PendingBuffer> _buffers = new();

    public void OnMessageReceived(MessageDto message)
    {
        if (message.IsOutgoing)
        {
            if (_buffers.TryRemove(message.PeerUserId, out var existing))
                existing.Cts.Cancel();

            // владелец ответил сам — если агент чего-то ждал по этому чату, снимаем ожидание
            _ = Task.Run(async () =>
            {
                var active = await accountStorage.GetActiveAccountAsync();
                if (active is not null) await agentRunStorage.CancelAsync(active.Id, message.PeerUserId);
            });
            return;
        }

        var buffer = _buffers.AddOrUpdate(
            message.PeerUserId,
            _ => new PendingBuffer([message]),
            (_, existing) =>
            {
                existing.Cts.Cancel();
                existing.Messages.Add(message);
                return existing;
            });

        ScheduleFlush(message.PeerUserId, buffer);
    }

    private void ScheduleFlush(long peerUserId, PendingBuffer buffer)
    {
        var cts = new CancellationTokenSource();
        buffer.Cts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                var settings = await settingsStorage.GetAsync();
                await Task.Delay(TimeSpan.FromSeconds(settings.DebounceSeconds), cts.Token);
                await FlushAsync(peerUserId, cts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug("Флаш для peer={PeerId} отменён — подоспело новое сообщение", peerUserId);
            }
        });
    }

    private async Task FlushAsync(long peerUserId, CancellationToken ct)
    {
        if (!_buffers.TryGetValue(peerUserId, out var buffer)) return;

        var active = await accountStorage.GetActiveAccountAsync();
        if (active is null) { _buffers.TryRemove(peerUserId, out _); return; }

        var profile = await profileStorage.GetByPeerAsync(active.Id, peerUserId);
        if (profile is null || !profile.AutoReplyEnabled)
        {
            _buffers.TryRemove(peerUserId, out _);
            return;
        }

        var pending = buffer.Messages.ToList();

        var activeRun = await agentRunStorage.GetActiveRunAsync(active.Id, peerUserId);
        if (activeRun is { State: AgentRunState.WaitingClarification })
        {
            _buffers.TryRemove(peerUserId, out _);
            await agentService.ResumeAsync(active.Id, peerUserId, pending, ct);
            return;
        }
        
        var settings = await settingsStorage.GetAsync();

        var history = await messageStorage.GetMessagesByPeerAsync(active.Id, peerUserId);
        var recentHistory = history.OrderBy(m => m.Time).TakeLast(settings.HistoryContextSize).ToList();

        var result = await triageService.EvaluateAsync(peerUserId, pending, recentHistory, profile, ct);

        logger.LogInformation(
            "Триаж peer={PeerId} ({Name}): action={Action}, reason={Reason}, сообщений в буфере={Count}",
            peerUserId, profile.DisplayName, result.Action, result.Reason, pending.Count);

        switch (result.Action)
        {
            case TriageAction.Reply:
                _buffers.TryRemove(peerUserId, out _);
                await agentService.StartAsync(active.Id, peerUserId, pending, recentHistory, profile, ct);
                break;

            case TriageAction.Wait when buffer.WaitExtensions < settings.MaxWaitExtensions:
                buffer.WaitExtensions++;
                ScheduleFlush(peerUserId, buffer);
                break;

            default:
                _buffers.TryRemove(peerUserId, out _);
                break;
        }
    }

    private class PendingBuffer(List<MessageDto> initial)
    {
        public List<MessageDto> Messages { get; } = initial;
        public CancellationTokenSource Cts { get; set; } = new();
        public int WaitExtensions { get; set; }
    }
}