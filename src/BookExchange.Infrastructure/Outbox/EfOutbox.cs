using System.Text.Json;
using BookExchange.Application.Shared.Outbox;
using BookExchange.Infrastructure.Persistence;

namespace BookExchange.Infrastructure.Outbox;

internal sealed class EfOutbox(AppDbContext db, TimeProvider clock) : IOutbox
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Enqueue<TPayload>(string type, TPayload payload)
        where TPayload : notnull
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        db.OutboxMessages.Add(OutboxMessage.Create(type, json, clock.GetUtcNow()));
    }
}
