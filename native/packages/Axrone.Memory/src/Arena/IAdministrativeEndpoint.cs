namespace Axrone.Memory.Arena;

public interface IAdministrativeEndpoint
{
    HealthReport GetHealthReport();

    MetricsSnapshot GetMetricsSnapshot();

    void RequestDrain(CancellationToken cancellationToken = default);
}
