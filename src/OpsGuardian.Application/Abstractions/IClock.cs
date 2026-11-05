namespace OpsGuardian.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
