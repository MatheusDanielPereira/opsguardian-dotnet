using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsGuardian.Domain.Incidents;

namespace OpsGuardian.Infrastructure.Persistence.Configurations;

internal sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("Incidents");
        builder.HasKey(incident => incident.Id);

        builder.Property(incident => incident.Title).HasMaxLength(160).IsRequired();
        builder.Property(incident => incident.Description).HasMaxLength(2_000).IsRequired();
        builder.Property(incident => incident.AssignedEngineer).HasMaxLength(120);
        builder.Property(incident => incident.ResolutionSummary).HasMaxLength(1_000);
        builder.Property(incident => incident.Severity).HasConversion<string>().HasMaxLength(16);
        builder.Property(incident => incident.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(incident => incident.ConcurrencyToken).IsConcurrencyToken();

        builder.HasIndex(incident => new { incident.Status, incident.SlaDueAtUtc });
        builder.HasIndex(incident => incident.IsSlaBreached);
    }
}
