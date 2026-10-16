using Axrone.Memory.Arena;
using Axrone.Utility.Backoff.SpinPolicies;

namespace Axrone.Memory.Tests;

public sealed class ArenaWriterScopeTests : IDisposable
{
    private readonly ArenaMemoryRing<int, AdaptiveSpinBackoff> _ring =
        new(new ArenaCapacity(16));

    public void Dispose()
    {
        _ring.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void BufferWriter_Streams_ThroughReservation()
    {
        var writer = new ArenaBufferWriter<int>(_ring.Batch);
        Span<int> span = writer.GetSpan(4);
        span.Length.Should().BeGreaterThanOrEqualTo(4);
        span[0] = 1;
        span[1] = 2;
        writer.Advance(2);
        writer.Commit();
        writer.Dispose();

        _ring.Consumer.TryRead(out int first).Should().BeTrue();
        _ring.Consumer.TryRead(out int second).Should().BeTrue();
        (first, second).Should().Be((1, 2));
    }

    [Fact]
    public void BufferWriter_GetMemory_ThrowsNotSupported()
    {
        var writer = new ArenaBufferWriter<int>(_ring.Batch);
        bool threw = false;
        try
        {
            writer.GetMemory();
        }
        catch (NotSupportedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
        writer.Dispose();
    }

    [Fact]
    public void BufferWriter_Advance_WithoutReservation_Throws()
    {
        var writer = new ArenaBufferWriter<int>(_ring.Batch);
        bool threw = false;
        try
        {
            writer.Advance(1);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
        writer.Dispose();
    }

    [Fact]
    public void BufferWriter_Dispose_Abandons_AsZeroed()
    {
        var writer = new ArenaBufferWriter<int>(_ring.Batch);
        _ = writer.GetSpan(4);
        writer.Dispose();

        RingPeekView<int> view = _ring.Batch.PeekAvailable();
        view.TotalCount.Should().Be(4);
        foreach (ReadOnlySpan<int> run in view)
        {
            foreach (int value in run)
            {
                value.Should().Be(0);
            }
        }
    }

    [Fact]
    public void ArenaScope_Rewinds_OnDispose()
    {
        using var arena = new Axrone.Memory.Arena.Arena();
        arena.AllocateBytes(new ByteSize(128), Axrone.Utility.Alignment.Alignment.Byte).Length.Should().Be(128);
        nuint before = arena.TotalCommittedBytes.Value;
        before.Should().BeGreaterThan((nuint)0);

        using (arena.CreateScope())
        {
            arena.AllocateBytes(new ByteSize(1024), Axrone.Utility.Alignment.Alignment.Byte);
        }

        arena.TotalCommittedBytes.Value.Should().Be(before);
    }
}
