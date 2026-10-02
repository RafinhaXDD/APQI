using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookExchange.Infrastructure.Identity;

public enum RefreshTokenRevocation
{
    Rotated,
    ReuseDetected,
    Logout,
    PasswordChanged,
}

/// <summary>
/// Opaque, single-use refresh token (R-18). Only a SHA-256 hash is stored. Every token belongs to a
/// family (one login); presenting an already-rotated token revokes the whole family.
/// </summary>
public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid FamilyId { get; private set; }

    public byte[] TokenHash { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public RefreshTokenRevocation? RevokedReason { get; private set; }

    public Guid? ReplacedById { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin): two refreshes with one token → only one wins.</summary>
    public uint Version { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    /// <returns>The entity to store and the raw token to hand to the client (never stored).</returns>
    public static (RefreshToken Token, string RawToken) Issue(Guid userId, Guid familyId, DateTimeOffset now, TimeSpan lifetime)
    {
        var raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var token = new RefreshToken
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = Hash(raw),
            CreatedAt = now,
            ExpiresAt = now + lifetime,
        };
        return (token, raw);
    }

    /// <summary>High-entropy random token, so a plain SHA-256 (no salt or stretching) is sufficient.</summary>
    public static byte[] Hash(string rawToken) => SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

    public void Revoke(DateTimeOffset now, RefreshTokenRevocation reason, Guid? replacedById = null)
    {
        RevokedAt = now;
        RevokedReason = reason;
        ReplacedById = replacedById;
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.TokenHash).HasMaxLength(32).IsRequired();
        builder.Property(t => t.RevokedReason).HasConversion<string>().HasMaxLength(32);
        builder.Property(t => t.Version).IsRowVersion();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);
        builder.HasIndex(t => t.FamilyId);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
