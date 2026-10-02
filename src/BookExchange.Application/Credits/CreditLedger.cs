using BookExchange.Application.Abstractions;
using BookExchange.Domain.Credits;
using Microsoft.EntityFrameworkCore;

namespace BookExchange.Application.Credits;

public sealed record CreditBalance(int Available, int Held);

/// <summary>
/// The only writer of credit events (PLAN §4.4). Each write locks the user's <see cref="CreditAccount"/>
/// row (<c>FOR UPDATE</c>) and recomputes the cached balance from the events, inside the caller's transaction.
/// </summary>
public sealed class CreditLedger(IAppDbContext db, TimeProvider clock)
{
    /// <summary>Grants the one-time starter credit. Returns false when the user already has it.</summary>
    public async Task<bool> GrantStarterAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await LockAccountAsync(userId, cancellationToken);
        if (await db.CreditEvents.AnyAsync(e => e.UserId == userId && e.Type == CreditEventType.Starter, cancellationToken))
        {
            return false;
        }

        await AppendAsync(account, CreditEvent.Starter(userId, clock.GetUtcNow()), cancellationToken);
        return true;
    }

    public async Task<CreditBalance> GetBalanceAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await db.CreditAccounts.AsNoTracking().SingleOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        return account is null ? new CreditBalance(0, 0) : new CreditBalance(account.Available, account.Held);
    }

    private async Task<CreditAccount> LockAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Credit changes must run inside a database transaction.");
        }

        var rows = await db.CreditAccounts
            .FromSql($"""SELECT * FROM "CreditAccounts" WHERE "UserId" = {userId} FOR UPDATE""")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault()
            ?? throw new InvalidOperationException($"Credit account for user {userId} does not exist.");
    }

    private async Task AppendAsync(CreditAccount account, CreditEvent creditEvent, CancellationToken cancellationToken)
    {
        var events = db.CreditEvents.Where(e => e.UserId == account.UserId);
        var sum = await events.SumAsync(e => e.Amount, cancellationToken);
        var holds = await events.CountAsync(e => e.Type == CreditEventType.Held, cancellationToken);
        var closedHolds = await events.CountAsync(
            e => e.Type == CreditEventType.Released || e.Type == CreditEventType.Spent, cancellationToken);

        var heldDelta = creditEvent.Type switch
        {
            CreditEventType.Held => 1,
            CreditEventType.Released or CreditEventType.Spent => -1,
            _ => 0,
        };

        // Throws DomainException when available would go negative, so nothing is written.
        account.Recalculate(sum + creditEvent.Amount, holds - closedHolds + heldDelta);
        db.CreditEvents.Add(creditEvent);
    }
}
