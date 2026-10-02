using BookExchange.Domain.Shared;

namespace BookExchange.Domain.Credits;

public enum CreditEventType
{
    Starter,
    Held,
    Released,
    Spent,
    Earned,
    AdminAdjustment,
}

/// <summary>
/// One immutable line in a user's credit ledger (R-13). Balances are derived from these events,
/// never edited directly. Amounts: Starter +1, Held -1, Released +1, Spent 0 (closes the hold), Earned +1.
/// </summary>
public sealed class CreditEvent
{
    public const int DescriptionMaxLength = 200;

    private CreditEvent()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public CreditEventType Type { get; private set; }

    public int Amount { get; private set; }

    public Guid? ExchangeRequestId { get; private set; }

    public string Description { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public static CreditEvent Starter(Guid userId, DateTimeOffset now) =>
        Create(userId, CreditEventType.Starter, +1, exchangeRequestId: null, "Starter credit for confirming your email", now);

    private static CreditEvent Create(
        Guid userId, CreditEventType type, int amount, Guid? exchangeRequestId, string description, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A credit event needs a user.");
        }

        return new CreditEvent
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            Type = type,
            Amount = amount,
            ExchangeRequestId = exchangeRequestId,
            Description = description,
            CreatedAt = now,
        };
    }
}
