namespace Axrone.Utility.Backoff;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct BackoffMetricsSnapshot(
    ulong TotalExecutedSteps,
    ulong SpinPhaseSteps,
    ulong YieldPhaseSteps,
    ulong SleepPhaseSteps,
    ulong AsyncPhaseSteps,
    long ActiveWorkerContention,
    bool IsFaulted,
    bool IsDrained);
