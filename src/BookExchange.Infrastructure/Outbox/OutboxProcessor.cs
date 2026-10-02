using BookExchange.Application.Shared.Outbox;
using BookExchange.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookExchange.Infrastructure.Outbox;

/// <summary>
/// Processes pending outbox messages one at a time. Each message is locked with
/// <c>FOR UPDATE SKIP LOCKED</c> inside its own transaction, so concurrent processors never handle
/// the same message twice. The handler's database writes and "processed" marker commit together;
/// a failing handler is rolled back to a savepoint and the failure is recorded with backoff.
/// </summary>
public sealed partial class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger)
{
    private const string HandlerSavepoint = "outbox_handler";

    /// <returns>Number of messages attempted (succeeded or failed) in this call.</returns>
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var attempted = 0;
        while (attempted < options.Value.BatchSize && await TryProcessNextAsync(cancellationToken))
        {
            attempted++;
        }

        return attempted;
    }

    private async Task<bool> TryProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var now = clock.GetUtcNow();
        var candidates = await db.OutboxMessages
            .FromSql($"""
                SELECT * FROM "OutboxMessages"
                WHERE "ProcessedAt" IS NULL AND "DeadLetteredAt" IS NULL AND "NextAttemptAt" <= {now}
                ORDER BY "NextAttemptAt"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        var message = candidates.SingleOrDefault();
        if (message is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var handler = scope.ServiceProvider.GetServices<IOutboxMessageHandler>()
            .FirstOrDefault(h => h.MessageType == message.Type);

        await transaction.CreateSavepointAsync(HandlerSavepoint, cancellationToken);
        try
        {
            if (handler is null)
            {
                throw new InvalidOperationException($"No outbox handler is registered for type '{message.Type}'.");
            }

            var envelope = new OutboxMessageEnvelope(message.Id, message.Type, message.Payload, message.Attempts + 1);
            await handler.HandleAsync(envelope, cancellationToken);

            message.MarkProcessed(clock.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackToSavepointAsync(HandlerSavepoint, cancellationToken);
            db.ChangeTracker.Clear();
            await RecordFailureAsync(db, message, ex, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
    }

    private async Task RecordFailureAsync(AppDbContext db, OutboxMessage message, Exception ex, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var attempts = message.Attempts + 1;
        var failedAt = clock.GetUtcNow();
        var deadLetteredAt = attempts >= settings.MaxAttempts ? failedAt : (DateTimeOffset?)null;
        var nextAttemptAt = failedAt + settings.BackoffFor(attempts);
        var error = $"{ex.GetType().Name}: {ex.Message}";
        if (error.Length > OutboxMessage.LastErrorMaxLength)
        {
            error = error[..OutboxMessage.LastErrorMaxLength];
        }

        await db.OutboxMessages
            .Where(m => m.Id == message.Id)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(m => m.Attempts, attempts)
                    .SetProperty(m => m.LastError, error)
                    .SetProperty(m => m.NextAttemptAt, nextAttemptAt)
                    .SetProperty(m => m.DeadLetteredAt, deadLetteredAt),
                cancellationToken);

        if (deadLetteredAt is null)
        {
            LogRetryScheduled(logger, ex, message.Id, message.Type, attempts, nextAttemptAt);
        }
        else
        {
            LogDeadLettered(logger, ex, message.Id, message.Type, attempts);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} ({MessageType}) failed on attempt {Attempt}; retry at {NextAttemptAt}")]
    private static partial void LogRetryScheduled(ILogger logger, Exception exception, Guid messageId, string messageType, int attempt, DateTimeOffset nextAttemptAt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} ({MessageType}) dead-lettered after {Attempt} attempts")]
    private static partial void LogDeadLettered(ILogger logger, Exception exception, Guid messageId, string messageType, int attempt);
}
