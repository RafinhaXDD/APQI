using BookExchange.Domain.Shared;

namespace BookExchange.Domain.Users;

/// <summary>Public-facing details of a user. One per account, keyed by the account id.</summary>
public sealed class UserProfile
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 50;
    public const int BioMaxLength = 500;
    public const int AreaLabelMaxLength = 80;

    private UserProfile()
    {
    }

    public Guid UserId { get; private set; }

    public string DisplayName { get; private set; } = null!;

    public string? Bio { get; private set; }

    /// <summary>Approximate, human-readable area (e.g. "Campus Norte"). Safe to show publicly.</summary>
    public string? HomeAreaLabel { get; private set; }

    /// <summary>Exact home point. Never exposed to anyone but the owner.</summary>
    public GeoPoint? HomePoint { get; private set; }

    public string PreferredLanguage { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool HasHomeArea => HomePoint is not null;

    public static UserProfile Create(Guid userId, string displayName, string preferredLanguage, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("A profile needs a user.");
        }

        var profile = new UserProfile { UserId = userId, CreatedAt = now };
        profile.Update(displayName, bio: null, preferredLanguage, now);
        return profile;
    }

    public void Update(string displayName, string? bio, string preferredLanguage, DateTimeOffset now)
    {
        DisplayName = RequireLength(displayName, DisplayNameMinLength, DisplayNameMaxLength, "Display name");
        Bio = string.IsNullOrWhiteSpace(bio) ? null : RequireLength(bio, 1, BioMaxLength, "Bio");
        PreferredLanguage = SupportedLanguages.IsSupported(preferredLanguage)
            ? preferredLanguage
            : throw new DomainException($"Language '{preferredLanguage}' is not supported.");
        UpdatedAt = now;
    }

    public void SetHomeArea(string label, GeoPoint point, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(point);
        HomeAreaLabel = RequireLength(label, 1, AreaLabelMaxLength, "Area label");
        HomePoint = point;
        UpdatedAt = now;
    }

    private static string RequireLength(string? value, int min, int max, string field)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length < min || trimmed.Length > max)
        {
            throw new DomainException($"{field} must be between {min} and {max} characters.");
        }

        return trimmed;
    }
}
