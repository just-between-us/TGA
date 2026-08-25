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
    IOptions<AutoReplyOptions> options,
    ILogger<AutoReplyDebounceService> logger)
{
    private readonly ConcurrentDictionary<long, PendingBuffer> _buffers = new();

    public void OnMessageReceived(MessageDto message)
    {
        if (message.IsOutgoing)
        {
            // владелец аккаунта уже отвечает сам вручную — автоответ для этого чата больше не нужен
            if (_buffers.TryRemove(message.PeerUserId, out var existing))
                existing.Cts.Cancel();
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
                await Task.Delay(TimeSpan.FromSeconds(options.Value.DebounceSeconds), cts.Token);
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

        var history = await messageStorage.GetMessagesByPeerAsync(active.Id, peerUserId);
        var recentHistory = history.OrderBy(m => m.Time).TakeLast(options.Value.HistoryContextSize).ToList();

        var pending = buffer.Messages.ToList();
        var result = await triageService.EvaluateAsync(peerUserId, pending, recentHistory, profile, ct);

        logger.LogInformation(
            "Триаж peer={PeerId} ({Name}): action={Action}, reason={Reason}, сообщений в буфере={Count}",
            peerUserId, profile.DisplayName, result.Action, result.Reason, pending.Count);

        switch (result.Action)
        {
            case TriageAction.Reply:
                
                // TODO: здесь подключится ReAct-агент и реальная отправка
                
                _buffers.TryRemove(peerUserId, out _);
                break;

            case TriageAction.Wait when buffer.WaitExtensions < options.Value.MaxWaitExtensions:
                buffer.WaitExtensions++;
                ScheduleFlush(peerUserId, buffer);
                break;

            default: // Wait с исчерпанным лимитом или Skip — не зацикливаемся
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