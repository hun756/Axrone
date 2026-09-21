namespace Axrone.Utility.Backoff;

public enum BackoffHealthStatus : byte
{
    Healthy = 0,
    Degraded = 1,
    Unhealthy = 2
}
