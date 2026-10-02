using BookExchange.Domain.Credits;
using BookExchange.Domain.Shared;
using BookExchange.Domain.Users;
using BookExchange.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;

namespace BookExchange.Infrastructure.Persistence;

internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(p => p.UserId);
        builder.Property(p => p.UserId).ValueGeneratedNever();
        builder.Property(p => p.DisplayName).HasMaxLength(UserProfile.DisplayNameMaxLength).IsRequired();
        builder.Property(p => p.Bio).HasMaxLength(UserProfile.BioMaxLength);
        builder.Property(p => p.HomeAreaLabel).HasMaxLength(UserProfile.AreaLabelMaxLength);
        builder.Property(p => p.PreferredLanguage).HasMaxLength(8).IsRequired();

        // Domain keeps a BCL-only GeoPoint; the database stores a PostGIS geography point (x = lon, y = lat).
        builder.Property(p => p.HomePoint)
            .HasColumnType("geography (point, 4326)")
            .HasConversion(
                g => new Point(g!.Longitude, g.Latitude) { SRID = 4326 },
                p => new GeoPoint(p.Y, p.X));

        builder.Ignore(p => p.HasHomeArea);
        builder.HasOne<AppUser>().WithOne().HasForeignKey<UserProfile>(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CreditEventConfiguration : IEntityTypeConfiguration<CreditEvent>
{
    public void Configure(EntityTypeBuilder<CreditEvent> builder)
    {
        builder.ToTable("CreditEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Description).HasMaxLength(CreditEvent.DescriptionMaxLength).IsRequired();
        // Two indexes on UserId: they must be named, or EF merges them into one.
        builder.HasIndex(e => e.UserId, "IX_CreditEvents_UserId");

        // Idempotency (PLAN §4.4): one starter credit per user; one event of each kind per exchange.
        builder.HasIndex(e => e.UserId, "UX_CreditEvents_Starter").IsUnique().HasFilter("\"Type\" = 'Starter'");
        builder.HasIndex(e => new { e.ExchangeRequestId, e.Type }).IsUnique().HasFilter("\"ExchangeRequestId\" IS NOT NULL");

        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CreditAccountConfiguration : IEntityTypeConfiguration<CreditAccount>
{
    public void Configure(EntityTypeBuilder<CreditAccount> builder)
    {
        builder.ToTable("CreditAccounts", t =>
        {
            // Last line of defence behind the domain check (no negative available balance).
            t.HasCheckConstraint("CK_CreditAccounts_Available_NonNegative", "\"Available\" >= 0");
            t.HasCheckConstraint("CK_CreditAccounts_Held_NonNegative", "\"Held\" >= 0");
        });
        builder.HasKey(a => a.UserId);
        builder.Property(a => a.UserId).ValueGeneratedNever();
        builder.HasOne<AppUser>().WithOne().HasForeignKey<CreditAccount>(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
