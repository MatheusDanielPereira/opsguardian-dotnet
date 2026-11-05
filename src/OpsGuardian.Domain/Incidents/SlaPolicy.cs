namespace OpsGuardian.Domain.Incidents;

public static class SlaPolicy
{
    public static TimeSpan For(IncidentSeverity severity) => severity switch
    {
        IncidentSeverity.Critical => TimeSpan.FromHours(1),
        IncidentSeverity.High => TimeSpan.FromHours(4),
        IncidentSeverity.Medium => TimeSpan.FromHours(12),
        IncidentSeverity.Low => TimeSpan.FromHours(24),
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown severity."),
    };
}
