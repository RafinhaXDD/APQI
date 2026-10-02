namespace BookExchange.Application.Abstractions;

/// <summary>Binary storage for uploaded files (Azure Blob in production, Azurite or local disk in dev/tests).</summary>
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <returns>null when the key doesn't exist.</returns>
    Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

public sealed record StoredFile(Stream Content, string ContentType);

/// <summary>Book metadata by ISBN (Open Library in production, a fake in tests).</summary>
public interface IBookMetadataProvider
{
    /// <returns>null when the ISBN is unknown.</returns>
    /// <exception cref="BookMetadataUnavailableException">The provider could not be reached.</exception>
    Task<BookMetadata?> LookupAsync(string isbn13, CancellationToken cancellationToken);
}

public sealed record BookMetadata(string Title, IReadOnlyList<string> Authors, int? PublishedYear, string? CoverUrl);

public sealed class BookMetadataUnavailableException : Exception
{
    public BookMetadataUnavailableException()
    {
    }

    public BookMetadataUnavailableException(string message)
        : base(message)
    {
    }

    public BookMetadataUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Validates and re-encodes uploaded photos (SPEC §6.10).</summary>
public interface IImageProcessor
{
    /// <exception cref="InvalidImageException">Not a JPEG/PNG/WebP image by its content.</exception>
    Task<ProcessedImage> ProcessAsync(Stream upload, CancellationToken cancellationToken);
}

/// <summary>Re-encoded images without any metadata (EXIF, GPS, IPTC, XMP stripped).</summary>
public sealed record ProcessedImage(byte[] Display, byte[] Thumbnail, string ContentType, string Extension, int Width, int Height);

public sealed class InvalidImageException : Exception
{
    public InvalidImageException()
    {
    }

    public InvalidImageException(string message)
        : base(message)
    {
    }

    public InvalidImageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
