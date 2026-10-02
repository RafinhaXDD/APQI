using BookExchange.Application.Shared.Outbox;
using BookExchange.Infrastructure.Health;
using BookExchange.Infrastructure.Outbox;
using BookExchange.Infrastructure.Persistence;
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

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyHealthTag]);

        return services;
    }
}
