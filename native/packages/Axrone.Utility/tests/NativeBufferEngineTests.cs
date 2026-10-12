namespace Axrone.Utility.Tests.NativeBuffer;

using System.Buffers;
using Axrone.Utility.NativeBuffer;

public sealed class NativeBufferEngineTests : IDisposable
{
    private NativeBuffer<int, AlignedNativeAllocator>? _buffer;

    public void Dispose()
    {
        _buffer?.Dispose();
        _buffer = null;
    }

    private NativeBuffer<int, AlignedNativeAllocator> NewBuffer(nuint length = 64)
    {
        _buffer?.Dispose();
        _buffer = new NativeBuffer<int, AlignedNativeAllocator>(ElementCount.From(length), MemoryAlignment.CacheLine);
        return _buffer;
    }

    [Fact]
    public void ZeroLength_Throws()
    {
        Action act = () => _ = new NativeBuffer<int, AlignedNativeAllocator>(ElementCount.From(0), MemoryAlignment.CacheLine);

        act.Should().Throw<ArgumentOutOfRangeException>();
        _buffer = null;
    }

    [Fact]
    public void Write_Read_RoundTrip()
    {
        var buffer = NewBuffer();
        int[] source = [1, 2, 3, 4, 5, 6, 7, 8];

        buffer.Write(ElementCount.From(0), source);
        buffer[BufferIndex.From(3)].Should().Be(4);

        Span<int> destination = stackalloc int[8];
        buffer.Read(ElementCount.From(0), destination);
        destination.ToArray().Should().Equal(source);
    }

    [Fact]
    public void Index_OutOfRange_Throws()
    {
        var buffer = NewBuffer(4);

        Action act = () => _ = buffer[BufferIndex.From(4)];

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Fill_And_Clear()
    {
        var buffer = NewBuffer();
        buffer.Fill(9);
        buffer[BufferIndex.From(63)].Should().Be(9);

        buffer.Clear();
        buffer[BufferIndex.From(0)].Should().Be(0);
        buffer[BufferIndex.From(63)].Should().Be(0);
    }

    [Fact]
    public void SequenceEqual_Compares()
    {
        var left = NewBuffer();
        using var right = new NativeBuffer<int, AlignedNativeAllocator>(ElementCount.From(64), MemoryAlignment.CacheLine);
        int[] data = new int[64];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = i;
        }

        left.Write(ElementCount.From(0), data);
        right.Write(ElementCount.From(0), data);

        left.SequenceEqual(right).Should().BeTrue();
        left.SequenceEqual(data).Should().BeTrue();
        right[BufferIndex.From(0)] = -1;
        left.SequenceEqual(right).Should().BeFalse();
        left.SequenceEqual(data.AsSpan(0, 8)).Should().BeFalse();
    }

    [Fact]
    public void Slice_Subviews()
    {
        var buffer = NewBuffer();
        int[] data = new int[64];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = i * 2;
        }
        buffer.Write(ElementCount.From(0), data);

        var slice = buffer.Slice(ElementCount.From(8), ElementCount.From(8));
        slice.Length.Should().Be(ElementCount.From(8));
        slice[BufferIndex.From(0)].Should().Be(16);
        slice.Span.Length.Should().Be(8);

        var sub = slice.Slice(ElementCount.From(2), ElementCount.From(4));
        sub[BufferIndex.From(0)].Should().Be(20);

