using BookExchange.Domain.Shared;

namespace BookExchange.Domain.Listings;

public enum ListingStatus
{
    Draft,
    Active,
    Reserved,
    Exchanged,
    Archived,
}

public enum BookCondition
{
    New,
    LikeNew,
    Good,
    Fair,
    Worn,
}

/// <summary>AQPI catalogue subjects (Duda's brief). "Bom para começar" is a separate tag, not a category.</summary>
public enum BookCategory
{
    Fiction,
    NonFiction,
    PersonalDevelopment,
    Philosophy,
    CareerStrategy,
    Classics,
}

/// <summary>
/// A physical copy offered by its owner. The exact <see cref="Location"/> is private to the owner; the
/// <see cref="PublicPoint"/> is the same place shifted a few hundred metres, fixed per location, and is
/// the only point used for public distances (R-14, PLAN Q5).
/// </summary>
public sealed class Listing
{
    public const int DescriptionMaxLength = 2000;
    public const int AreaLabelMaxLength = 80;
    public const int MaxImages = 8;

    private readonly List<ListingImage> _images = [];

    private Listing()
    {
    }

    public Guid Id { get; private set; }

    public Guid OwnerId { get; private set; }

    public Guid BookId { get; private set; }

    public string? Description { get; private set; }

    public BookCondition Condition { get; private set; }

    public BookCategory Category { get; private set; }

    /// <summary>"Bom para começar": an easy entry point for people who don't read much yet.</summary>
    public bool GoodForBeginners { get; private set; }

    public GeoPoint Location { get; private set; } = null!;

    public GeoPoint PublicPoint { get; private set; } = null!;

    public string AreaLabel { get; private set; } = null!;

    public ListingStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin, R-12).</summary>
    public uint Version { get; private set; }

    public IReadOnlyList<ListingImage> Images => _images;

    /// <summary>Creates a published (Active) listing: listing must take seconds, so there is no draft step.</summary>
    public static Listing Create(
        Guid ownerId,
        Guid bookId,
        string? description,
        BookCondition condition,
        BookCategory category,
        bool goodForBeginners,
        string areaLabel,
        GeoPoint location,
        GeoPoint publicPoint,
        DateTimeOffset now)
    {
        if (ownerId == Guid.Empty || bookId == Guid.Empty)
        {
            throw new DomainException("A listing needs an owner and a book.");
        }

        var listing = new Listing
        {
            Id = Guid.CreateVersion7(now),
            OwnerId = ownerId,
            BookId = bookId,
            Status = ListingStatus.Active,
            CreatedAt = now,
        };
        listing.ApplyDetails(description, condition, category, goodForBeginners, now);
        listing.ApplyLocation(areaLabel, location, publicPoint);
        return listing;
    }

    public bool IsOwnedBy(Guid userId) => OwnerId == userId;

    public void UpdateDetails(string? description, BookCondition condition, BookCategory category, bool goodForBeginners, DateTimeOffset now)
    {
        EnsureEditable();
        ApplyDetails(description, condition, category, goodForBeginners, now);
    }

    public void MoveTo(string areaLabel, GeoPoint location, GeoPoint publicPoint, DateTimeOffset now)
    {
        EnsureEditable();
        ApplyLocation(areaLabel, location, publicPoint);
        UpdatedAt = now;
    }

    /// <summary>Withdraws an Active listing. A Reserved one must have its exchange cancelled first (PLAN Q13).</summary>
    public void Archive(DateTimeOffset now)
    {
        if (Status != ListingStatus.Active)
        {
            throw new DomainException(Status == ListingStatus.Reserved
                ? "This listing is reserved for an exchange. Cancel the exchange before archiving it."
                : $"A {Status} listing cannot be archived.");
        }

        Status = ListingStatus.Archived;
        UpdatedAt = now;
    }

    public ListingImage AddImage(Guid imageId, string displayKey, string thumbnailKey, int width, int height, DateTimeOffset now)
    {
        EnsureEditable();
        if (_images.Count >= MaxImages)
        {
            throw new DomainException($"A listing can have at most {MaxImages} photos.");
        }

        var position = _images.Count == 0 ? 0 : _images.Max(i => i.Position) + 1;
        var image = ListingImage.Create(imageId, Id, position, displayKey, thumbnailKey, width, height, now);
        _images.Add(image);
        UpdatedAt = now;
        return image;
    }

    public ListingImage RemoveImage(Guid imageId, DateTimeOffset now)
    {
        EnsureEditable();
        var image = _images.SingleOrDefault(i => i.Id == imageId) ?? throw new DomainException("Photo not found on this listing.");
        _images.Remove(image);
        UpdatedAt = now;
        return image;
    }

    private void EnsureEditable()
    {
        if (Status != ListingStatus.Active)
        {
            throw new DomainException($"A {Status} listing can't be changed.");
        }
    }

    private void ApplyDetails(string? description, BookCondition condition, BookCategory category, bool goodForBeginners, DateTimeOffset now)
    {
        var text = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (text?.Length > DescriptionMaxLength)
        {
            throw new DomainException($"Description can have at most {DescriptionMaxLength} characters.");
        }

        if (!Enum.IsDefined(condition))
        {
            throw new DomainException("Unknown condition.");
        }

        if (!Enum.IsDefined(category))
        {
            throw new DomainException("Unknown category.");
        }

        Description = text;
        Condition = condition;
        Category = category;
        GoodForBeginners = goodForBeginners;
        UpdatedAt = now;
    }

    private void ApplyLocation(string areaLabel, GeoPoint location, GeoPoint publicPoint)
    {
        var label = areaLabel?.Trim() ?? string.Empty;
        if (label.Length is 0 or > AreaLabelMaxLength)
        {
            throw new DomainException($"Area label must be between 1 and {AreaLabelMaxLength} characters.");
        }

        AreaLabel = label;
        Location = location ?? throw new DomainException("A listing needs a location.");
        PublicPoint = publicPoint ?? throw new DomainException("A listing needs a public point.");
    }
}

public sealed class ListingImage
{
    private ListingImage()
    {
    }

    public Guid Id { get; private set; }

    public Guid ListingId { get; private set; }

    public int Position { get; private set; }

    /// <summary>Storage keys are generated (GUID names), never the uploaded file name.</summary>
    public string DisplayKey { get; private set; } = null!;

    public string ThumbnailKey { get; private set; } = null!;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static ListingImage Create(
        Guid id, Guid listingId, int position, string displayKey, string thumbnailKey, int width, int height, DateTimeOffset now) =>
        new()
        {
            Id = id,
            ListingId = listingId,
            Position = position,
            DisplayKey = displayKey,
            ThumbnailKey = thumbnailKey,
            Width = width,
            Height = height,
            CreatedAt = now,
        };
}
