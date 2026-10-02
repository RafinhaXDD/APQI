using BookExchange.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookExchange.IntegrationTests.Infrastructure;

public sealed class ApiFactory(
    string connectionString,
    Action<IServiceCollection>? configureServices = null,
    IReadOnlyDictionary<string, string>? settings = null)
    : WebApplicationFactory<Program>
{
    public const string TestSigningKey = "test-signing-key-for-integration-tests-only-0123456789";

    public FakeEmailSender Emails { get; } = new();

    public FakeBookMetadataProvider BookMetadata { get; } = new();

    /// <summary>Uploaded photos for this factory only; deleted on dispose.</summary>
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "aqpi-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", connectionString);
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("Email:AppBaseUrl", "http://app.test");
        builder.UseSetting("Storage:Provider", "LocalDisk");
        builder.UseSetting("Storage:LocalRootPath", StorageRoot);

        // Tests drive the outbox processor directly instead of racing the background loop.
        builder.UseSetting("Outbox:Enabled", "false");

        // All test requests come from one "IP"; only the rate-limit tests lower these.
        builder.UseSetting("RateLimiting:AuthPermitLimit", "100000");
        builder.UseSetting("RateLimiting:RefreshPermitLimit", "100000");

        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
            services.RemoveAll<IBookMetadataProvider>();
            services.AddSingleton<IBookMetadataProvider>(BookMetadata);
            configureServices?.Invoke(services);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(StorageRoot))
        {
            Directory.Delete(StorageRoot, recursive: true);
        }
    }
}
