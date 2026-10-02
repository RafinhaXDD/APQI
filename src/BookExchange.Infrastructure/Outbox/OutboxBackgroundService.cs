using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookExchange.Infrastructure.Outbox;

internal sealed partial class OutboxBackgroundService(
    OutboxProcessor processor,
    TimeProvider clock,
    IOptions<OutboxOptions> options,
    ILogger<OutboxBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        using var timer = new PeriodicTimer(settings.PollInterval, clock);
        try
        {
            do
            {
                try
                {
                    // Keep draining while full batches come back, then wait for the next tick.
                    while (await processor.ProcessPendingAsync(stoppingToken) == settings.BatchSize)
                    {
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    LogCycleFailed(logger, ex);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down.
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox processing cycle failed")]
    private static partial void LogCycleFailed(ILogger logger, Exception exception);
}
