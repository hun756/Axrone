namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for stable merge sort — order parity, the stability guarantee itself, and the scratch
/// validation that keeps a caller from aliasing their temp buffer onto the data.
/// </summary>
public class NativeBatchStableSortTests
{
    private struct Ascending : IBatchComparer<int>
    {
        public readonly int Compare(int left, int right) => left.CompareTo(right);
    }

    /// <summary>Sorts by <see cref="Key"/> only, so ties are interchangeable by value but not by order.</summary>
    private struct Payload
    {
        public int Key;
        public int Ordinal;
    }

    private struct ByKey : IBatchComparer<Payload>
    {
        public readonly int Compare(Payload left, Payload right) => left.Key.CompareTo(right.Key);
    }

    private static unsafe void StableSortWith(int[] data, int length)
    {
        var scratch = new int[length];
        fixed (int* pointer = data)
        fixed (int* scratchPointer = scratch)
        {
            new NativeBatch<int>(pointer, length).StableSort(new Span<int>(scratchPointer, length), new Ascending());
        }
    }

    // ── order parity ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(129)]
    [InlineData(1000)]
    public void StableSort_MatchesLinqOrderBy_AtEveryRunWidth(int length)
    {
        // Bottom-up merging doubles the run width each pass, so the interesting boundaries are
        // powers of two and the element just past them.
        var rng = new SeededRng((ulong)length + 11);
        var data = new int[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = rng.Next(-64, 64);
        }

        var scratch = new int[Math.Max(length, 1)];
        unsafe
        {
            fixed (int* pointer = data)
            fixed (int* scratchPointer = scratch)
            {
                new NativeBatch<int>(pointer, length)
                    .StableSort(new Span<int>(scratchPointer, length), new Ascending());
            }
        }

        data.Should().Equal(data.OrderBy(value => value));
    }

    [Fact]
    public void StableSort_LargeInput_MatchesLinqOrderBy()
    {
        const int length = 20_000;
        var rng = new SeededRng(31337);
        var data = new int[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = rng.Next(-1000, 1000);
        }

        var scratch = new int[length];
        var expected = (int[])data.Clone();
        Array.Sort(expected);

        unsafe
        {
            fixed (int* pointer = data)
            fixed (int* scratchPointer = scratch)
            {
                new NativeBatch<int>(pointer, length)
                    .StableSort(new Span<int>(scratchPointer, length), new Ascending());
            }
        }

        data.Should().Equal(expected);
    }

    [Fact]
    public void StableSort_AllEqual_PreservesEveryOrdinal()
    {
        // Every comparison ties, so the merge always takes left. A flipped tie-break would reverse
        // the whole batch and this is where it shows up.
        const int count = 300;
        var data = new Payload[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = new Payload { Key = 5, Ordinal = i };
        }

        var scratch = new Payload[count];
        unsafe
        {
            fixed (Payload* pointer = data)
            fixed (Payload* scratchPointer = scratch)
            {
                new NativeBatch<Payload>(pointer, count)
                    .StableSort(new Span<Payload>(scratchPointer, count), new ByKey());
            }
        }

        data.Select(payload => payload.Ordinal).Should().Equal(Enumerable.Range(0, count));
    }

    [Fact]
    public void StableSort_KeepsTiesInOriginalRelativeOrder()
    {
        const int count = 500;
        var rng = new SeededRng(4711);
        var data = new Payload[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = new Payload { Key = rng.Next(0, 6), Ordinal = i };
        }

        var scratch = new Payload[count];
        unsafe
        {
            fixed (Payload* pointer = data)
            fixed (Payload* scratchPointer = scratch)
            {
                new NativeBatch<Payload>(pointer, count)
                    .StableSort(new Span<Payload>(scratchPointer, count), new ByKey());
            }
        }

        data.Select(payload => payload.Key).Should().BeInAscendingOrder();

        // The contract: within any key group, ordinals must still ascend.
        foreach (var group in data.GroupBy(payload => payload.Key))
        {
            group.Select(payload => payload.Ordinal).Should().BeInAscendingOrder(
                $"ties on key {group.Key} must keep submission order");
        }
    }

    // ── scratch validation ──────────────────────────────────────────────

    [Fact]
    public void StableSort_ScratchTooShort_Throws()
    {
        int[] data = [5, 4, 3, 2, 1];
        var scratch = new int[4];

        var act = () =>
        {
            unsafe
            {
                fixed (int* pointer = data)
                fixed (int* scratchPointer = scratch)
                {
                    new NativeBatch<int>(pointer, 5).StableSort(new Span<int>(scratchPointer, 4), new Ascending());
                }
            }
        };

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public unsafe void StableSort_ScratchOverlappingBatch_Throws()
    {
        int* buffer = stackalloc int[8];
        for (var i = 0; i < 8; i++)
        {
            buffer[i] = 8 - i;
        }

        var batch = new NativeBatch<int>(buffer, 8);

        // Handing the batch its own memory as scratch would have the merge read slots it is
        // mid-write on, and produce a scrambled order with no error.
        var act = () => batch.StableSort(new Span<int>(buffer, 8), new Ascending());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public unsafe void StableSort_ScratchPartiallyOverlappingBatch_Throws()
    {
        int* buffer = stackalloc int[16];
        for (var i = 0; i < 16; i++)
        {
            buffer[i] = 16 - i;
        }

        var batch = new NativeBatch<int>(buffer, 8);

        var act = () => batch.StableSort(new Span<int>(buffer + 4, 8), new Ascending());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public unsafe void StableSort_ScratchPastBatchEnd_IsAccepted()
    {
        int* buffer = stackalloc int[16];
        for (var i = 0; i < 8; i++)
        {
            buffer[i] = 8 - i;
        }

        var batch = new NativeBatch<int>(buffer, 8);

        batch.StableSort(new Span<int>(buffer + 8, 8), new Ascending());

        new ReadOnlySpan<int>(buffer, 8).ToArray().Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
    }

    // ── degenerate inputs ───────────────────────────────────────────────

    [Fact]
    public unsafe void StableSort_EmptyAndSingleElementBatches_NeedNoScratch()
    {
        int* pointer = stackalloc int[1];
        pointer[0] = 42;

        new NativeBatch<int>(null, 0).StableSort(Span<int>.Empty, new Ascending());
        new NativeBatch<int>(pointer, 1).StableSort(Span<int>.Empty, new Ascending());

        pointer[0].Should().Be(42);
    }

    [Fact]
    public unsafe void StableSort_OnSlice_OrdersOnlyThatSlice()
    {
        int[] data = [1, 9, 8, 7, 2];
        var scratch = new int[3];
        fixed (int* pointer = data)
        fixed (int* scratchPointer = scratch)
        {
            new NativeBatch<int>(pointer, data.Length)
                .Slice(1, 3)
                .StableSort(new Span<int>(scratchPointer, 3), new Ascending());
        }

        data.Should().Equal(1, 7, 8, 9, 2);
    }

    [Fact]
    public void StableSort_IsIdempotent()
    {
        int[] data = [5, 3, 9, 1, 7, 2, 8, 4, 6, 0];

        StableSortWith(data, data.Length);
        var afterFirst = (int[])data.Clone();
        StableSortWith(afterFirst, afterFirst.Length);

        afterFirst.Should().Equal(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
    }
}
