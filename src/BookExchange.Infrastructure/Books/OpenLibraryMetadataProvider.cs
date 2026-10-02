using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using BookExchange.Application.Abstractions;
using Polly.Timeout;

namespace BookExchange.Infrastructure.Books;

/// <summary>
/// Open Library Books API (ADR-13): <c>/api/books?bibkeys=ISBN:…&amp;jscmd=data</c>. Timeouts and retries come
/// from the standard resilience handler; any failure becomes <see cref="BookMetadataUnavailableException"/>
/// so the user falls back to manual entry instead of being blocked.
/// </summary>
internal sealed partial class OpenLibraryMetadataProvider(HttpClient http) : IBookMetadataProvider
{
    public async Task<BookMetadata?> LookupAsync(string isbn13, CancellationToken cancellationToken)
    {
        var key = $"ISBN:{isbn13}";
        try
        {
            using var response = await http.GetAsync(
                new Uri($"api/books?bibkeys={Uri.EscapeDataString(key)}&format=json&jscmd=data", UriKind.Relative), cancellationToken);
            response.EnsureSuccessStatusCode();

            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return json.RootElement.TryGetProperty(key, out var book) ? Map(book) : null;
        }
        catch (Exception e) when (e is HttpRequestException or TimeoutRejectedException or JsonException
            || (e is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new BookMetadataUnavailableException("Open Library lookup failed.", e);
        }
    }

    private static BookMetadata? Map(JsonElement book)
    {
        var title = book.TryGetProperty("title", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var authors = book.TryGetProperty("authors", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray()
                .Select(a => a.TryGetProperty("name", out var n) ? n.GetString() : null)
                .OfType<string>()
                .ToList()
            : [];

        int? year = null;
        if (book.TryGetProperty("publish_date", out var date) && YearPattern().Match(date.GetString() ?? string.Empty) is { Success: true } match)
        {
            year = int.Parse(match.Value, CultureInfo.InvariantCulture);
        }

        var cover = book.TryGetProperty("cover", out var c) && c.TryGetProperty("medium", out var medium) ? medium.GetString() : null;
        return new BookMetadata(title, authors, year, cover?.Replace("http://", "https://", StringComparison.Ordinal));
    }

    [GeneratedRegex(@"\b(1[5-9]|20)\d{2}\b")]
    private static partial Regex YearPattern();
}
