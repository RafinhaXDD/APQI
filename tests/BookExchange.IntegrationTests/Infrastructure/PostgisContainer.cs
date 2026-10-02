using BookExchange.IntegrationTests.Infrastructure;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgisContainer))]

namespace BookExchange.IntegrationTests.Infrastructure;

/// <summary>
/// One PostGIS container per test run. Each test class gets its own freshly created database
/// (see <see cref="ApiFixture"/>), so classes are isolated and can run in parallel (R-30).
/// </summary>
public sealed class PostgisContainer : IAsyncLifetime
{
    // Keep in sync with docker-compose.yml.
    public const string Image = "postgis/postgis:17-3.5";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"test_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
#pragma warning disable CA2100 // Name is generated above, not user input; CREATE DATABASE can't take parameters
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name }.ConnectionString;
    }
}
