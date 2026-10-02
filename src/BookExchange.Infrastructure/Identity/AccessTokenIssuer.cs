using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BookExchange.Infrastructure.Identity;

public static class AppClaims
{
    public const string Subject = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;

    /// <summary>Refresh-token family of the session that issued the token (lets "change password" keep it).</summary>
    public const string SessionId = "sid";
}

public sealed class AccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock)
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset ExpiresAt) Issue(AppUser user, Guid sessionId)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now + settings.AccessTokenLifetime;

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(AppClaims.Subject, user.Id.ToString()),
                new Claim(AppClaims.Email, user.Email ?? string.Empty),
                new Claim(AppClaims.SessionId, sessionId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(settings.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        });

        return (token, expiresAt);
    }
}
