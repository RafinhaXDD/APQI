using BookExchange.Application.Shared;
using BookExchange.Domain.Listings;
using BookExchange.Domain.Shared;

namespace BookExchange.Application.Listings;

public sealed record ListingSearchCriteria(
    GeoPoint Origin,
    double RadiusKm,
    string? Query,
    string? Isbn13,
    BookCondition? Condition,
    BookCategory? Category,
    bool BeginnersOnly,
    ListingSort Sort,
    int Page,
    int PageSize,
    Guid? ViewerId);

/// <summary>PostGIS + full-text read queries (raw SQL in Infrastructure; distances always from the public point).</summary>
public interface IListingQueries
{
    Task<PagedResult<ListingSummary>> SearchAsync(ListingSearchCriteria criteria, CancellationToken cancellationToken);

    /// <summary>Distance in metres from <paramref name="origin"/> to the listing's public point.</summary>
    Task<double?> PublicDistanceMetersAsync(Guid listingId, GeoPoint origin, CancellationToken cancellationToken);
}
