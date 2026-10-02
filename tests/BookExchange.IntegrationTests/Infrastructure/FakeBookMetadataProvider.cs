using System.Collections.Concurrent;
using BookExchange.Application.Abstractions;

namespace BookExchange.IntegrationTests.Infrastructure;

/// <summary>Stands in for Open Library (CLAUDE.md: fake implementation for tests).</summary>
public sealed class FakeBookMetadataProvider : IBookMetadataProvider
{
    private int _calls;

    public ConcurrentDictionary<string, BookMetadata> Books { get; } = new();

    /// <summary>When true, every lookup fails as if Open Library were down.</summary>
    public bool Unavailable { get; set; }

    public int Calls => _calls;

    public Task<BookMetadata?> LookupAsync(string isbn13, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        if (Unavailable)
        {
            throw new BookMetadataUnavailableException("Simulated outage.");
        }

        return Task.FromResult(Books.TryGetValue(isbn13, out var book) ? book : null);
    }
}
