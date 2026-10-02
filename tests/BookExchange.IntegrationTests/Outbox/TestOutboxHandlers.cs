using System.Collections.Concurrent;
using BookExchange.Application.Shared.Outbox;
using BookExchange.Infrastructure.Persistence;

namespace BookExchange.IntegrationTests.Outbox;

/// <summary>Shared record of what the test handlers saw (registered as a singleton).</summary>
public sealed class HandledMessages
{
    public ConcurrentQueue<OutboxMessageEnvelope> All { get; } = new();

    public void Clear() => All.Clear();
}

public sealed class SucceedingHandler(HandledMessages handled) : IOutboxMessageHandler
{
    public const string Type = "test.succeeds";

    public string MessageType => Type;

    public async Task HandleAsync(OutboxMessageEnvelope message, CancellationToken cancellationToken)
    {
        // Small delay widens the window for concurrent processors to collide if locking were broken.
        await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
        handled.All.Enqueue(message);
    }
}

public sealed class FailingHandler(HandledMessages handled) : IOutboxMessageHandler
{
    public const string Type = "test.fails";

    public string MessageType => Type;

    public Task HandleAsync(OutboxMessageEnvelope message, CancellationToken cancellationToken)
    {
        handled.All.Enqueue(message);
        throw new InvalidOperationException("boom");
    }
}

/// <summary>Writes to the database, saves, then fails: the write must be rolled back.</summary>
public sealed class WritesThenFailsHandler(IOutbox outbox, AppDbContext db) : IOutboxMessageHandler
{
    public const string Type = "test.writes-then-fails";

    public string MessageType => Type;

    public async Task HandleAsync(OutboxMessageEnvelope message, CancellationToken cancellationToken)
    {
        outbox.Enqueue(SucceedingHandler.Type, new { CreatedBy = message.Id });
        await db.SaveChangesAsync(cancellationToken);
        throw new InvalidOperationException("failed after writing");
    }
}
