using OpsGuardian.Domain.Incidents;

namespace OpsGuardian.Domain.Tests;

public sealed class IncidentTests
{
    private static readonly DateTime ReferenceTime = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateCalculatesSlaAndNormalizesContent()
    {
        var incident = Incident.Create(
            "  API unavailable  ",
            "  The checkout API is not responding.  ",
            IncidentSeverity.Critical,
            ReferenceTime);

        Assert.Equal("API unavailable", incident.Title);
        Assert.Equal("The checkout API is not responding.", incident.Description);
        Assert.Equal(ReferenceTime.AddHours(1), incident.SlaDueAtUtc);
        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.False(incident.IsSlaBreached);
    }

    [Fact]
    public void CreateRejectsNonUtcTimestamp()
    {
        var localTime = DateTime.SpecifyKind(ReferenceTime, DateTimeKind.Local);

        var exception = Assert.Throws<ArgumentException>(() =>
            Incident.Create("Title", "Description", IncidentSeverity.Low, localTime));

        Assert.Contains("UTC", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AssignSetsOwnerAndChangesConcurrencyToken()
    {
        var incident = CreateIncident();
        var originalToken = incident.ConcurrencyToken;

        incident.Assign("  Platform Team  ", ReferenceTime.AddMinutes(5));

        Assert.Equal("Platform Team", incident.AssignedEngineer);
        Assert.NotEqual(originalToken, incident.ConcurrencyToken);
    }

    [Fact]
    public void AcknowledgeMovesOpenIncidentToAcknowledged()
    {
        var incident = CreateIncident();

        incident.Acknowledge(ReferenceTime.AddMinutes(10));

        Assert.Equal(IncidentStatus.Acknowledged, incident.Status);
    }

    [Fact]
    public void ResolveRecordsSummaryAndTimestamp()
    {
        var incident = CreateIncident();
        var resolvedAt = ReferenceTime.AddMinutes(30);

        incident.Resolve("Service recovered after rollback.", resolvedAt);

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal(resolvedAt, incident.ResolvedAtUtc);
        Assert.Equal("Service recovered after rollback.", incident.ResolutionSummary);
    }

    [Fact]
    public void ResolvedIncidentCannotBeAssigned()
    {
        var incident = CreateIncident();
        incident.Resolve("Recovered.", ReferenceTime.AddMinutes(30));

        Assert.Throws<InvalidOperationException>(() =>
            incident.Assign("New owner", ReferenceTime.AddMinutes(31)));
    }

    [Fact]
    public void ReopenResetsResolutionAndCreatesNewSlaWindow()
    {
        var incident = CreateIncident(IncidentSeverity.High);
        incident.Resolve("Recovered.", ReferenceTime.AddMinutes(30));
        var reopenedAt = ReferenceTime.AddHours(2);

        incident.Reopen(reopenedAt);

        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.Null(incident.ResolutionSummary);
        Assert.Null(incident.ResolvedAtUtc);
        Assert.Equal(reopenedAt.AddHours(4), incident.SlaDueAtUtc);
    }

    [Fact]
    public void MarkSlaBreachedChangesOnlyOverdueActiveIncidents()
    {
        var incident = CreateIncident(IncidentSeverity.Critical);

        var beforeDeadline = incident.MarkSlaBreached(ReferenceTime.AddMinutes(59));
        var afterDeadline = incident.MarkSlaBreached(ReferenceTime.AddHours(1));
        var repeated = incident.MarkSlaBreached(ReferenceTime.AddHours(2));

        Assert.False(beforeDeadline);
        Assert.True(afterDeadline);
        Assert.False(repeated);
        Assert.True(incident.IsSlaBreached);
    }

    [Theory]
    [InlineData(IncidentSeverity.Critical, 1)]
    [InlineData(IncidentSeverity.High, 4)]
    [InlineData(IncidentSeverity.Medium, 12)]
    [InlineData(IncidentSeverity.Low, 24)]
    public void SlaPolicyMapsSeverityToExpectedHours(IncidentSeverity severity, int hours)
    {
        Assert.Equal(TimeSpan.FromHours(hours), SlaPolicy.For(severity));
    }

    private static Incident CreateIncident(IncidentSeverity severity = IncidentSeverity.Medium) =>
        Incident.Create("Service degradation", "Latency is above the expected threshold.", severity, ReferenceTime);
}
