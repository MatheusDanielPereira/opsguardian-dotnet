using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsGuardian.Infrastructure.Persistence;

namespace OpsGuardian.Infrastructure.BackgroundJobs;

internal sealed class OutboxDispatcherWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobOptions> options,
    ILogger<OutboxDispatcherWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogFailure =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2001, nameof(LogFailure)),
            "Outbox dispatch failed.");

    private static readonly Action<ILogger, string, Guid, Exception?> LogDispatched =
        LoggerMessage.Define<string, Guid>(
            LogLevel.Information,
            new EventId(2002, nameof(LogDispatched)),
            "Dispatched outbox event {EventType} with id {MessageId}.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.OutboxIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
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

    private async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OpsGuardianDbContext>();
        var messages = await dbContext.OutboxMessages
            .Where(message => message.ProcessedAtUtc == null)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.AttemptCount++;
            message.ProcessedAtUtc = DateTime.UtcNow;
            message.LastError = null;
            LogDispatched(logger, message.EventType, message.Id, null);
        }

        if (messages.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
