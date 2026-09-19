namespace Axrone.Batching.Tests;

/// <summary>Coverage for LSD radix sort and sort-by-key.</summary>
public class NativeBatchRadixSortTests
{
    private struct Payload
    {
        public int Ordinal;
    }

    private static void RadixSortWith(uint[] data, int length)
    {
        var scratch = new uint[Math.Max(length, 1)];
        unsafe
        {
            fixed (uint* pointer = data)
            fixed (uint* scratchPointer = scratch)
            {
                new NativeBatch<uint>(pointer, length).RadixSort(new Span<uint>(scratchPointer, length));
            }
        }
    }

    // ── order parity ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(4096)]
    public void RadixSort_MatchesArraySort_AtDigitBoundaries(int length)
    {
        // 256 is the bucket count; runs straddling it exercise the prefix-sum boundaries.
        var rng = new SeededRng((ulong)length + 5);
        var data = new uint[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = rng.NextUInt32();
        }

        var expected = (uint[])data.Clone();
        Array.Sort(expected);

        RadixSortWith(data, length);

        data.Should().Equal(expected);
    }

    [Fact]
    public void RadixSort_ExtremeValues_SortsEveryBitPosition()
    {
        uint[] data = [uint.MaxValue, 0u, 1u, uint.MaxValue - 1, 1u << 31, 1u << 24, 1u << 16, 1u << 8];
        var expected = (uint[])data.Clone();
        Array.Sort(expected);

        RadixSortWith(data, data.Length);

        data.Should().Equal(expected);
    }

    [Fact]
    public void RadixSort_AllEqual_IsUnchanged()
    {
        uint[] data = Enumerable.Repeat(0xDEADBEEF, 500).ToArray();

        RadixSortWith(data, 500);

        data.Should().OnlyContain(value => value == 0xDEADBEEF);
    }

    [Fact]
    public void RadixSort_LargeInput_MatchesArraySort()
    {
        const int length = 50_000;
        var rng = new SeededRng(8123);
        var data = new uint[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = rng.NextUInt32();
        }

        var expected = (uint[])data.Clone();
        Array.Sort(expected);

        RadixSortWith(data, length);

        data.Should().Equal(expected);
    }

    [Fact]
    public void RadixSort_NonUintBatch_IsANoOp()
    {
        // Documented behaviour: the pass reads raw unsigned bits, so a signed batch is left alone
        // rather than silently mis-sorted.
        int[] data = [3, 1, 2];
        var scratch = new int[3];
        unsafe
        {
            fixed (int* pointer = data)
            fixed (int* scratchPointer = scratch)
            {
                new NativeBatch<int>(pointer, 3).RadixSort(new Span<int>(scratchPointer, 3));
            }
        }

        data.Should().Equal(3, 1, 2);
    }

    // ── validation ─────────────────────────────────────────────────────

    [Fact]
    public unsafe void RadixSort_ScratchTooShort_Throws()
    {
        uint[] data = [9, 8, 7, 6, 5];
        var scratch = new uint[4];
        fixed (uint* pointer = data)
        fixed (uint* scratchPointer = scratch)
        {
            var batch = new NativeBatch<uint>(pointer, 5);
            var span = new Span<uint>(scratchPointer, 4);

            var threw = false;
            try
            {
                batch.RadixSort(span);
            }
            catch (ArgumentException)
            {
                threw = true;
            }

            threw.Should().BeTrue();
        }
    }

    [Fact]
    public unsafe void RadixSort_ScratchOverlappingBatch_Throws()
    {
        uint* buffer = stackalloc uint[8];
        for (var i = 0; i < 8; i++)
        {
            buffer[i] = (uint)(8 - i);
        }

        var batch = new NativeBatch<uint>(buffer, 8);

        var act = () => batch.RadixSort(new Span<uint>(buffer, 8));

        act.Should().Throw<ArgumentException>();
    }

    // ── SortByKey ──────────────────────────────────────────────────────

