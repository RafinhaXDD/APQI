using BookExchange.Domain.Books;
using BookExchange.Domain.Listings;
using BookExchange.Domain.Shared;
using BookExchange.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;
using NpgsqlTypes;

namespace BookExchange.Infrastructure.Persistence;

internal static class GeographyColumns
{
    public const string PointType = "geography (point, 4326)";

    /// <summary>Domain keeps a BCL-only GeoPoint; PostGIS stores x = longitude, y = latitude.</summary>
    public static readonly ValueConverter<GeoPoint, Point> Converter = new(
        g => new Point(g.Longitude, g.Latitude) { SRID = 4326 },
        p => new GeoPoint(p.Y, p.X));
}

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    /// <summary>Generated full-text document over title + authors, accent-insensitive (see migration for the function).</summary>
    public const string SearchVectorColumn = "SearchVector";

    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Books");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Isbn).HasMaxLength(13);
        builder.Property(b => b.Title).HasMaxLength(Book.TitleMaxLength).IsRequired();
        builder.Property(b => b.Authors).HasColumnType("text[]").IsRequired();
        builder.Property(b => b.CoverUrl).HasMaxLength(Book.CoverUrlMaxLength);
        builder.Property(b => b.Source).HasConversion<string>().HasMaxLength(16);

        builder.Property<NpgsqlTsVector>(SearchVectorColumn)
            .HasComputedColumnSql("book_search_document(\"Title\", \"Authors\")", stored: true);
        builder.HasIndex(SearchVectorColumn).HasMethod("GIN");

        // SPEC §10.1: metadata reuse and wishlist matching by ISBN.
        builder.HasIndex(b => b.Isbn).IsUnique().HasFilter("\"Isbn\" IS NOT NULL");
    }
}

internal sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("Listings");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Description).HasMaxLength(Listing.DescriptionMaxLength);
        builder.Property(l => l.AreaLabel).HasMaxLength(Listing.AreaLabelMaxLength).IsRequired();
        builder.Property(l => l.Condition).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.Category).HasConversion<string>().HasMaxLength(32);
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(l => l.Location).HasColumnType(GeographyColumns.PointType).HasConversion(GeographyColumns.Converter).IsRequired();
        builder.Property(l => l.PublicPoint).HasColumnType(GeographyColumns.PointType).HasConversion(GeographyColumns.Converter).IsRequired();
        builder.Property(l => l.Version).IsRowVersion();

        builder.HasMany(l => l.Images).WithOne().HasForeignKey(i => i.ListingId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(l => l.Images).HasField("_images").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<AppUser>().WithMany().HasForeignKey(l => l.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Book>().WithMany().HasForeignKey(l => l.BookId).OnDelete(DeleteBehavior.Restrict);

        // Radius search runs on the public (jittered) point, so that's the one with the GiST index.
        builder.HasIndex(l => l.PublicPoint).HasMethod("GIST");
        builder.HasIndex(l => new { l.Status, l.CreatedAt });
        builder.HasIndex(l => l.OwnerId);
    }
}

internal sealed class ListingImageConfiguration : IEntityTypeConfiguration<ListingImage>
{
    public void Configure(EntityTypeBuilder<ListingImage> builder)
    {
        builder.ToTable("ListingImages");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.DisplayKey).HasMaxLength(200).IsRequired();
        builder.Property(i => i.ThumbnailKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(i => new { i.ListingId, i.Position });
    }
}
