namespace OpsGuardian.Application.Abstractions;

public interface IOutboxWriter
{
    Task EnqueueAsync(
        string eventType,
        string payload,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken);
}
