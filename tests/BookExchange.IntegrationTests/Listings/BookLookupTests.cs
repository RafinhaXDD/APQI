using System.Net;
using BookExchange.Application.Abstractions;
using BookExchange.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BookExchange.IntegrationTests.Listings;

public sealed class BookLookupTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Known_isbn_is_fetched_once_then_served_from_the_database()
    {
        fixture.Factory.BookMetadata.Books["9788535914849"] =
            new BookMetadata("Ensaio sobre a Cegueira", ["José Saramago"], 1995, "https://covers.openlibrary.org/b/id/1-M.jpg");
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();
        var callsBefore = fixture.Factory.BookMetadata.Calls;

        using var first = await client.GetAsync("/api/books/lookup?isbn=978-85-359-1484-9");
        using var second = await client.GetAsync("/api/books/lookup?isbn=9788535914849");
        using var body = await first.JsonAsync();

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        body.RootElement.GetProperty("title").GetString().Should().Be("Ensaio sobre a Cegueira");
        body.RootElement.GetProperty("isbn").GetString().Should().Be("9788535914849");
        body.RootElement.GetProperty("publishedYear").GetInt32().Should().Be(1995);
        fixture.Factory.BookMetadata.Calls.Should().Be(callsBefore + 1, "the second lookup must not call Open Library");
    }

    [Fact]
    public async Task Isbn10_is_normalized_to_isbn13()
    {
        fixture.Factory.BookMetadata.Books["9780306406157"] = new BookMetadata("Test Book", ["A. Author"], null, null);
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();

        using var response = await client.GetAsync("/api/books/lookup?isbn=0-306-40615-2");
        using var body = await response.JsonAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.RootElement.GetProperty("isbn").GetString().Should().Be("9780306406157");
    }

    [Theory]
    [InlineData("9788535914848")]
    [InlineData("12345")]
    [InlineData("not-an-isbn")]
    public async Task Invalid_isbn_is_422(string isbn)
    {
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();

        using var response = await client.GetAsync($"/api/books/lookup?isbn={isbn}");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.ProblemCodeAsync()).Should().Be("book.invalid_isbn");
    }

    [Fact]
    public async Task Unknown_isbn_is_404_so_the_client_offers_manual_entry()
    {
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();

        using var response = await client.GetAsync("/api/books/lookup?isbn=9781234567897");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ProblemCodeAsync()).Should().Be("book.not_found");
    }

    [Fact]
    public async Task Provider_outage_is_503_and_stores_nothing()
    {
        using var client = fixture.CreateAuthClient();
        await client.SignUpAndLoginAsync();
        fixture.Factory.BookMetadata.Unavailable = true;
        try
        {
            using var response = await client.GetAsync("/api/books/lookup?isbn=9780141036144");

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            (await response.ProblemCodeAsync()).Should().Be("book.lookup_unavailable");
            (await fixture.WithDbAsync(db => db.Books.AnyAsync(b => b.Isbn == "9780141036144", Ct))).Should().BeFalse();
        }
        finally
        {
            fixture.Factory.BookMetadata.Unavailable = false;
        }
    }

    [Fact]
    public async Task Lookup_requires_sign_in()
    {
        using var client = fixture.CreateAuthClient();

        using var response = await client.GetAsync("/api/books/lookup?isbn=9788535914849");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
