using Microsoft.EntityFrameworkCore;
using OpsGuardian.Application.Abstractions;
using OpsGuardian.Domain.Incidents;

namespace OpsGuardian.Infrastructure.Persistence;

public sealed class OpsGuardianDbContext(DbContextOptions<OpsGuardianDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpsGuardianDbContext).Assembly);
    }
}
