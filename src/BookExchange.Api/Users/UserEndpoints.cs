using System.Security.Claims;
using BookExchange.Api.Infrastructure;
using BookExchange.Application.Shared;
using BookExchange.Application.Users;

namespace BookExchange.Api.Users;

/// <summary>The caller's own profile under /api/users/me. Every route requires a signed-in user.</summary>
internal static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/users/me").WithTags("Users").RequireAuthorization();

        me.MapGet("/", async (ClaimsPrincipal principal, ProfileService profiles, CancellationToken ct) =>
            principal.GetUserId() is { } userId
                ? OkOrNotFound(await profiles.GetMineAsync(userId, ct))
                : Unauthorized());

        me.MapPut("/", async (UpdateProfileRequest request, ClaimsPrincipal principal, ProfileService profiles, CancellationToken ct) =>
                principal.GetUserId() is { } userId
                    ? OkOrNotFound(await profiles.UpdateAsync(userId, request, ct))
                    : Unauthorized())
            .Validate<UpdateProfileRequest>();

        me.MapPut("/home-area", async (SetHomeAreaRequest request, ClaimsPrincipal principal, ProfileService profiles, CancellationToken ct) =>
                principal.GetUserId() is { } userId
                    ? OkOrNotFound(await profiles.SetHomeAreaAsync(userId, request, ct))
                    : Unauthorized())
            .Validate<SetHomeAreaRequest>();

        return app;
    }

    private static IResult OkOrNotFound(MyProfileResponse? profile) =>
        profile is null ? Problems.Status(StatusCodes.Status404NotFound, "Profile not found.", ErrorCodes.NotFound) : Results.Ok(profile);

    private static IResult Unauthorized() =>
        Problems.Status(StatusCodes.Status401Unauthorized, "Sign in again.", ErrorCodes.Unauthorized);
}
