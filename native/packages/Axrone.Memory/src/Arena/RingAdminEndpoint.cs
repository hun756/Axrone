namespace Axrone.Memory.Arena;

public sealed class RingAdminEndpoint<T, TBackoff> : IAdministrativeEndpoint
    where T : unmanaged
    where TBackoff : struct, IBackoffPolicy
{
    private readonly RingCore<T, TBackoff> _core;

    internal RingAdminEndpoint(RingCore<T, TBackoff> core) => _core = core;

    public bool IsHealthy => !_core.Lifecycle.IsFaulted && !_core.Lifecycle.IsCompleted;

    public MetricsSnapshot GetSnapshot()
    {
        long headCommitted = _core.HeadCommitted.Value;
        long tailCommitted = _core.TailCommitted.Value;
        long headReserved = _core.HeadReserved.Value;
        long tailReserved = _core.TailReserved.Value;

        nuint capacity = _core.Capacity.Value;
        ulong inFlightWrites = (ulong)(headReserved - headCommitted);
        ulong inFlightReads = (ulong)(tailReserved - tailCommitted);
        long used = headCommitted - tailCommitted;
        double utilization = capacity > 0 ? (double)used / (long)capacity * 100.0 : 0.0;

        return new MetricsSnapshot(
            CommittedHead: (ulong)headCommitted,
            CommittedTail: (ulong)tailCommitted,
            InFlightWrites: inFlightWrites,
            InFlightReads: inFlightReads,
            Capacity: capacity,
            ActiveLeases: _core.Lifecycle.ActiveLeaseCount,
            UtilizationPercentage: utilization,
            IsCompleted: _core.Lifecycle.IsCompleted,
            IsFaulted: _core.Lifecycle.IsFaulted,
            IsHealthy: !_core.Lifecycle.IsFaulted);
    }

    public HealthReport ExamineHealth()
    {
        var lifecycle = _core.Lifecycle;
        bool isFaulted = lifecycle.IsFaulted;
        bool isCompleted = lifecycle.IsCompleted;
        Exception? terminal = lifecycle.TerminalException;

        long headCommitted = _core.HeadCommitted.Value;
        long tailCommitted = _core.TailCommitted.Value;
        long used = headCommitted - tailCommitted;
        nuint capacity = _core.Capacity.Value;
        double utilization = capacity > 0 ? (double)used / (long)capacity * 100.0 : 0.0;

        string status = isFaulted
            ? $"Faulted: {terminal?.Message ?? "unknown error"}"
            : isCompleted
                ? used == 0 ? "Drained" : "Completing (draining)"
                : "Active";

        return new HealthReport(
            IsHealthy: !isFaulted,
            StatusMessage: status,
            Utilization: utilization,
            StalledWriteCycles: 0,
            TerminalFault: terminal);
    }

    public void Complete(Exception? error = null)
    {
        _core.Lifecycle.Complete(error);
    }

    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        if (!_core.Lifecycle.IsCompleted && !_core.Lifecycle.IsFaulted)
        {
            _core.Lifecycle.Complete(null);
        }

        await _core.Lifecycle.WaitForDrainAsync(cancellationToken).ConfigureAwait(false);
    }
}
