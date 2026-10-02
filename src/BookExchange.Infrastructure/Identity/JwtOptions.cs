using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BookExchange.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "aqpi-api";

    public string Audience { get; set; } = "aqpi-web";

    /// <summary>HMAC-SHA256 key, at least 32 bytes. Comes from configuration/secrets, never from source.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));

    public bool IsValid() =>
        Encoding.UTF8.GetByteCount(SigningKey) >= 32
        && AccessTokenLifetime > TimeSpan.Zero
        && RefreshTokenLifetime > AccessTokenLifetime;
}
