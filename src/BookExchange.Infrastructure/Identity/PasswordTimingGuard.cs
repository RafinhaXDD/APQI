using Microsoft.AspNetCore.Identity;

namespace BookExchange.Infrastructure.Identity;

/// <summary>
/// Spends the same password-hashing time on paths where no real account is involved (unknown email
/// at login, existing email at registration), so response times don't reveal which emails exist (R-19).
/// </summary>
public sealed class PasswordTimingGuard
{
    private static readonly AppUser Nobody = new();
    private readonly PasswordHasher<AppUser> _hasher = new();
    private readonly string _dummyHash;

    public PasswordTimingGuard() => _dummyHash = _hasher.HashPassword(Nobody, Guid.NewGuid().ToString());

    public void SimulateVerify(string password) => _hasher.VerifyHashedPassword(Nobody, _dummyHash, password);

    public void SimulateHash(string password) => _hasher.HashPassword(Nobody, password);
}
