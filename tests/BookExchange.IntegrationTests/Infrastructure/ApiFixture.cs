using BookExchange.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookExchange.IntegrationTests.Infrastructure;

/// <summary>Class fixture: a new database with all migrations applied, plus an in-memory API host.</summary>
public class ApiFixture(PostgisContainer postgis) : IAsyncLifetime
{
    public ApiFactory Factory { get; private set; } = null!;

    public string ConnectionString { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await postgis.CreateDatabaseAsync();
        Factory = new ApiFactory(ConnectionString, ConfigureServices);

        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }
}
