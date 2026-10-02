using System.Text.RegularExpressions;
using BookExchange.Application.Listings;
using BookExchange.Application.Shared;
using BookExchange.Domain.Listings;
using BookExchange.Domain.Shared;
using BookExchange.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace BookExchange.Infrastructure.Listings;

/// <summary>
/// Search runs as one parameterized SQL statement: ST_DWithin on the GiST-indexed public point, full-text
/// on the generated Book.SearchVector, a window count for paging. Only Active listings are returned.
/// </summary>
internal sealed partial class ListingQueries(AppDbContext db) : IListingQueries
{
    private const string Origin = "ST_SetSRID(ST_MakePoint(@lon, @lat), 4326)::geography";

    public async Task<PagedResult<ListingSummary>> SearchAsync(ListingSearchCriteria criteria, CancellationToken cancellationToken)
    {
        // Fixed whitelist: the ORDER BY clause never contains caller input.
        var orderBy = criteria.Sort switch
        {
            ListingSort.Newest => "l.\"CreatedAt\" DESC, l.\"Id\"",
            ListingSort.Title => "b.\"Title\", l.\"Id\"",
            _ => "\"DistanceMeters\", l.\"Id\"",
        };

        var sql = $"""
            SELECT l."Id", b."Title", b."Authors", b."CoverUrl",
                   (SELECT i."Id" FROM "ListingImages" i WHERE i."ListingId" = l."Id" ORDER BY i."Position" LIMIT 1) AS "ThumbnailImageId",
                   l."Condition", l."Category", l."GoodForBeginners", l."AreaLabel",
                   ST_Distance(l."PublicPoint", {Origin}) AS "DistanceMeters",
                   p."DisplayName" AS "OwnerDisplayName", l."OwnerId", l."CreatedAt",
                   COUNT(*) OVER () AS "TotalCount"
            FROM "Listings" l
            JOIN "Books" b ON b."Id" = l."BookId"
            JOIN "UserProfiles" p ON p."UserId" = l."OwnerId"
            WHERE l."Status" = 'Active'
              AND ST_DWithin(l."PublicPoint", {Origin}, @radius)
              AND (@tsquery IS NULL OR b."SearchVector" @@ to_tsquery('simple', immutable_unaccent(@tsquery)))
              AND (@isbn IS NULL OR b."Isbn" = @isbn)
              AND (@condition IS NULL OR l."Condition" = @condition)
              AND (@category IS NULL OR l."Category" = @category)
              AND (NOT @beginners OR l."GoodForBeginners")
            ORDER BY {orderBy}
            LIMIT @take OFFSET @skip
            """;

        var rows = await db.Database.SqlQueryRaw<SearchRow>(
                sql,
                new NpgsqlParameter("lat", criteria.Origin.Latitude),
                new NpgsqlParameter("lon", criteria.Origin.Longitude),
                new NpgsqlParameter("radius", criteria.RadiusKm * 1000),
                Text("tsquery", ToPrefixQuery(criteria.Query)),
                Text("isbn", criteria.Isbn13),
                Text("condition", criteria.Condition?.ToString()),
                Text("category", criteria.Category?.ToString()),
                new NpgsqlParameter("beginners", criteria.BeginnersOnly),
                new NpgsqlParameter("take", criteria.PageSize),
                new NpgsqlParameter("skip", (criteria.Page - 1) * criteria.PageSize))
            .ToListAsync(cancellationToken);

        var items = rows.Select(r =>
        {
            var (km, upperBound) = PublicLocation.RoundDistance(r.DistanceMeters);
            return new ListingSummary(
                r.Id, r.Title, r.Authors, r.CoverUrl,
                r.ThumbnailImageId is { } image ? ImageUrls.Thumbnail(image) : null,
                Enum.Parse<BookCondition>(r.Condition), Enum.Parse<BookCategory>(r.Category), r.GoodForBeginners,
                r.AreaLabel, km, upperBound,
                r.OwnerDisplayName, r.OwnerId == criteria.ViewerId, r.CreatedAt);
        }).ToList();

        return new PagedResult<ListingSummary>(items, criteria.Page, criteria.PageSize, (int)(rows.FirstOrDefault()?.TotalCount ?? 0));
    }

    public async Task<double?> PublicDistanceMetersAsync(Guid listingId, GeoPoint origin, CancellationToken cancellationToken)
    {
        var distances = await db.Database.SqlQueryRaw<double>(
                $"""SELECT ST_Distance(l."PublicPoint", {Origin}) AS "Value" FROM "Listings" l WHERE l."Id" = @id""",
                new NpgsqlParameter("id", listingId),
                new NpgsqlParameter("lat", origin.Latitude),
                new NpgsqlParameter("lon", origin.Longitude))
            .ToListAsync(cancellationToken);
        return distances.Count == 0 ? null : distances[0];
    }

    /// <summary>
    /// "jose sara" → "jose:* &amp; sara:*" (prefix match, all words). Only letters and digits survive, so no
    /// tsquery operators from the caller ever reach to_tsquery.
    /// </summary>
    internal static string? ToPrefixQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var words = WordPattern().Matches(query).Select(m => m.Value.ToLowerInvariant() + ":*").Take(8).ToList();
        return words.Count == 0 ? null : string.Join(" & ", words);
    }

    private static NpgsqlParameter Text(string name, string? value) =>
        new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();

    private sealed record SearchRow(
        Guid Id,
        string Title,
        string[] Authors,
        string? CoverUrl,
        Guid? ThumbnailImageId,
        string Condition,
        string Category,
        bool GoodForBeginners,
        string AreaLabel,
        double DistanceMeters,
        string OwnerDisplayName,
        Guid OwnerId,
        DateTimeOffset CreatedAt,
        long TotalCount);
}
