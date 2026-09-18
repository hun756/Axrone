namespace Axrone.Memory.Arena;

public readonly record struct HealthReport(
    bool IsHealthy,
    string StatusMessage,
    double Utilization,
    long StalledWriteCycles,
    Exception? TerminalFault);
