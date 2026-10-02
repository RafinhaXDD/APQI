using BookExchange.Infrastructure.Persistence;
using BookExchange.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookExchange.IntegrationTests.Database;

public sealed class DatabaseSmokeTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task All_migrations_are_applied()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        (await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken))
            .Should().Contain(m => m.EndsWith("_InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PostGIS_is_installed_and_computes_geography_distances()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var version = await db.Database
            .SqlQuery<string>($"SELECT postgis_lib_version() AS \"Value\"")
            .SingleAsync(TestContext.Current.CancellationToken);

        // Lisbon (Praça do Comércio) → Porto (Avenida dos Aliados), ~274 km as the crow flies.
        var meters = await db.Database
            .SqlQuery<double>($"""
                SELECT ST_Distance(
                    ST_SetSRID(ST_MakePoint(-9.1366, 38.7077), 4326)::geography,
                    ST_SetSRID(ST_MakePoint(-8.6110, 41.1485), 4326)::geography) AS "Value"
                """)
            .SingleAsync(TestContext.Current.CancellationToken);

        version.Should().StartWith("3.");
        meters.Should().BeInRange(270_000, 280_000);
    }

    [Fact]
    public async Task Outbox_table_exists()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.OutboxMessages.CountAsync(TestContext.Current.CancellationToken)).Should().BeGreaterThanOrEqualTo(0);
    }
}