        Action over = () => buffer.Slice(ElementCount.From(60), ElementCount.From(8));
        over.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Unaligned_ReadWrite()
    {
        var buffer = NewBuffer();

        buffer.WriteUnaligned(ByteOffset.From(3), 0x11223344u);
        buffer.ReadUnaligned<uint>(ByteOffset.From(3)).Should().Be(0x11223344u);

        Action over = () => buffer.ReadUnaligned<ulong>(ByteOffset.From((nuint)(64 * 4 - 4)));
        over.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Lease_TracksAndDrains()
    {
        var buffer = NewBuffer();
        buffer.ActiveLeases.Should().Be(0u);

        using (NativeBufferLease<int, AlignedNativeAllocator> lease = buffer.Lease())
        {
            lease.IsActive.Should().BeTrue();
            buffer.ActiveLeases.Should().Be(1u);
            lease.Length.Should().Be(64u);
            lease.Span.Length.Should().Be(64);
            lease.ReadOnlySpan.Length.Should().Be(64);
            buffer.LifetimeAcquisitions.Should().Be(1);
        }

        buffer.ActiveLeases.Should().Be(0u);
        buffer.LifetimeReleases.Should().Be(1);
    }

    [Fact]
    public void TryLease_Failure_Reports()
    {
        var buffer = NewBuffer();

        buffer.TryLease(out NativeBufferLease<int, AlignedNativeAllocator> lease).Should().BeTrue();
        lease.Dispose();

        buffer.Complete();
        buffer.TryLease(out _).Should().BeFalse();
        buffer.TryAcquireLease().Outcome.Should().Be(LeaseOutcome.BufferDrained);
    }

    [Fact]
    public void Complete_Fault_Rethrows()
    {
        var buffer = NewBuffer();
        buffer.Complete(new InvalidOperationException("boom"));

        Action read = () => buffer.Fill(1);
        read.Should().Throw<InvalidOperationException>()
            .Where(e => e.InnerException != null && e.InnerException.Message == "boom");
    }

    [Fact]
    public async Task DrainAsync_WaitsForLeases()
    {
        var buffer = NewBuffer();
        NativeBufferLease<int, AlignedNativeAllocator> lease = buffer.Lease();

        Task drain = buffer.DrainAsync().AsTask();
        await Task.Delay(50);
        drain.IsCompleted.Should().BeFalse();

        lease.Dispose();
        await drain;
        _buffer = null;
    }

    [Fact]
    public void Builder_BuildsSizedBuffer()
    {
        using var buffer = NativeBuffer.Configure<int>()
            .WithLength(ElementCount.From(32))
            .Build();

        buffer.Length.Should().Be(ElementCount.From(32));
        buffer.Alignment.Value.Should().Be((nuint)64);
    }

    [Fact]
    public void Builder_ZeroLength_Throws()
    {
        Action act = () => NativeBuffer.Configure<int>().WithLength(ElementCount.From(0));

        act.Should().Throw<ArgumentOutOfRangeException>();
        _buffer = null;
    }

    [Fact]
    public void StaticFactory_Allocates()
    {
        using var buffer = NativeBuffer.Allocate<int>(ElementCount.From(16));

        buffer.Length.Should().Be(ElementCount.From(16));
        _buffer = null;
    }

    [Fact]
    public void Enumerator_WalksElements()
    {
        var buffer = NewBuffer(8);
        int[] data = [5, 6, 7, 8, 9, 10, 11, 12];
        buffer.Write(ElementCount.From(0), data);

        var seen = new List<int>();
        foreach (int value in buffer)
        {
            seen.Add(value);
        }
        seen.Should().Equal(data);
    }

    [Fact]
    public void Writer_AdvancesAndResets()
    {
        var buffer = NewBuffer();
        var writer = new NativeBufferWriter<int, AlignedNativeAllocator>(buffer);
        writer.Capacity.Should().Be(ElementCount.From(64));

        writer.GetSpan(8).Fill(3);
        writer.Advance(8);
        writer.WrittenCount.Should().Be(ElementCount.From(8));
        writer.WrittenSpan.ToArray().Should().AllSatisfy(v => v.Should().Be(3));

        writer.Reset();
        writer.WrittenCount.Should().Be(ElementCount.From(0));

        Action over = () => writer.Advance(65);
        over.Should().Throw<ArgumentOutOfRangeException>();

        Action negative = () => writer.Advance(-1);
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Memory_Pin_Works()
    {
        var buffer = NewBuffer(8);

        buffer.Memory.Length.Should().Be(8);
        using MemoryHandle handle = buffer.Memory.Pin();
    }

    [Fact]
    public void Dispose_Poisons_AndIsIdempotent()
    {
        var buffer = NewBuffer(8);
        _buffer = buffer;

        buffer.Dispose();
        buffer.IsDisposed.Should().BeTrue();
        Action read = () => buffer.Fill(1);
        read.Should().Throw<ObjectDisposedException>();

        Action again = () => buffer.Dispose();
        again.Should().NotThrow();
    }
}
