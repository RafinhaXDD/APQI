using BookExchange.Domain.Books;
using BookExchange.Domain.Credits;
using BookExchange.Domain.Listings;
using BookExchange.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BookExchange.Application.Abstractions;

/// <summary>The application's view of the database (ADR-15: no repositories, use the context directly).</summary>
public interface IAppDbContext
{
    DbSet<UserProfile> UserProfiles { get; }

    DbSet<CreditEvent> CreditEvents { get; }

    DbSet<CreditAccount> CreditAccounts { get; }

    DbSet<Book> Books { get; }

    DbSet<Listing> Listings { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
