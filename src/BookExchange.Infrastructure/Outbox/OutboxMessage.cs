namespace BookExchange.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public const int TypeMaxLength = 200;
    public const int LastErrorMaxLength = 2000;

    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = null!;

    /// <summary>JSON payload (jsonb).</summary>
    public string Payload { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Earliest time the processor may pick the message up (backoff after failures).</summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>Set when the message failed <c>MaxAttempts</c> times; it is never retried again.</summary>
    public DateTimeOffset? DeadLetteredAt { get; private set; }

    internal void MarkProcessed(DateTimeOffset now) => ProcessedAt = now;

    public static OutboxMessage Create(string type, string payload, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(type.Length, TypeMaxLength, nameof(type));
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return new OutboxMessage
        {
            Id = Guid.CreateVersion7(now),
            Type = type,
            Payload = payload,
            CreatedAt = now,
            NextAttemptAt = now,
        };
    }
}
