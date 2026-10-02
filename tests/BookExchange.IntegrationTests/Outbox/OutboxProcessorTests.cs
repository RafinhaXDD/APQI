using System.Text.Json;
using BookExchange.Application.Shared.Outbox;
using BookExchange.Infrastructure.Outbox;
using BookExchange.Infrastructure.Persistence;
using BookExchange.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BookExchange.IntegrationTests.Outbox;

public sealed class OutboxFixture(PostgisContainer postgis) : ApiFixture(postgis)
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<HandledMessages>();
        services.AddScoped<IOutboxMessageHandler, SucceedingHandler>();
        services.AddScoped<IOutboxMessageHandler, FailingHandler>();
        services.AddScoped<IOutboxMessageHandler, WritesThenFailsHandler>();
    }
}

public sealed class OutboxProcessorTests(OutboxFixture fixture) : IClassFixture<OutboxFixture>, IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceProvider Services => fixture.Factory.Services;

    private HandledMessages Handled => Services.GetRequiredService<HandledMessages>();

    private OutboxProcessor Processor => Services.GetRequiredService<OutboxProcessor>();

    // Tests in a class share one database: start each test from an empty outbox.
    public async ValueTask InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages.ExecuteDeleteAsync();
        Handled.Clear();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Enqueued_message_is_handled_once_and_marked_processed()
    {
        var id = await EnqueueAsync(SucceedingHandler.Type, new { BookId = 42 });

        var attempted = await Processor.ProcessPendingAsync(Ct);
        var again = await Processor.ProcessPendingAsync(Ct);

        attempted.Should().Be(1);
        again.Should().Be(0);
        var envelope = Handled.All.Should().ContainSingle().Subject;
        envelope.Id.Should().Be(id);
        envelope.Attempt.Should().Be(1);
        using (var payload = JsonDocument.Parse(envelope.Payload))
        {
            payload.RootElement.GetProperty("bookId").GetInt32().Should().Be(42);
        }

        var stored = await LoadAsync(id);
        stored.ProcessedAt.Should().NotBeNull();
        stored.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task Message_enqueued_in_a_rolled_back_transaction_is_never_stored()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            scope.ServiceProvider.GetRequiredService<IOutbox>().Enqueue(SucceedingHandler.Type, new { });
            await db.SaveChangesAsync(Ct);
            await transaction.RollbackAsync(Ct);
        }

        (await CountAsync()).Should().Be(0);
        (await Processor.ProcessPendingAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task Failing_handler_records_the_attempt_and_schedules_a_retry_with_backoff()
    {
        var id = await EnqueueAsync(FailingHandler.Type, new { });
        var before = DateTimeOffset.UtcNow;

        await Processor.ProcessPendingAsync(Ct);
        var secondRun = await Processor.ProcessPendingAsync(Ct);

        var stored = await LoadAsync(id);
        stored.Attempts.Should().Be(1);
        stored.LastError.Should().Be("InvalidOperationException: boom");
        stored.ProcessedAt.Should().BeNull();
        stored.DeadLetteredAt.Should().BeNull();
        stored.NextAttemptAt.Should().BeAfter(before.AddSeconds(1), "first backoff is 2 s");
        secondRun.Should().Be(0, "the message is not due again until its backoff elapses");
    }

    [Fact]
    public async Task Database_writes_of_a_failing_handler_are_rolled_back()
    {
        var id = await EnqueueAsync(WritesThenFailsHandler.Type, new { });

        await Processor.ProcessPendingAsync(Ct);

        (await CountAsync()).Should().Be(1, "the message the handler saved before failing must not survive");
        (await LoadAsync(id)).Attempts.Should().Be(1);
    }

    [Fact]
    public async Task Message_without_a_handler_is_recorded_as_failed()
    {
        var id = await EnqueueAsync("test.nobody-handles-this", new { });

        await Processor.ProcessPendingAsync(Ct);

        var stored = await LoadAsync(id);
        stored.Attempts.Should().Be(1);
        stored.LastError.Should().Contain("No outbox handler");
        stored.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task Message_is_dead_lettered_after_max_attempts_and_never_retried()
    {
        var id = await EnqueueAsync(FailingHandler.Type, new { });
        var processor = CreateProcessor(new OutboxOptions { MaxAttempts = 1 });

        await processor.ProcessPendingAsync(Ct);

        var stored = await LoadAsync(id);
        stored.Attempts.Should().Be(1);
        stored.DeadLetteredAt.Should().NotBeNull();
        stored.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task Concurrent_processors_handle_each_message_exactly_once()
    {
        const int messageCount = 40;
        await using (var scope = Services.CreateAsyncScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
            for (var i = 0; i < messageCount; i++)
            {
                outbox.Enqueue(SucceedingHandler.Type, new { Index = i });
            }

            await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync(Ct);
        }

        var processors = Enumerable.Range(0, 4).Select(_ => CreateProcessor(new OutboxOptions { BatchSize = messageCount }));
        var attempted = await Task.WhenAll(processors.Select(p => p.ProcessPendingAsync(Ct)));

        attempted.Sum().Should().Be(messageCount);
        Handled.All.Should().HaveCount(messageCount);
        Handled.All.Select(m => m.Id).Should().OnlyHaveUniqueItems();
        await using var verify = Services.CreateAsyncScope();
        (await verify.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages
            .CountAsync(m => m.ProcessedAt == null, Ct)).Should().Be(0);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 8)]
    [InlineData(9, 512)]
    [InlineData(10, 900)]
    [InlineData(30, 900)]
    public void Backoff_doubles_per_attempt_and_is_capped(int attempts, int expectedSeconds)
    {
        new OutboxOptions().BackoffFor(attempts).Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    private OutboxProcessor CreateProcessor(OutboxOptions options) => new(
        Services.GetRequiredService<IServiceScopeFactory>(),
        TimeProvider.System,
        Options.Create(options),
        NullLogger<OutboxProcessor>.Instance);

    private async Task<Guid> EnqueueAsync(string type, object payload)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        scope.ServiceProvider.GetRequiredService<IOutbox>().Enqueue(type, payload);
        await db.SaveChangesAsync(Ct);
        return await db.OutboxMessages.Select(m => m.Id).SingleAsync(Ct);
    }

    private async Task<OutboxMessage> LoadAsync(Guid id)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages
            .AsNoTracking()
            .SingleAsync(m => m.Id == id, Ct);
    }

    private async Task<int> CountAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages.CountAsync(Ct);
    }
}
