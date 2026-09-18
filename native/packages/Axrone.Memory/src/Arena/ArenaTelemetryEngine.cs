using System.Diagnostics.Metrics;

namespace Axrone.Memory.Arena;

internal sealed class ArenaTelemetryEngine : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<ulong> _allocations;
    private readonly Counter<ulong> _allocatedBytes;
    private readonly Counter<ulong> _contentionEvents;
    private readonly Counter<ulong> _commitStalls;
    private readonly Histogram<double> _commitLatencyNs;
    private readonly KeyValuePair<string, object?>[] _tags;

    public ArenaTelemetryEngine(string meterName, string instanceName)
    {
        _meter = new Meter(meterName, "1.0.0");
        _tags = [new KeyValuePair<string, object?>("arena.instance", instanceName)];

        _allocations = _meter.CreateCounter<ulong>("arena.allocations.count", "{allocations}");
        _allocatedBytes = _meter.CreateCounter<ulong>("arena.allocated.bytes", "By");
        _contentionEvents = _meter.CreateCounter<ulong>("arena.contention.cas_cycles", "{cycles}");
        _commitStalls = _meter.CreateCounter<ulong>("arena.commit.stalls", "{stalls}",
            "Commit CAS spin iterations that exhausted the bounded spin budget.");
        _commitLatencyNs = _meter.CreateHistogram<double>("arena.commit.duration", "ns",
            "Wall-clock nanoseconds spent in the ordered CAS commit spin-wait.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordAllocation(ulong count, nuint bytes)
    {
        _allocations.Add(count, _tags);
        _allocatedBytes.Add((ulong)bytes, _tags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordContention(ulong cycles) => _contentionEvents.Add(cycles, _tags);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordCommitStall() => _commitStalls.Add(1, _tags);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordCommitLatency(double nanoseconds) => _commitLatencyNs.Record(nanoseconds, _tags);

    public void Dispose() => _meter.Dispose();
}
