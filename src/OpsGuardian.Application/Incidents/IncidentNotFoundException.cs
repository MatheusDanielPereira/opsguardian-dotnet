namespace OpsGuardian.Application.Incidents;

public sealed class IncidentNotFoundException(Guid id)
    : Exception($"Incident '{id}' was not found.");
