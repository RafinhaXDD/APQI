using BookExchange.Infrastructure.Outbox;
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
        Factory = new ApiFactory(ConnectionString, ConfigureServices, Settings);

        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    public AuthClient CreateAuthClient() => new(Factory.CreateClient(), this);

    /// <summary>Delivers queued emails (the outbox loop is off in tests).</summary>
    public Task<int> RunOutboxAsync() =>
        Factory.Services.GetRequiredService<OutboxProcessor>().ProcessPendingAsync(TestContext.Current.CancellationToken);

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected virtual IReadOnlyDictionary<string, string>? Settings => null;

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }
}
