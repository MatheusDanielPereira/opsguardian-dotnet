using OpsGuardian.Domain.Incidents;

namespace OpsGuardian.Application.Incidents;

public sealed record CreateIncidentCommand(
    string Title,
    string Description,
    IncidentSeverity Severity,
    string? AssignedEngineer);

public sealed record IncidentSummary(
    Guid Id,
    string Title,
    IncidentSeverity Severity,
    IncidentStatus Status,
    string? AssignedEngineer,
    DateTime SlaDueAtUtc,
    bool IsSlaBreached);

public sealed record IncidentDetails(
    Guid Id,
    string Title,
    string Description,
    IncidentSeverity Severity,
    IncidentStatus Status,
    string? AssignedEngineer,
    string? ResolutionSummary,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime SlaDueAtUtc,
    DateTime? ResolvedAtUtc,
    bool IsSlaBreached);

public sealed record DashboardSnapshot(
    int Total,
    int Open,
    int Acknowledged,
    int Resolved,
    int Breached,
    int DueSoon,
    IReadOnlyList<IncidentSummary> Incidents);
