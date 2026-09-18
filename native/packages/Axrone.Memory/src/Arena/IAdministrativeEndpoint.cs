namespace Axrone.Memory.Arena;

public interface IAdministrativeEndpoint
{
    MetricsSnapshot GetSnapshot();
    HealthReport ExamineHealth();
    bool IsHealthy { get; }
    void Complete(Exception? error = null);
    ValueTask DrainAsync(CancellationToken cancellationToken = default);
}
