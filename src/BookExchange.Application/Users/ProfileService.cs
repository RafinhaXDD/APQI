using BookExchange.Application.Abstractions;
using BookExchange.Application.Credits;
using BookExchange.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace BookExchange.Application.Users;

/// <param name="Latitude">Exact point: returned only to the owner (R-14).</param>
public sealed record HomeAreaDto(string Label, double Latitude, double Longitude);

public sealed record MyProfileResponse(
    Guid UserId,
    string DisplayName,
    string? Bio,
    string PreferredLanguage,
    HomeAreaDto? HomeArea,
    CreditBalance Credits);

public sealed record UpdateProfileRequest(string DisplayName, string? Bio, string PreferredLanguage);

public sealed record SetHomeAreaRequest(string Label, double Latitude, double Longitude);

public sealed class ProfileService(IAppDbContext db, CreditLedger ledger, TimeProvider clock)
{
    public async Task<MyProfileResponse?> GetMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var homeArea = profile.HomePoint is null || profile.HomeAreaLabel is null
            ? null
            : new HomeAreaDto(profile.HomeAreaLabel, profile.HomePoint.Latitude, profile.HomePoint.Longitude);

        return new MyProfileResponse(
            profile.UserId,
            profile.DisplayName,
            profile.Bio,
            profile.PreferredLanguage,
            homeArea,
            await ledger.GetBalanceAsync(userId, cancellationToken));
    }

    public async Task<MyProfileResponse?> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        profile.Update(request.DisplayName, request.Bio, request.PreferredLanguage, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return await GetMineAsync(userId, cancellationToken);
    }

    public async Task<MyProfileResponse?> SetHomeAreaAsync(Guid userId, SetHomeAreaRequest request, CancellationToken cancellationToken)
    {
        var profile = await db.UserProfiles.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        profile.SetHomeArea(request.Label, new GeoPoint(request.Latitude, request.Longitude), clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return await GetMineAsync(userId, cancellationToken);
    }
}
