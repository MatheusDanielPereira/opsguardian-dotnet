using OpsGuardian.Domain.Incidents;

namespace OpsGuardian.Application.Abstractions;

public interface IIncidentRepository
{
    Task AddAsync(Incident incident, CancellationToken cancellationToken);

    Task<Incident?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Incident>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Incident>> ListOverdueAsync(DateTime utcNow, CancellationToken cancellationToken);
}
