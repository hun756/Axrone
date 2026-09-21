using Axrone.Utility.Alignment;

namespace Axrone.Memory.Arena;

public sealed class ArenaMemoryRing<T, TBackoff> : IDisposable
    where T : unmanaged
    where TBackoff : struct, ISpinBackoff
{
    private readonly RingCore<T, TBackoff> _core;
    private readonly RingProducer<T, TBackoff> _producer;
    private readonly RingConsumer<T, TBackoff> _consumer;
    private readonly RingStreamer<T, TBackoff> _streamer;
    private readonly RingAdminEndpoint<T, TBackoff> _admin;
    private readonly RingBatchCoordinator<T, TBackoff> _batch;

    public ArenaMemoryRing(
        ArenaCapacity capacity,
        MemoryTopology topology = MemoryTopology.NativeAligned,
        Alignment alignment = default,
        bool zeroOnRecycle = false,
        string meterName = "Axrone.Memory.Arena",
        string instanceName = "default")
    {
        _core = new RingCore<T, TBackoff>(capacity, topology, alignment, zeroOnRecycle, meterName, instanceName);
        _producer = new RingProducer<T, TBackoff>(_core);
        _consumer = new RingConsumer<T, TBackoff>(_core);
        _streamer = new RingStreamer<T, TBackoff>(_core);
        _admin = new RingAdminEndpoint<T, TBackoff>(_core);
        _batch = new RingBatchCoordinator<T, TBackoff>(_core);
    }

    public IArenaProducer<T> Producer => _producer;

    public IArenaConsumer<T> Consumer => _consumer;

    public IAsyncStreamable<T> Streamer => _streamer;

    public IAdministrativeEndpoint Admin => _admin;

    public IBatchReservable<T> Batch => _batch;

    public nuint Capacity => _core.Capacity.Value;

    public bool IsDisposed => _isDisposed != 0;

    private int _isDisposed;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            _core.Dispose();
        }
    }
}
