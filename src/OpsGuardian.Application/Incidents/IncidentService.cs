using System.Text.Json;
using OpsGuardian.Application.Abstractions;
using OpsGuardian.Domain.Incidents;

namespace OpsGuardian.Application.Incidents;

public sealed class IncidentService(
    IIncidentRepository repository,
    IOutboxWriter outbox,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Guid> CreateAsync(CreateIncidentCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var incident = Incident.Create(command.Title, command.Description, command.Severity, now);
        if (!string.IsNullOrWhiteSpace(command.AssignedEngineer))
        {
            incident.Assign(command.AssignedEngineer, now);
        }

        await repository.AddAsync(incident, cancellationToken);
        await EnqueueAsync("incident.created", incident, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return incident.Id;
    }

    public async Task<DashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var incidents = await repository.ListAsync(cancellationToken);
        var summaries = incidents
            .OrderBy(incident => incident.Status == IncidentStatus.Resolved)
            .ThenByDescending(incident => incident.IsSlaBreached)
            .ThenBy(incident => incident.SlaDueAtUtc)
            .Select(ToSummary)
            .ToArray();

        return new DashboardSnapshot(
            incidents.Count,
            incidents.Count(incident => incident.Status == IncidentStatus.Open),
            incidents.Count(incident => incident.Status == IncidentStatus.Acknowledged),
            incidents.Count(incident => incident.Status == IncidentStatus.Resolved),
            incidents.Count(incident => incident.IsSlaBreached),
            incidents.Count(incident =>
                incident.Status != IncidentStatus.Resolved &&
                !incident.IsSlaBreached &&
                incident.SlaDueAtUtc <= now.AddHours(2)),
            summaries);
    }

    public async Task<IncidentDetails> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToDetails(await GetRequiredAsync(id, cancellationToken));

    public async Task AssignAsync(Guid id, string engineer, CancellationToken cancellationToken)
    {
        var incident = await GetRequiredAsync(id, cancellationToken);
        var now = clock.UtcNow;
        incident.Assign(engineer, now);
        await EnqueueAsync("incident.assigned", incident, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AcknowledgeAsync(Guid id, CancellationToken cancellationToken)
    {
        var incident = await GetRequiredAsync(id, cancellationToken);
        var now = clock.UtcNow;
        incident.Acknowledge(now);
        await EnqueueAsync("incident.acknowledged", incident, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolveAsync(Guid id, string summary, CancellationToken cancellationToken)
    {
        var incident = await GetRequiredAsync(id, cancellationToken);
        var now = clock.UtcNow;
        incident.Resolve(summary, now);
        await EnqueueAsync("incident.resolved", incident, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ReopenAsync(Guid id, CancellationToken cancellationToken)
    {
        var incident = await GetRequiredAsync(id, cancellationToken);
        var now = clock.UtcNow;
        incident.Reopen(now);
        await EnqueueAsync("incident.reopened", incident, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> MarkOverdueIncidentsAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var incidents = await repository.ListOverdueAsync(now, cancellationToken);
        var changed = 0;
        foreach (var incident in incidents)
        {
            if (!incident.MarkSlaBreached(now))
            {
                continue;
            }

            changed++;
            await EnqueueAsync("incident.sla-breached", incident, now, cancellationToken);
        }

        if (changed > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }

    private async Task<Incident> GetRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetAsync(id, cancellationToken) ?? throw new IncidentNotFoundException(id);

    private Task EnqueueAsync(
        string eventType,
        Incident incident,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            incident.Id,
            incident.Title,
            Severity = incident.Severity.ToString(),
            Status = incident.Status.ToString(),
            incident.AssignedEngineer,
            incident.SlaDueAtUtc,
            incident.IsSlaBreached,
        });

        return outbox.EnqueueAsync(eventType, payload, occurredAtUtc, cancellationToken);
    }

    private static IncidentSummary ToSummary(Incident incident) => new(
        incident.Id,
        incident.Title,
        incident.Severity,
        incident.Status,
        incident.AssignedEngineer,
        incident.SlaDueAtUtc,
        incident.IsSlaBreached);

    private static IncidentDetails ToDetails(Incident incident) => new(
        incident.Id,
        incident.Title,
        incident.Description,
        incident.Severity,
        incident.Status,
        incident.AssignedEngineer,
        incident.ResolutionSummary,
        incident.CreatedAtUtc,
        incident.UpdatedAtUtc,
        incident.SlaDueAtUtc,
        incident.ResolvedAtUtc,
        incident.IsSlaBreached);
}
