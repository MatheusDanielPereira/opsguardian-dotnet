namespace OpsGuardian.Infrastructure.Persistence;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}
