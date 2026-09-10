using Axrone.Collections;

namespace Axrone.Collections.Tests;

public class SpscRingBufferTests
{
    [Fact]
    public void TryWrite_TryRead_BasicFlow()
    {
        using var buffer = new SpscVectorStreamRingBuffer<int>(new BatchCapacity(16));

        Span<int> data = stackalloc int[] { 1, 2, 3, 4, 5 };
        buffer.TryWrite(data).Should().BeTrue();

        Span<int> dest = stackalloc int[5];
        buffer.TryRead(dest, out int read).Should().BeTrue();
        read.Should().Be(5);
        dest[0].Should().Be(1);
        dest[4].Should().Be(5);
    }

    [Fact]
    public void TryRead_WhenEmpty_ReturnsFalse()
    {
        using var buffer = new SpscVectorStreamRingBuffer<int>(new BatchCapacity(16));

        Span<int> dest = stackalloc int[4];
        buffer.TryRead(dest, out int read).Should().BeFalse();
        read.Should().Be(0);
    }

    [Fact]
    public void TryWrite_ExceedingCapacity_ReturnsFalse()
    {
        using var buffer = new SpscVectorStreamRingBuffer<int>(new BatchCapacity(4));

        Span<int> data = stackalloc int[5];
        data.Fill(42);
        buffer.TryWrite(data).Should().BeFalse();
    }

    [Fact]
    public void TryWrite_FullBuffer_ReturnsFalse()
    {
        using var buffer = new SpscVectorStreamRingBuffer<int>(new BatchCapacity(4));

        Span<int> first = stackalloc int[] { 1, 2, 3, 4 };
        buffer.TryWrite(first).Should().BeTrue();

        Span<int> second = stackalloc int[] { 5 };
        buffer.TryWrite(second).Should().BeFalse();
    }

    [Fact]
    public void WrapAround_WritesAndReadsAcrossBoundary()
    {
        using var buffer = new SpscVectorStreamRingBuffer<int>(new BatchCapacity(4));

        Span<int> writeData = stackalloc int[3];
        Span<int> readDest = stackalloc int[3];

        // Fill and drain multiple times to force wrap-around
        for (int round = 0; round < 5; round++)
        {
            writeData[0] = round * 10 + 1;
            writeData[1] = round * 10 + 2;
            writeData[2] = round * 10 + 3;
            buffer.TryWrite(writeData).Should().BeTrue();

            buffer.TryRead(readDest, out int read).Should().BeTrue();
            read.Should().Be(3);
            readDest[0].Should().Be(round * 10 + 1);
            readDest[2].Should().Be(round * 10 + 3);
        }
    }

    [Fact]
    public void WrapAround_SplitWrite_ReadsCorrectly()
    {
        using var buffer = new SpscVectorStreamRingBuffer<int>(new BatchCapacity(4));

        // Write 3, read 3 to advance head/tail to position 3 (near end of 4-slot buffer)
        Span<int> advance = stackalloc int[] { 100, 200, 300 };
        buffer.TryWrite(advance).Should().BeTrue();
        Span<int> discard = stackalloc int[3];
        buffer.TryRead(discard, out _).Should().BeTrue();

        // Now tail=3, head=3. Write 3 items: should wrap around the boundary
        Span<int> wrapData = stackalloc int[] { 1, 2, 3 };
        buffer.TryWrite(wrapData).Should().BeTrue();

        Span<int> result = stackalloc int[3];
        buffer.TryRead(result, out int read).Should().BeTrue();
        read.Should().Be(3);
        result[0].Should().Be(1);
        result[1].Should().Be(2);
        result[2].Should().Be(3);
    }

    [Fact]
    public void Capacity_IsRoundedUpToPowerOfTwo()
    {
        using var buffer = new SpscVectorStreamRingBuffer<int>(new BatchCapacity(10));
        buffer.Capacity.Should().Be(16);
    }

    [Fact]
    public void PartialRead_ReadsOnlyAvailable()
    {
        using var buffer = new SpscVectorStreamRingBuffer<int>(new BatchCapacity(16));

        Span<int> data = stackalloc int[] { 10, 20, 30 };
        buffer.TryWrite(data).Should().BeTrue();

        Span<int> dest = stackalloc int[10];
        buffer.TryRead(dest, out int read).Should().BeTrue();
        read.Should().Be(3);
        dest[0].Should().Be(10);
        dest[2].Should().Be(30);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var buffer = new SpscVectorStreamRingBuffer<int>(new BatchCapacity(16));
        buffer.Dispose();
        buffer.Dispose();
    }
}
