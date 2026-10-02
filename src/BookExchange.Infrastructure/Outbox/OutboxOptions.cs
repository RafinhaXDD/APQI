namespace BookExchange.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Runs the background polling loop. Tests turn it off and call the processor directly.</summary>
    public bool Enabled { get; set; } = true;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Maximum messages handled per polling cycle.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>After this many failed attempts a message is dead-lettered.</summary>
    public int MaxAttempts { get; set; } = 10;

    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Exponential backoff: 2^attempts seconds, capped at <see cref="MaxBackoff"/>.</summary>
    public TimeSpan BackoffFor(int attempts)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempts);
        var seconds = Math.Pow(2, Math.Min(attempts, 30));
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxBackoff.TotalSeconds));
    }
}
