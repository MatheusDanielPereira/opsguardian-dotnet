using Microsoft.EntityFrameworkCore;
using OpsGuardian.Application.Abstractions;
using OpsGuardian.Domain.Incidents;

namespace OpsGuardian.Infrastructure.Persistence;

internal sealed class IncidentRepository(OpsGuardianDbContext dbContext) : IIncidentRepository
{
    public async Task AddAsync(Incident incident, CancellationToken cancellationToken) =>
        await dbContext.Incidents.AddAsync(incident, cancellationToken);

    public Task<Incident?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Incidents.SingleOrDefaultAsync(incident => incident.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Incident>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Incidents
            .AsNoTracking()
            .OrderByDescending(incident => incident.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Incident>> ListOverdueAsync(
        DateTime utcNow,
        CancellationToken cancellationToken) =>
        await dbContext.Incidents
            .Where(incident =>
                incident.Status != IncidentStatus.Resolved &&
                !incident.IsSlaBreached &&
                incident.SlaDueAtUtc <= utcNow)
            .ToListAsync(cancellationToken);
}
