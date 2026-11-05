using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpsGuardian.Application.Abstractions;
using OpsGuardian.Application.Incidents;
using OpsGuardian.Infrastructure.BackgroundJobs;
using OpsGuardian.Infrastructure.Persistence;
using OpsGuardian.Infrastructure.Time;

namespace OpsGuardian.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOpsGuardianInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OpsGuardian");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'OpsGuardian' is required. Use user-secrets or an environment variable outside development.");
        }

        var provider = configuration["Database:Provider"] ?? "SqlServer";
        services.AddDbContext<OpsGuardianDbContext>(options =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
                return;
            }

            if (!provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unsupported database provider '{provider}'.");
            }

            options.UseSqlServer(connectionString, sql =>
                sql.EnableRetryOnFailure(maxRetryCount: 3));
        });

        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddScoped<IOutboxWriter, EfOutboxWriter>();
        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<OpsGuardianDbContext>());
        services.AddScoped<IncidentService>();
        services.AddSingleton<IClock, SystemClock>();

        services.Configure<BackgroundJobOptions>(
            configuration.GetSection(BackgroundJobOptions.SectionName));
        if (configuration.GetValue("BackgroundJobs:Enabled", true))
        {
            services.AddHostedService<SlaMonitorWorker>();
            services.AddHostedService<OutboxDispatcherWorker>();
        }

        return services;
    }
}
