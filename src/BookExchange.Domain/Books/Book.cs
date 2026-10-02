using BookExchange.Domain.Shared;

namespace BookExchange.Domain.Books;

public enum BookSource
{
    OpenLibrary,
    Manual,
}

/// <summary>
/// A title shared by every copy listed. Reused by ISBN so a second scan of the same book is instant
/// and wishlist matching (by ISBN) works. Books without an ISBN come from manual entry.
/// </summary>
public sealed class Book
{
    public const int TitleMaxLength = 300;
    public const int AuthorMaxLength = 150;
    public const int MaxAuthors = 10;
    public const int CoverUrlMaxLength = 500;

    private Book()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Normalized ISBN-13, unique when present.</summary>
    public string? Isbn { get; private set; }

    public string Title { get; private set; } = null!;

    public IReadOnlyList<string> Authors { get; private set; } = [];

    public int? PublishedYear { get; private set; }

    public string? CoverUrl { get; private set; }

    public BookSource Source { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Book Create(
        string? isbn,
        string title,
        IEnumerable<string> authors,
        int? publishedYear,
        string? coverUrl,
        BookSource source,
        DateTimeOffset now)
    {
        var normalizedIsbn = isbn is null ? null : Books.Isbn.Normalize(isbn) ?? throw new DomainException("ISBN is not valid.");
        var cleanTitle = title?.Trim() ?? string.Empty;
        if (cleanTitle.Length is 0 or > TitleMaxLength)
        {
            throw new DomainException($"Title must be between 1 and {TitleMaxLength} characters.");
        }

        var cleanAuthors = authors.Select(a => a.Trim()).Where(a => a.Length > 0).Distinct().ToList();
        if (cleanAuthors.Count > MaxAuthors || cleanAuthors.Any(a => a.Length > AuthorMaxLength))
        {
            throw new DomainException($"At most {MaxAuthors} authors of up to {AuthorMaxLength} characters.");
        }

        if (coverUrl is not null && (coverUrl.Length > CoverUrlMaxLength
            || !Uri.TryCreate(coverUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            coverUrl = null; // Only well-formed https covers are kept; a bad one is dropped, not fatal.
        }

        return new Book
        {
            Id = Guid.CreateVersion7(now),
            Isbn = normalizedIsbn,
            Title = cleanTitle,
            Authors = cleanAuthors,
            PublishedYear = publishedYear is > 0 and < 3000 ? publishedYear : null,
            CoverUrl = coverUrl,
            Source = source,
            CreatedAt = now,
        };
    }
}
