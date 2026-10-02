using Microsoft.AspNetCore.Identity;

namespace BookExchange.Infrastructure.Identity;

/// <summary>Login account (ASP.NET Core Identity). Public details live in <c>UserProfile</c>.</summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    public DateTimeOffset CreatedAt { get; set; }
}
