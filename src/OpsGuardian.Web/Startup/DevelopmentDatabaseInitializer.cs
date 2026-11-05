using Microsoft.EntityFrameworkCore;
using OpsGuardian.Domain.Incidents;
using OpsGuardian.Infrastructure.Persistence;

namespace OpsGuardian.Web.Startup;

public static class DevelopmentDatabaseInitializer
{
    public static async Task InitializeAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment() ||
            !app.Configuration.GetValue("Database:Initialize", false))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OpsGuardianDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        if (await dbContext.Incidents.AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;
        var critical = Incident.Create(
            "Order integration is timing out",
            "The downstream order gateway has exceeded its latency threshold for five consecutive checks.",
            IncidentSeverity.Critical,
            now.AddMinutes(-90));
        critical.Assign("Demo Operator", now.AddMinutes(-85));
        critical.MarkSlaBreached(now);

        var high = Incident.Create(
            "Invoice queue requires investigation",
            "Messages are accumulating and need an owner before the service window closes.",
            IncidentSeverity.High,
            now.AddMinutes(-45));
        high.Assign("Platform Team", now.AddMinutes(-40));
        high.Acknowledge(now.AddMinutes(-35));

        var resolved = Incident.Create(
            "Customer portal authentication recovered",
            "Authentication requests briefly failed after a provider configuration update.",
            IncidentSeverity.Medium,
            now.AddHours(-3));
        resolved.Assign("Identity Team", now.AddHours(-3));
        resolved.Resolve("Configuration was rolled back and health checks are stable.", now.AddHours(-2));

        await dbContext.Incidents.AddRangeAsync(critical, high, resolved);
        await dbContext.SaveChangesAsync();
    }
}
