using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsGuardian.Application.Incidents;

namespace OpsGuardian.Infrastructure.BackgroundJobs;

internal sealed class SlaMonitorWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobOptions> options,
    ILogger<SlaMonitorWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> LogBreached =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            new EventId(1001, nameof(LogBreached)),
            "SLA monitor marked {IncidentCount} incident(s) as breached.");

    private static readonly Action<ILogger, Exception?> LogFailure =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1002, nameof(LogFailure)),
            "SLA monitor execution failed.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.SlaMonitorIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IncidentService>();
                var changed = await service.MarkOverdueIncidentsAsync(stoppingToken);
                if (changed > 0)
                {
                    LogBreached(logger, changed, null);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogFailure(logger, exception);
            }
        }
    }
}
