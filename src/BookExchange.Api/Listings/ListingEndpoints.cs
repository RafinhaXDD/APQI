using System.Security.Claims;
using BookExchange.Api.Infrastructure;
using BookExchange.Application.Books;
using BookExchange.Application.Listings;
using BookExchange.Application.Shared;
using Microsoft.AspNetCore.Mvc;

namespace BookExchange.Api.Listings;

internal static class ListingEndpoints
{
    /// <summary>SPEC §6.10: at most 5 MB per photo. The request limit adds room for multipart overhead.</summary>
    public const long MaxImageBytes = 5 * 1024 * 1024;

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/books/lookup", async ([FromQuery] string isbn, BookService books, CancellationToken ct) =>
                (await books.LookupAsync(isbn, ct)).ToResult(Results.Ok))
            .WithTags("Books");

        var listings = app.MapGroup("/api/listings").WithTags("Listings");

        // Public search: anonymous callers pass their own position; signed-in users default to their home area.
        listings.MapGet("/", async ([AsParameters] ListingSearchRequest request, ClaimsPrincipal principal, ListingService service, CancellationToken ct) =>
                (await service.SearchAsync(request, principal.GetUserId(), ct)).ToResult(Results.Ok))
            .AllowAnonymous().Validate<ListingSearchRequest>();

        listings.MapGet("/mine", async (ClaimsPrincipal principal, ListingService service, CancellationToken ct) =>
            principal.GetUserId() is { } userId ? Results.Ok(await service.GetMineAsync(userId, ct)) : Unauthorized());

        listings.MapGet("/{id:guid}", async (Guid id, double? lat, double? lng, ClaimsPrincipal principal, ListingService service, CancellationToken ct) =>
                (await service.GetDetailsAsync(id, principal.GetUserId(), lat, lng, ct)).ToResult(Results.Ok))
            .AllowAnonymous();

        listings.MapPost("/", async (CreateListingRequest request, ClaimsPrincipal principal, ListingService service, CancellationToken ct) =>
                principal.GetUserId() is { } userId
                    ? (await service.CreateAsync(userId, request, ct)).ToResult(d => Results.Created($"/api/listings/{d.Id}", d))
                    : Unauthorized())
            .Validate<CreateListingRequest>();

        listings.MapPut("/{id:guid}", async (Guid id, UpdateListingRequest request, ClaimsPrincipal principal, ListingService service, CancellationToken ct) =>
                principal.GetUserId() is { } userId
                    ? (await service.UpdateAsync(userId, id, request, ct)).ToResult(Results.Ok)
                    : Unauthorized())
            .Validate<UpdateListingRequest>();

        listings.MapPost("/{id:guid}/archive", async (Guid id, ClaimsPrincipal principal, ListingService service, CancellationToken ct) =>
            principal.GetUserId() is { } userId
                ? (await service.ArchiveAsync(userId, id, ct)).ToResult(_ => Results.NoContent())
                : Unauthorized());

        // Bearer-token auth (no cookies), so the default antiforgery check for form posts doesn't apply.
        listings.MapPost("/{id:guid}/images", async (Guid id, IFormFile? file, ClaimsPrincipal principal, ListingService service, CancellationToken ct) =>
            {
                if (principal.GetUserId() is not { } userId)
                {
                    return Unauthorized();
                }

                if (file is null || file.Length == 0)
                {
                    return Problems.Validation(new Dictionary<string, string[]> { ["file"] = ["Choose a photo to upload."] }, ErrorCodes.InvalidImage);
                }

                if (file.Length > MaxImageBytes)
                {
                    return Problems.Validation(new Dictionary<string, string[]> { ["file"] = ["Photos can be at most 5 MB."] }, ErrorCodes.ImageTooLarge);
                }

                await using var stream = file.OpenReadStream();
                return (await service.AddImageAsync(userId, id, stream, ct))
                    .ToResult(image => Results.Created(image.DisplayUrl, image));
            })
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxImageBytes + (512 * 1024)));

        listings.MapDelete("/{id:guid}/images/{imageId:guid}", async (Guid id, Guid imageId, ClaimsPrincipal principal, ListingService service, CancellationToken ct) =>
            principal.GetUserId() is { } userId
                ? (await service.RemoveImageAsync(userId, id, imageId, ct)).ToResult(_ => Results.NoContent())
                : Unauthorized());

        // Images are immutable (new id per upload), so they can be cached forever.
        app.MapGet("/api/listing-images/{imageId:guid}/{size}", async (Guid imageId, string size, HttpResponse response, ListingService service, CancellationToken ct) =>
            {
                if (size is not ("display" or "thumb"))
                {
                    return Problems.Status(StatusCodes.Status404NotFound, "Not found.", ErrorCodes.NotFound);
                }

                var file = await service.OpenImageAsync(imageId, size == "thumb", ct);
                if (file is null)
                {
                    return Problems.Status(StatusCodes.Status404NotFound, "Not found.", ErrorCodes.NotFound);
                }

                response.Headers.CacheControl = "public, max-age=31536000, immutable";
                return Results.Stream(file.Content, file.ContentType);
            })
            .AllowAnonymous().WithTags("Listings");

        return app;
    }

    private static IResult Unauthorized() =>
        Problems.Status(StatusCodes.Status401Unauthorized, "Sign in again.", ErrorCodes.Unauthorized);
}
