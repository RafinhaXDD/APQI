using BookExchange.Domain.Shared;

namespace BookExchange.Domain.Credits;

/// <summary>
/// Cached balance and lock row for one user's ledger (ADR-06). Every new <see cref="CreditEvent"/>
/// locks this row and recomputes the cache from the events in the same transaction (R-13).
/// </summary>
public sealed class CreditAccount
{
    private CreditAccount()
    {
    }

    public Guid UserId { get; private set; }

    /// <summary>Credits the user can spend now: the sum of all event amounts.</summary>
    public int Available { get; private set; }

    /// <summary>Credits reserved by accepted credit requests, not yet spent or released.</summary>
    public int Held { get; private set; }

    public static CreditAccount Open(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A credit account needs a user.");
        }

        return new CreditAccount { UserId = userId };
    }

    /// <param name="available">Sum of all event amounts, including the one being added.</param>
    /// <param name="held">Held events minus Released and Spent events.</param>
    public void Recalculate(int available, int held)
    {
        if (available < 0)
        {
            throw new DomainException("Not enough credits.");
        }

        if (held < 0)
        {
            throw new DomainException("Held credits cannot be negative.");
        }

        Available = available;
        Held = held;
    }
}
