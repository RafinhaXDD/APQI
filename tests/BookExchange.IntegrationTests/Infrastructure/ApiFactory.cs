using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BookExchange.IntegrationTests.Infrastructure;

public sealed class ApiFactory(string connectionString, Action<IServiceCollection>? configureServices = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", connectionString);

        // Tests drive the outbox processor directly instead of racing the background loop.
        builder.UseSetting("Outbox:Enabled", "false");

        if (configureServices is not null)
        {
            builder.ConfigureTestServices(configureServices);
        }
    }
}
