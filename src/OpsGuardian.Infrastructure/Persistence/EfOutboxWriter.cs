using OpsGuardian.Application.Abstractions;

namespace OpsGuardian.Infrastructure.Persistence;

internal sealed class EfOutboxWriter(OpsGuardianDbContext dbContext) : IOutboxWriter
{
    public async Task EnqueueAsync(
        string eventType,
        string payload,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Payload = payload,
            OccurredAtUtc = occurredAtUtc,
        };

        await dbContext.OutboxMessages.AddAsync(message, cancellationToken);
    }
}
