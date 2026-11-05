namespace OpsGuardian.Infrastructure.BackgroundJobs;

public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    public bool Enabled { get; set; } = true;

    public int SlaMonitorIntervalSeconds { get; set; } = 30;

    public int OutboxIntervalSeconds { get; set; } = 10;
}
