using BookExchange.Application.Abstractions;
using BookExchange.Application.Books;
using BookExchange.Application.Credits;
using BookExchange.Application.Shared;
using BookExchange.Domain.Books;
using BookExchange.Domain.Listings;
using BookExchange.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace BookExchange.Application.Listings;

public sealed class ListingService(
    IAppDbContext db,
    BookService books,
    IListingQueries queries,
    IImageProcessor images,
    IFileStorage storage,
    CreditLedger ledger,
    TimeProvider clock)
{
    public async Task<ServiceResult<PagedResult<ListingSummary>>> SearchAsync(
        ListingSearchRequest request, Guid? viewerId, CancellationToken cancellationToken)
    {
        var origin = await ResolveOriginAsync(request.Lat, request.Lng, viewerId, cancellationToken);
        if (origin is null)
        {
            return ServiceResult.Invalid<PagedResult<ListingSummary>>(
                ErrorCodes.LocationRequired, "Share your location or set your home area to search nearby.", "lat");
        }

        var criteria = new ListingSearchCriteria(
            origin,
            Math.Min(request.RadiusKm ?? ListingSearchLimits.DefaultRadiusKm, ListingSearchLimits.MaxRadiusKm),
            string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim(),
            Isbn.Normalize(request.Isbn),
            request.Condition,
            request.Category,
            request.Beginners == true,
            request.Sort ?? ListingSort.Distance,
            request.Page ?? 1,
            Math.Min(request.PageSize ?? ListingSearchLimits.DefaultPageSize, ListingSearchLimits.MaxPageSize),
            viewerId);

        return ServiceResult.Ok(await queries.SearchAsync(criteria, cancellationToken));
    }

    /// <summary>Active listings are public. Any other status is visible to its owner only (404 for others, R-17).</summary>
    public async Task<ServiceResult<ListingDetails>> GetDetailsAsync(
        Guid listingId, Guid? viewerId, double? latitude, double? longitude, CancellationToken cancellationToken)
    {
        var listing = await db.Listings.AsNoTracking().Include(l => l.Images).SingleOrDefaultAsync(l => l.Id == listingId, cancellationToken);
        if (listing is null || (listing.Status != ListingStatus.Active && listing.OwnerId != viewerId))
        {
            return ServiceResult.NotFound<ListingDetails>();
        }

        return ServiceResult.Ok(await ToDetailsAsync(listing, viewerId, latitude, longitude, cancellationToken));
    }

    public async Task<IReadOnlyList<MyListingItem>> GetMineAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        var rows = await db.Listings.AsNoTracking()
            .Where(l => l.OwnerId == ownerId)
            .OrderByDescending(l => l.CreatedAt)
            .Join(db.Books, l => l.BookId, b => b.Id, (l, b) => new { Listing = l, Book = b })
            .Select(x => new
            {
                x.Listing.Id,
                x.Book.Title,
                x.Book.Authors,
                x.Book.CoverUrl,
                FirstImageId = x.Listing.Images.OrderBy(i => i.Position).Select(i => (Guid?)i.Id).FirstOrDefault(),
                x.Listing.Condition,
                x.Listing.Category,
                x.Listing.GoodForBeginners,
                x.Listing.Status,
                x.Listing.AreaLabel,
                ImageCount = x.Listing.Images.Count,
                x.Listing.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new MyListingItem(
            r.Id, r.Title, r.Authors, r.CoverUrl, r.FirstImageId is { } img ? ImageUrls.Thumbnail(img) : null,
            r.Condition, r.Category, r.GoodForBeginners, r.Status, r.AreaLabel, r.ImageCount, r.CreatedAt)).ToList();
    }

    public async Task<ServiceResult<ListingDetails>> CreateAsync(Guid ownerId, CreateListingRequest request, CancellationToken cancellationToken)
    {
        Book? book;
        if (request.BookId is { } bookId)
        {
            book = await db.Books.SingleOrDefaultAsync(b => b.Id == bookId, cancellationToken);
            if (book is null)
            {
                return ServiceResult.Invalid<ListingDetails>(ErrorCodes.BookNotFound, "Book not found.", "bookId");
            }
        }
        else
        {
            book = await books.GetOrCreateManualAsync(request.ManualBook!, cancellationToken);
        }

        var place = await ResolvePlaceAsync(ownerId, request.AreaLabel, request.Latitude, request.Longitude, cancellationToken);
        if (place is null)
        {
            return ServiceResult.Invalid<ListingDetails>(
                ErrorCodes.LocationRequired, "Set your home area first, or give this listing a location.", "latitude");
        }

        var listing = Listing.Create(
            ownerId, book.Id, request.Description, request.Condition, request.Category!.Value, request.GoodForBeginners, place.Value.Label, place.Value.Point, Jitter(place.Value.Point), clock.GetUtcNow());
        db.Listings.Add(listing);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult.Ok(await ToDetailsAsync(listing, ownerId, null, null, cancellationToken));
    }

    public async Task<ServiceResult<ListingDetails>> UpdateAsync(
        Guid ownerId, Guid listingId, UpdateListingRequest request, CancellationToken cancellationToken)
    {
        var (listing, failure) = await LoadOwnedAsync<ListingDetails>(ownerId, listingId, includeImages: true, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var now = clock.GetUtcNow();
        listing!.UpdateDetails(request.Description, request.Condition, request.Category!.Value, request.GoodForBeginners, now);
        if (request.Latitude is { } lat && request.Longitude is { } lon)
        {
            var point = new GeoPoint(lat, lon);
            listing.MoveTo(request.AreaLabel ?? listing.AreaLabel, point, Jitter(point), now);
        }
        else if (!string.IsNullOrWhiteSpace(request.AreaLabel) && request.AreaLabel.Trim() != listing.AreaLabel)
        {
            // Renaming the area keeps the same point and the same public jitter.
            listing.MoveTo(request.AreaLabel, listing.Location, listing.PublicPoint, now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok(await ToDetailsAsync(listing, ownerId, null, null, cancellationToken));
    }

    public async Task<ServiceResult<bool>> ArchiveAsync(Guid ownerId, Guid listingId, CancellationToken cancellationToken)
    {
        var (listing, failure) = await LoadOwnedAsync<bool>(ownerId, listingId, includeImages: false, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        listing!.Archive(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok(true);
    }

    public async Task<ServiceResult<ListingImageDto>> AddImageAsync(
        Guid ownerId, Guid listingId, Stream upload, CancellationToken cancellationToken)
    {
        var (listing, failure) = await LoadOwnedAsync<ListingImageDto>(ownerId, listingId, includeImages: true, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (listing!.Images.Count >= Listing.MaxImages)
        {
            throw new DomainException($"A listing can have at most {Listing.MaxImages} photos.");
        }

        ProcessedImage processed;
        try
        {
            processed = await images.ProcessAsync(upload, cancellationToken);
        }
        catch (InvalidImageException)
        {
            return ServiceResult.Invalid<ListingImageDto>(ErrorCodes.InvalidImage, "Upload a JPEG, PNG or WebP photo.", "file");
        }

        var now = clock.GetUtcNow();
        var imageId = Guid.CreateVersion7(now);
        var displayKey = $"listings/{listing.Id}/{imageId}-display{processed.Extension}";
        var thumbnailKey = $"listings/{listing.Id}/{imageId}-thumb{processed.Extension}";
        await storage.SaveAsync(displayKey, new MemoryStream(processed.Display), processed.ContentType, cancellationToken);
        await storage.SaveAsync(thumbnailKey, new MemoryStream(processed.Thumbnail), processed.ContentType, cancellationToken);

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var image = listing.AddImage(imageId, displayKey, thumbnailKey, processed.Width, processed.Height, now);
            await db.SaveChangesAsync(cancellationToken);

            // CLAUDE.md (Phase 5b): the starter ficha comes with the first published book that has a photo,
            // once per account (the ledger is idempotent and the unique index backs it up).
            await ledger.GrantStarterAsync(ownerId, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ServiceResult.Ok(ToDto(image));
        }
        catch
        {
            // Don't leave orphaned files when the row couldn't be saved (e.g. a concurrent 9th photo).
            await storage.DeleteAsync(displayKey, CancellationToken.None);
            await storage.DeleteAsync(thumbnailKey, CancellationToken.None);
            throw;
        }
    }

    public async Task<ServiceResult<bool>> RemoveImageAsync(Guid ownerId, Guid listingId, Guid imageId, CancellationToken cancellationToken)
    {
        var (listing, failure) = await LoadOwnedAsync<bool>(ownerId, listingId, includeImages: true, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (listing!.Images.All(i => i.Id != imageId))
        {
            return ServiceResult.NotFound<bool>();
        }

        var removed = listing.RemoveImage(imageId, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(removed.DisplayKey, cancellationToken);
        await storage.DeleteAsync(removed.ThumbnailKey, cancellationToken);
        return ServiceResult.Ok(true);
    }

    public async Task<StoredFile?> OpenImageAsync(Guid imageId, bool thumbnail, CancellationToken cancellationToken)
    {
        var keys = await db.Listings.AsNoTracking()
            .SelectMany(l => l.Images)
            .Where(i => i.Id == imageId)
            .Select(i => new { i.DisplayKey, i.ThumbnailKey })
            .SingleOrDefaultAsync(cancellationToken);
        return keys is null ? null : await storage.OpenReadAsync(thumbnail ? keys.ThumbnailKey : keys.DisplayKey, cancellationToken);
    }

    /// <summary>R-17: someone else's Active listing is public, so editing it is 403; any other listing they can't see is 404.</summary>
    private async Task<(Listing? Listing, ServiceResult<T>? Failure)> LoadOwnedAsync<T>(
        Guid ownerId, Guid listingId, bool includeImages, CancellationToken cancellationToken)
    {
        IQueryable<Listing> query = includeImages ? db.Listings.Include(l => l.Images) : db.Listings;
        var listing = await query.SingleOrDefaultAsync(l => l.Id == listingId, cancellationToken);
        if (listing is null || (!listing.IsOwnedBy(ownerId) && listing.Status != ListingStatus.Active))
        {
            return (null, ServiceResult.NotFound<T>());
        }

        return listing.IsOwnedBy(ownerId) ? (listing, null) : (null, ServiceResult.Forbidden<T>());
    }

    private async Task<ListingDetails> ToDetailsAsync(
        Listing listing, Guid? viewerId, double? latitude, double? longitude, CancellationToken cancellationToken)
    {
        var book = await db.Books.AsNoTracking().SingleAsync(b => b.Id == listing.BookId, cancellationToken);
        var ownerName = await db.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == listing.OwnerId).Select(p => p.DisplayName).SingleAsync(cancellationToken);
        var isMine = listing.OwnerId == viewerId;

        PublicDistance? distance = null;
        if (!isMine && await ResolveOriginAsync(latitude, longitude, viewerId, cancellationToken) is { } origin
            && await queries.PublicDistanceMetersAsync(listing.Id, origin, cancellationToken) is { } meters)
        {
            var (km, upperBound) = PublicLocation.RoundDistance(meters);
            distance = new PublicDistance(km, upperBound);
        }

        return new ListingDetails(
            listing.Id,
            BookDto.From(book),
            listing.Description,
            listing.Condition,
            listing.Category,
            listing.GoodForBeginners,
            listing.Status,
            listing.AreaLabel,
            distance,
            listing.Images.OrderBy(i => i.Position).Select(ToDto).ToList(),
            new ListingOwner(listing.OwnerId, ownerName),
            isMine,
            isMine ? new OwnerLocation(listing.AreaLabel, listing.Location.Latitude, listing.Location.Longitude) : null,
            listing.CreatedAt);
    }

    private async Task<GeoPoint?> ResolveOriginAsync(double? latitude, double? longitude, Guid? viewerId, CancellationToken cancellationToken)
    {
        if (latitude is { } lat && longitude is { } lon)
        {
            return new GeoPoint(lat, lon);
        }

        return viewerId is { } id
            ? await db.UserProfiles.AsNoTracking().Where(p => p.UserId == id).Select(p => p.HomePoint).SingleOrDefaultAsync(cancellationToken)
            : null;
    }

    /// <summary>The listing's place: given coordinates, else the owner's home area (CLAUDE.md: location defaults to home).</summary>
    private async Task<(string Label, GeoPoint Point)?> ResolvePlaceAsync(
        Guid ownerId, string? label, double? latitude, double? longitude, CancellationToken cancellationToken)
    {
        var home = await db.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == ownerId)
            .Select(p => new { p.HomeAreaLabel, p.HomePoint })
            .SingleAsync(cancellationToken);

        var point = latitude is { } lat && longitude is { } lon ? new GeoPoint(lat, lon) : home.HomePoint;
        var area = string.IsNullOrWhiteSpace(label) ? home.HomeAreaLabel : label.Trim();
        return point is null || string.IsNullOrWhiteSpace(area) ? null : (area, point);
    }

    private static GeoPoint Jitter(GeoPoint point) =>
        PublicLocation.Jitter(point, Random.Shared.NextDouble(), Random.Shared.NextDouble());

    private static ListingImageDto ToDto(ListingImage image) =>
        new(image.Id, ImageUrls.Display(image.Id), ImageUrls.Thumbnail(image.Id), image.Width, image.Height);
}
