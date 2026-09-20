using Axrone.Utility.Alignment;
using Axrone.Utility.Builders;

namespace Axrone.Memory.Arena;

public sealed class ArenaRingBuilder<T> : BuilderBase<ArenaRingBuilder<T>, ArenaMemoryRing<T, ProgressiveSpinBackoff>>
    where T : unmanaged
{
    private nuint _capacity;
    private MemoryTopology _topology = MemoryTopology.NativeAligned;
    private Alignment _alignment;
    private bool _zeroOnRecycle;
    private string _meterName = "Axrone.Memory.Arena";
    private string _instanceName = "default";

    public ArenaRingBuilder(nuint capacity)
    {
        _capacity = capacity;
    }

    public static ArenaRingBuilder<T> Create(nuint capacity) => new(capacity);

    public ArenaRingBuilder<T> WithCapacity(nuint capacity)
    {
        _capacity = capacity;
        return this;
    }

    public ArenaRingBuilder<T> WithTopology(MemoryTopology topology)
    {
        _topology = topology;
        return this;
    }

    public ArenaRingBuilder<T> WithNativeAlignedStorage()
    {
        _topology = MemoryTopology.NativeAligned;
        return this;
    }

    public ArenaRingBuilder<T> WithPinnedObjectHeapStorage()
    {
        _topology = MemoryTopology.PinnedObjectHeap;
        return this;
    }

    public ArenaRingBuilder<T> WithAlignment(Alignment alignment)
    {
        _alignment = alignment;
        return this;
    }

    public ArenaRingBuilder<T> WithZeroOnRecycle(bool value = true)
    {
        _zeroOnRecycle = value;
        return this;
    }

    public ArenaRingBuilder<T> WithTelemetry(string meterName, string instanceName = "default")
    {
        _meterName = meterName;
        _instanceName = instanceName;
        return this;
    }

    public ArenaMemoryRing<T, TBackoff> Build<TBackoff>()
        where TBackoff : struct, ISpinBackoff
    {
        ArenaCapacity capacity = new(_capacity);
        return new ArenaMemoryRing<T, TBackoff>(
            capacity,
            _topology,
            _alignment,
            _zeroOnRecycle,
            _meterName,
            _instanceName);
    }

    protected override ArenaRingBuilder<T> Self => this;

    public override bool TryBuild([MaybeNullWhen(false)] out ArenaMemoryRing<T, ProgressiveSpinBackoff> result, out BuilderDiagnostic diagnostic) =>
        TryCreate(Build, out result, out diagnostic);

    public override ArenaMemoryRing<T, ProgressiveSpinBackoff> Build()
    {
        return Build<ProgressiveSpinBackoff>();
    }
}
