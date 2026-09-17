using Axrone.Memory.Arena;
using Axrone.Utility.Alignment;

namespace Axrone.Memory.Tests.Arena;

public sealed class StorageBlockTests : IDisposable
{
    private NativeAlignedArenaStorageBlock<int>? _native;
    private PinnedHeapArenaStorageBlock<int>? _pinned;

    public void Dispose()
    {
        _native?.Dispose();
        _pinned?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void NativeAligned_CorrectTopology()
    {
        _native = new NativeAlignedArenaStorageBlock<int>(64, Alignment.CacheLine64);
        _native.Topology.Should().Be(MemoryTopology.NativeAligned);
    }

    [Fact]
    public void NativeAligned_CorrectElementCount()
    {
        _native = new NativeAlignedArenaStorageBlock<int>(128, Alignment.CacheLine64);
        _native.ElementCount.Should().Be(128);
    }

    [Fact]
    public void NativeAligned_SpanIsAccessible()
    {
        _native = new NativeAlignedArenaStorageBlock<int>(64, Alignment.CacheLine64);
        Span<int> span = _native.Span;
        span.Length.Should().BeGreaterThanOrEqualTo(64);
    }

    [Fact]
    public void NativeAligned_ZeroInitialized()
    {
        _native = new NativeAlignedArenaStorageBlock<int>(64, Alignment.CacheLine64);
        foreach (int val in _native.Span)
        {
            val.Should().Be(0);
        }
    }

    [Fact]
    public void NativeAligned_Dispose_PreventsAccess()
    {
        _native = new NativeAlignedArenaStorageBlock<int>(64, Alignment.CacheLine64);
        _native.Dispose();
        _native.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void PinnedHeap_CorrectTopology()
    {
        _pinned = new PinnedHeapArenaStorageBlock<int>(64, Alignment.CacheLine64);
        _pinned.Topology.Should().Be(MemoryTopology.PinnedHeap);
    }

    [Fact]
    public void PinnedHeap_CorrectElementCount()
    {
        _pinned = new PinnedHeapArenaStorageBlock<int>(128, Alignment.CacheLine64);
        _pinned.ElementCount.Should().Be(128);
    }

    [Fact]
    public void PinnedHeap_SpanIsAccessible()
    {
        _pinned = new PinnedHeapArenaStorageBlock<int>(64, Alignment.CacheLine64);
        Span<int> span = _pinned.Span;
        span.Length.Should().BeGreaterThanOrEqualTo(64);
    }

    [Fact]
    public void PinnedHeap_ZeroInitialized()
    {
        _pinned = new PinnedHeapArenaStorageBlock<int>(64, Alignment.CacheLine64);
        foreach (int val in _pinned.Span)
        {
            val.Should().Be(0);
        }
    }

    [Fact]
    public void PinnedHeap_Dispose_PreventsAccess()
    {
        _pinned = new PinnedHeapArenaStorageBlock<int>(64, Alignment.CacheLine64);
        _pinned.Dispose();
        _pinned.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void NativeAligned_InvalidCount_Throws()
    {
        Action act = () => { _ = new NativeAlignedArenaStorageBlock<int>(0, Alignment.CacheLine64); };
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PinnedHeap_InvalidCount_Throws()
    {
        Action act = () => { _ = new PinnedHeapArenaStorageBlock<int>(-1, Alignment.CacheLine64); };
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