    [Fact]
    public unsafe void SortByKey_ReordersPayloadsAlongWithKeys()
    {
        uint[] keys = [30u, 10u, 20u];
        var values = new Payload[3];
        var keyScratch = new uint[3];
        var valueScratch = new Payload[3];

        for (var i = 0; i < 3; i++)
        {
            values[i] = new Payload { Ordinal = i };
        }

        fixed (uint* keyPointer = keys)
        fixed (Payload* valuePointer = values)
        fixed (uint* keyScratchPointer = keyScratch)
        fixed (Payload* valueScratchPointer = valueScratch)
        {
            var keyBatch = new NativeBatch<uint>(keyPointer, 3);
            var valueBatch = new NativeBatch<Payload>(valuePointer, 3);

            keyBatch.SortByKey(keyBatch, valueBatch, new Span<uint>(keyScratchPointer, 3), new Span<Payload>(valueScratchPointer, 3));
        }

        keys.Should().Equal(10u, 20u, 30u);
        values.Select(payload => payload.Ordinal).Should().Equal(1, 2, 0);
    }

    [Fact]
    public unsafe void SortByKey_MismatchedLengths_Throws()
    {
        // keys.Length was trusted as authoritative, scattering payloads past the end of values.
        uint[] keys = [3u, 1u, 2u, 9u];
        var values = new Payload[2];
        var keyScratch = new uint[4];
        var valueScratch = new Payload[2];

        fixed (uint* keyPointer = keys)
        fixed (Payload* valuePointer = values)
        fixed (uint* keyScratchPointer = keyScratch)
        fixed (Payload* valueScratchPointer = valueScratch)
        {
            var keyBatch = new NativeBatch<uint>(keyPointer, 4);
            var valueBatch = new NativeBatch<Payload>(valuePointer, 2);

            var keySpan = new Span<uint>(keyScratchPointer, 4);
            var valueSpan = new Span<Payload>(valueScratchPointer, 2);

            var threw = false;
            try
            {
                keyBatch.SortByKey(keyBatch, valueBatch, keySpan, valueSpan);
            }
            catch (ArgumentException)
            {
                threw = true;
            }

            threw.Should().BeTrue("a shorter values batch must be rejected before any scatter");
        }
    }

    [Fact]
    public unsafe void SortByKey_KeepsTiesInSourceOrder()
    {
        const int count = 200;
        var keys = new uint[count];
        var values = new Payload[count];
        var keyScratch = new uint[count];
        var valueScratch = new Payload[count];
        var rng = new SeededRng(606);

        for (var i = 0; i < count; i++)
        {
            keys[i] = (uint)rng.Next(0, 5);
            values[i] = new Payload { Ordinal = i };
        }

        fixed (uint* keyPointer = keys)
        fixed (Payload* valuePointer = values)
        fixed (uint* keyScratchPointer = keyScratch)
        fixed (Payload* valueScratchPointer = valueScratch)
        {
            var keyBatch = new NativeBatch<uint>(keyPointer, count);
            var valueBatch = new NativeBatch<Payload>(valuePointer, count);

            keyBatch.SortByKey(keyBatch, valueBatch, new Span<uint>(keyScratchPointer, count), new Span<Payload>(valueScratchPointer, count));
        }

        keys.Should().BeInAscendingOrder();

        foreach (var group in keys.Zip(values).GroupBy(pair => pair.First))
        {
            group.Select(pair => pair.Second.Ordinal).Should().BeInAscendingOrder(
                $"ties on key {group.Key} must keep source order");
        }
    }

    [Fact]
    public unsafe void SortByKey_RejectsScratchOverlappingItsBatch()
    {
        uint* keys = stackalloc uint[4] { 4u, 3u, 2u, 1u };
        Payload* values = stackalloc Payload[4];
        for (var i = 0; i < 4; i++)
        {
            values[i] = new Payload { Ordinal = i };
        }

        var keyBatch = new NativeBatch<uint>(keys, 4);
        var valueBatch = new NativeBatch<Payload>(values, 4);

        var act = () => keyBatch.SortByKey(
            keyBatch, valueBatch, new Span<uint>(keys, 4), new Span<Payload>(values, 4));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public unsafe void SortByKey_SingleElement_IsANoOp()
    {
        uint* keys = stackalloc uint[1] { 7u };
        Payload* values = stackalloc Payload[1];
        values[0] = new Payload { Ordinal = 42 };

        var keyBatch = new NativeBatch<uint>(keys, 1);
        var valueBatch = new NativeBatch<Payload>(values, 1);

        keyBatch.SortByKey(keyBatch, valueBatch, Span<uint>.Empty, Span<Payload>.Empty);

        keys[0].Should().Be(7u);
        values[0].Ordinal.Should().Be(42);
    }
}
