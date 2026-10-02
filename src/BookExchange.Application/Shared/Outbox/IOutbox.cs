namespace BookExchange.Application.Shared.Outbox;

/// <summary>
/// Records a side effect to run after the current unit of work commits.
/// The message is saved by the same SaveChanges call (same transaction) as the state change.
/// </summary>
public interface IOutbox
{
    void Enqueue<TPayload>(string type, TPayload payload)
        where TPayload : notnull;
}
