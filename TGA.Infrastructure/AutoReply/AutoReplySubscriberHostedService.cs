using Microsoft.Extensions.Hosting;
using TGA.Contract.Abstractions;

namespace TGA.Infrastructure.AutoReply;


public class AutoReplySubscriberHostedService(
    ITelegramMessageService messageService,
    AutoReplyDebounceService debounceService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        messageService.OnNewMessageReceived += debounceService.OnMessageReceived;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        messageService.OnNewMessageReceived -= debounceService.OnMessageReceived;
        return Task.CompletedTask;
    }
}