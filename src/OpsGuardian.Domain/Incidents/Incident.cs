namespace OpsGuardian.Domain.Incidents;

public sealed class Incident
{
    private Incident()
    {
    }

    private Incident(
        Guid id,
        string title,
        string description,
        IncidentSeverity severity,
        DateTime createdAtUtc)
    {
        Id = id;
        Title = NormalizeRequired(title, nameof(title), 160);
        Description = NormalizeRequired(description, nameof(description), 2_000);
        Severity = severity;
        Status = IncidentStatus.Open;
        CreatedAtUtc = RequireUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
        SlaDueAtUtc = CreatedAtUtc.Add(SlaPolicy.For(severity));
        ConcurrencyToken = Guid.NewGuid();
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public IncidentSeverity Severity { get; private set; }

    public IncidentStatus Status { get; private set; }

    public string? AssignedEngineer { get; private set; }

    public string? ResolutionSummary { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime SlaDueAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public bool IsSlaBreached { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static Incident Create(
        string title,
        string description,
        IncidentSeverity severity,
        DateTime createdAtUtc) =>
        new(Guid.NewGuid(), title, description, severity, createdAtUtc);

    public void Assign(string engineer, DateTime changedAtUtc)
    {
        EnsureNotResolved();
        AssignedEngineer = NormalizeRequired(engineer, nameof(engineer), 120);
        Touch(changedAtUtc);
    }

    public void Acknowledge(DateTime changedAtUtc)
    {
        EnsureNotResolved();
        Status = IncidentStatus.Acknowledged;
        Touch(changedAtUtc);
    }

    public void Resolve(string summary, DateTime resolvedAtUtc)
    {
        EnsureNotResolved();
        ResolutionSummary = NormalizeRequired(summary, nameof(summary), 1_000);
        ResolvedAtUtc = RequireUtc(resolvedAtUtc, nameof(resolvedAtUtc));
        Status = IncidentStatus.Resolved;
        Touch(resolvedAtUtc);
    }

    public void Reopen(DateTime reopenedAtUtc)
    {
        if (Status != IncidentStatus.Resolved)
        {
            throw new InvalidOperationException("Only resolved incidents can be reopened.");
        }

        var changedAtUtc = RequireUtc(reopenedAtUtc, nameof(reopenedAtUtc));
        Status = IncidentStatus.Open;
        ResolutionSummary = null;
        ResolvedAtUtc = null;
        IsSlaBreached = false;
        SlaDueAtUtc = changedAtUtc.Add(SlaPolicy.For(Severity));
        Touch(changedAtUtc);
    }

    public bool MarkSlaBreached(DateTime checkedAtUtc)
    {
        var changedAtUtc = RequireUtc(checkedAtUtc, nameof(checkedAtUtc));
        if (Status == IncidentStatus.Resolved || IsSlaBreached || changedAtUtc < SlaDueAtUtc)
        {
            return false;
        }

        IsSlaBreached = true;
        Touch(changedAtUtc);
        return true;
    }

    private void EnsureNotResolved()
    {
        if (Status == IncidentStatus.Resolved)
        {
            throw new InvalidOperationException("Resolved incidents cannot be changed before reopening.");
        }
    }

    private void Touch(DateTime changedAtUtc)
    {
        UpdatedAtUtc = RequireUtc(changedAtUtc, nameof(changedAtUtc));
        ConcurrencyToken = Guid.NewGuid();
    }

    private static string NormalizeRequired(string value, string parameterName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Maximum length is {maximumLength} characters.");
        }

        return normalized;
    }

    private static DateTime RequireUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Date and time must use UTC.", parameterName);
        }

        return value;
    }
}
