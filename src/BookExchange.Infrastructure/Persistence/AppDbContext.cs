using BookExchange.Application.Abstractions;
using BookExchange.Domain.Books;
using BookExchange.Domain.Credits;
using BookExchange.Domain.Listings;
using BookExchange.Domain.Users;
using BookExchange.Infrastructure.Identity;
using BookExchange.Infrastructure.Outbox;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BookExchange.Infrastructure.Persistence;

/// <summary>Identity user tables only (no roles yet; admin roles arrive with moderation).</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<AppUser, Guid>(options), IAppDbContext
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<CreditEvent> CreditEvents => Set<CreditEvent>();

    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();

    public DbSet<Book> Books => Set<Book>();

    public DbSet<Listing> Listings => Set<Listing>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("postgis");
        builder.HasPostgresExtension("unaccent");
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
