using System.Diagnostics.Metrics;

namespace Axrone.Memory.Arena;

public sealed class ArenaTelemetry : IDisposable
{
    private static readonly Meter s_meter = new("Axrone.Memory.Arena", "1.0.0");

    private readonly Counter<long> _writeCounter;
    private readonly Counter<long> _readCounter;
    private readonly Counter<long> _writeSpinCounter;
    private readonly Counter<long> _readSpinCounter;
    private readonly Counter<long> _writeOverflowCounter;
    private readonly Counter<long> _readUnderflowCounter;
    private readonly IAdministrativeEndpoint _endpoint;
    private int _disposed;

    public ArenaTelemetry(IAdministrativeEndpoint endpoint, string? instanceName = null)
    {
        if (endpoint is null) ThrowHelper.ThrowArgumentNullException(nameof(endpoint));

        string prefix = instanceName is not null ? $"arena.{instanceName}." : "arena.";
        _endpoint = endpoint;
        _writeCounter = s_meter.CreateCounter<long>(prefix + "writes.total", "items", "Total items written");
        _readCounter = s_meter.CreateCounter<long>(prefix + "reads.total", "items", "Total items read");
        _writeSpinCounter = s_meter.CreateCounter<long>(prefix + "spins.write.total", "spins", "Total write spin-wait iterations");
        _readSpinCounter = s_meter.CreateCounter<long>(prefix + "spins.read.total", "spins", "Total read spin-wait iterations");
        _writeOverflowCounter = s_meter.CreateCounter<long>(prefix + "overflow.write.total", "events", "Total write overflow rejections");
        _readUnderflowCounter = s_meter.CreateCounter<long>(prefix + "underflow.read.total", "events", "Total read underflow rejections");
    }

    public void RecordWrite(long count) => _writeCounter.Add(count);

    public void RecordRead(long count) => _readCounter.Add(count);

    public void RecordWriteSpin() => _writeSpinCounter.Add(1);

    public void RecordReadSpin() => _readSpinCounter.Add(1);

    public void RecordWriteOverflow() => _writeOverflowCounter.Add(1);

    public void RecordReadUnderflow() => _readUnderflowCounter.Add(1);

    public HealthReport SampleHealth() => _endpoint.GetHealthReport();

    public MetricsSnapshot SampleMetrics() => _endpoint.GetMetricsSnapshot();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
        }
    }
}
