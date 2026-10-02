namespace BookExchange.Application.Shared.Outbox;

/// <param name="Id">Stable message id; use it as the idempotency key for external side effects.</param>
public sealed record OutboxMessageEnvelope(Guid Id, string Type, string Payload, int Attempt);

/// <summary>
/// Handles one outbox message type. Delivery is at least once: a handler may run again for the
/// same message after a failure, so it must be idempotent (keyed by <see cref="OutboxMessageEnvelope.Id"/>).
/// Database writes made by the handler commit atomically with marking the message processed.
/// </summary>
public interface IOutboxMessageHandler
{
    string MessageType { get; }

    Task HandleAsync(OutboxMessageEnvelope message, CancellationToken cancellationToken);
}
