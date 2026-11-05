using OpsGuardian.Application.Abstractions;

namespace OpsGuardian.Infrastructure.Time;

internal sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
