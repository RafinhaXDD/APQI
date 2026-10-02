using BookExchange.Application.Abstractions;
using BookExchange.Application.Auth;
using BookExchange.Application.Shared.Outbox;
using BookExchange.Infrastructure.Email;
using BookExchange.Infrastructure.Health;
using BookExchange.Infrastructure.Identity;
using BookExchange.Infrastructure.Outbox;
using BookExchange.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BookExchange.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "Database";
    public const string ReadyHealthTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DatabaseConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{DatabaseConnectionName}' is not configured.");

        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.UseNetTopologySuite()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        AddOutbox(services, configuration);
        AddIdentity(services, configuration);
        AddEmail(services, configuration);

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyHealthTag]);

        return services;
    }

    private static void AddOutbox(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .Validate(o => o.PollInterval > TimeSpan.Zero && o.BatchSize > 0 && o.MaxAttempts > 0, "Invalid Outbox options.")
            .ValidateOnStart();
        services.AddScoped<IOutbox, EfOutbox>();
        services.AddSingleton<OutboxProcessor>();
        if (configuration.GetSection(OutboxOptions.SectionName).GetValue(nameof(OutboxOptions.Enabled), defaultValue: true))
        {
            services.AddHostedService<OutboxBackgroundService>();
        }
    }

    private static void AddIdentity(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.IsValid(), "Jwt:SigningKey must be at least 32 bytes and lifetimes positive.")
            .ValidateOnStart();

        // Password rules mirror the request validators exactly, so Identity never rejects a password the
        // validator accepted (a difference between paths would leak which emails exist).
        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = PasswordPolicy.MinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // Keys protect email/reset tokens; persisted so links survive restarts (Blob in production, PLAN Q2).
        var dataProtection = services.AddDataProtection().SetApplicationName("aqpi");
        var keysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        services.AddSingleton<PasswordTimingGuard>();
        services.AddSingleton<AccessTokenIssuer>();
        services.AddScoped<AuthService>();
    }

    private static void AddEmail(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>().Bind(configuration.GetSection(EmailOptions.SectionName));
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IOutboxMessageHandler, ConfirmEmailHandler>();
        services.AddScoped<IOutboxMessageHandler, PasswordResetHandler>();
        services.AddScoped<IOutboxMessageHandler, AccountExistsHandler>();
    }
}
