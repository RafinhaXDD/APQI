using BookExchange.Application.Abstractions;
using BookExchange.Application.Shared;
using BookExchange.Domain.Books;
using Microsoft.EntityFrameworkCore;

namespace BookExchange.Application.Books;

public sealed record BookDto(Guid Id, string? Isbn, string Title, IReadOnlyList<string> Authors, int? PublishedYear, string? CoverUrl, string Source)
{
    public static BookDto From(Book book) =>
        new(book.Id, book.Isbn, book.Title, book.Authors, book.PublishedYear, book.CoverUrl, book.Source.ToString());
}

/// <summary>A book typed in by hand when the ISBN lookup finds nothing (or the book has no ISBN).</summary>
public sealed record ManualBookRequest(string? Isbn, string Title, IReadOnlyList<string> Authors);

public sealed class BookService(IAppDbContext db, IBookMetadataProvider metadata, TimeProvider clock)
{
    /// <summary>
    /// Scan flow: a known ISBN is answered from the database; otherwise the provider is asked once and the
    /// result is stored, so the next scan of the same book is instant and doesn't depend on Open Library.
    /// </summary>
    public async Task<ServiceResult<BookDto>> LookupAsync(string rawIsbn, CancellationToken cancellationToken)
    {
        var isbn = Isbn.Normalize(rawIsbn);
        if (isbn is null)
        {
            return ServiceResult.Invalid<BookDto>(ErrorCodes.InvalidIsbn, "That is not a valid ISBN.", "isbn");
        }

        var existing = await db.Books.AsNoTracking().SingleOrDefaultAsync(b => b.Isbn == isbn, cancellationToken);
        if (existing is not null)
        {
            return ServiceResult.Ok(BookDto.From(existing));
        }

        BookMetadata? found;
        try
        {
            found = await metadata.LookupAsync(isbn, cancellationToken);
        }
        catch (BookMetadataUnavailableException)
        {
            return ServiceResult.Unavailable<BookDto>(ErrorCodes.BookLookupUnavailable, "Book lookup is unavailable. Enter the details manually.");
        }

        if (found is null)
        {
            return ServiceResult.NotFound<BookDto>(ErrorCodes.BookNotFound);
        }

        var book = Book.Create(isbn, found.Title, found.Authors, found.PublishedYear, found.CoverUrl, BookSource.OpenLibrary, clock.GetUtcNow());
        return ServiceResult.Ok(BookDto.From(await SaveOrReuseAsync(book, cancellationToken)));
    }

    /// <summary>Reuses the stored book for this ISBN when there is one; otherwise stores the manual entry.</summary>
    public async Task<Book> GetOrCreateManualAsync(ManualBookRequest request, CancellationToken cancellationToken)
    {
        var isbn = Isbn.Normalize(request.Isbn);
        if (isbn is not null)
        {
            var existing = await db.Books.SingleOrDefaultAsync(b => b.Isbn == isbn, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }
        }

        var book = Book.Create(isbn, request.Title, request.Authors, null, null, BookSource.Manual, clock.GetUtcNow());
        return await SaveOrReuseAsync(book, cancellationToken);
    }

    /// <summary>Two people scanning the same new ISBN at once: the unique index lets one insert win.</summary>
    private async Task<Book> SaveOrReuseAsync(Book book, CancellationToken cancellationToken)
    {
        db.Books.Add(book);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return book;
        }
        catch (DbUpdateException) when (book.Isbn is not null)
        {
            db.Books.Entry(book).State = EntityState.Detached;
            return await db.Books.SingleAsync(b => b.Isbn == book.Isbn, cancellationToken);
        }
    }
}
