namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for introsort. The load-bearing test is parity against <see cref="Array.Sort{T}(T[])"/>
/// across adversarial shapes — an in-place sort that is subtly wrong on duplicates or skew is the
/// kind of bug that survives code review and corrupts a frame every tick.
/// </summary>
public class NativeBatchSortTests
{
    private struct Ascending : IBatchComparer<int>
    {
        public readonly int Compare(in int left, in int right) => left.CompareTo(right);
    }

    private struct Descending : IBatchComparer<int>
    {
        public readonly int Compare(in int left, in int right) => right.CompareTo(left);
    }

    /// <summary>Sorts by <see cref="Key"/> only, so ties are genuinely interchangeable.</summary>
    private struct Payload
    {
        public int Key;
        public int Ordinal;
    }

    private struct ByKey : IBatchComparer<Payload>
    {
        public readonly int Compare(in Payload left, in Payload right) => left.Key.CompareTo(right.Key);
    }

    /// <summary>
    /// Deterministic xorshift for reproducible property-test inputs. Deliberately not
    /// <see cref="Random"/>: these tests need a stable sequence, not a security-grade source, and
    /// a local generator keeps CA5394 from firing on a rule that has nothing to do with them.
    /// </summary>
    private struct SeededRng
    {
        private const ulong DefaultSeed = 0x9E3779B97F4A7C15UL;

        private ulong _state;

        public SeededRng(ulong seed) => _state = seed == 0 ? DefaultSeed : seed;

        public int Next(int exclusiveMax) => Next(0, exclusiveMax);

        public int Next(int minInclusive, int maxExclusive)
        {
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;

            var range = (ulong)(maxExclusive - minInclusive);
            return (int)(_state % range) + minInclusive;
        }
    }

    private static void SortWith(int[] data, int length, int[] scratch)
    {
        Array.Copy(data, scratch, length);
        unsafe
        {
            fixed (int* pointer = scratch)
            {
                new NativeBatch<int>(pointer, length).Sort(new Ascending());
            }
        }
    }

    // ── parity against Array.Sort ───────────────────────────────────────

    [Fact]
    public void Sort_MatchesArraySort_OnRandomInput()
    {
        var rng = new SeededRng(20260919);
        var scratch = new int[512];

        for (var trial = 0; trial < 200; trial++)
        {
            var length = rng.Next(0, scratch.Length + 1);
            var data = new int[scratch.Length];
            for (var i = 0; i < length; i++)
            {
                data[i] = rng.Next(-50, 50);
            }

            SortWith(data, length, scratch);

            var expected = new int[length];
            Array.Copy(data, expected, length);
            Array.Sort(expected);

            scratch.AsSpan(0, length).ToArray().Should().Equal(expected, $"trial {trial}, length {length}");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    public void Sort_MatchesArraySort_AtPartitionBoundaries(int length)
    {
        // 16 is the insertion-sort threshold; the depth limit flips near log2(n) * 2. Behaviour
        // either side of both is where an off-by-one in the range arithmetic shows up.
        var rng = new SeededRng((ulong)length + 1);
        var data = new int[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = rng.Next(-1000, 1000);
        }

        var scratch = new int[Math.Max(length, 1)];
        SortWith(data, length, scratch);

        var expected = (int[])data.Clone();
        Array.Sort(expected);

        scratch.AsSpan(0, length).ToArray().Should().Equal(expected);
    }

    [Fact]
    public void Sort_AlreadyAscending_IsUnchanged()
    {
        var data = Enumerable.Range(0, 200).ToArray();
        var scratch = new int[200];

        SortWith(data, 200, scratch);

        scratch.Should().Equal(data);
    }

    [Fact]
    public void Sort_DescendingInput_MatchesArraySort()
    {
        var data = Enumerable.Range(0, 200).Reverse().ToArray();
        var scratch = new int[200];

        SortWith(data, 200, scratch);

        scratch.Should().Equal(Enumerable.Range(0, 200));
    }

    [Fact]
    public void Sort_AllEqualElements_TerminatesAndPreservesInput()
    {
        // Every comparison returns equal, so Lomuto moves the store cursor on every probe. A
        // pivot-handling mistake here shows up as an infinite loop rather than a wrong answer.
        var data = Enumerable.Repeat(7, 500).ToArray();
        var scratch = new int[500];

        SortWith(data, 500, scratch);

        scratch.Should().OnlyContain(value => value == 7);
    }

    [Fact]
    public void Sort_TwoDistinctValues_MatchesArraySort()
    {
        var rng = new SeededRng(4242);
        var data = Enumerable.Range(0, 400).Select(_ => rng.Next(2) == 0 ? 1 : 999).ToArray();
        var scratch = new int[400];

        SortWith(data, 400, scratch);

        var expected = (int[])data.Clone();
        Array.Sort(expected);
        scratch.Should().Equal(expected);
    }

    [Fact]
    public void Sort_SawtoothPattern_MatchesArraySort()
    {
        // Repeated ascending runs with a hard drop — adversarial for naive first-element pivoting.
        var data = new int[300];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = i % 30;
        }

        var scratch = new int[300];
        SortWith(data, 300, scratch);

        var expected = (int[])data.Clone();
        Array.Sort(expected);
        scratch.Should().Equal(expected);
    }

    [Fact]
    public void Sort_LargeInput_MatchesArraySort()
    {
        const int length = 20_000;
        var rng = new SeededRng(99);
        var data = new int[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = rng.Next(int.MinValue / 4, int.MaxValue / 4);
        }

        var scratch = new int[length];
        SortWith(data, length, scratch);

        var expected = (int[])data.Clone();
        Array.Sort(expected);
        scratch.Should().Equal(expected);
    }

    [Fact]
    public void Sort_ExtremeValuesAcrossFullIntRange_MatchesArraySort()
    {
int[] data = [int.MaxValue, int.MinValue, 0, int.MaxValue - 1, int.MinValue + 1, -1, 1];
        var scratch = new int[data.Length];

        SortWith(data, data.Length, scratch);

        scratch.Should().Equal(int.MinValue, int.MinValue + 1, -1, 0, 1, int.MaxValue - 1, int.MaxValue);
    }

    // ── comparer direction ──────────────────────────────────────────────

    [Fact]
    public unsafe void Sort_DescendingComparer_OrdersReversed()
    {
        int[] data = [3, 1, 4, 1, 5, 9, 2, 6];
        fixed (int* pointer = data)
        {
            new NativeBatch<int>(pointer, data.Length).Sort(new Descending());
        }

        data.Should().Equal(9, 6, 5, 4, 3, 2, 1, 1);
    }

    // ── structural properties ───────────────────────────────────────────

    [Fact]
    public unsafe void Sort_IsPermutation_NoElementLostOrInvented()
    {
        var rng = new SeededRng(7);
        var data = new int[1000];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = rng.Next(0, 50);
        }

        var before = (int[])data.Clone();
        fixed (int* pointer = data)
        {
            new NativeBatch<int>(pointer, data.Length).Sort(new Ascending());
        }

        data.OrderBy(value => value).ToArray().Should().Equal(before.OrderBy(value => value));
    }

    [Fact]
    public unsafe void Sort_OnlyTouchesTheGivenLength_LeavesTailAlone()
    {
        int[] data = [9, 8, 7, 6, 5, 4, 3, 2];
        fixed (int* pointer = data)
        {
            new NativeBatch<int>(pointer, 4).Sort(new Ascending());
        }

        data.Should().Equal(6, 7, 8, 9, 5, 4, 3, 2);
    }

    [Fact]
    public unsafe void Sort_OnSlice_OrdersOnlyThatSlice()
    {
        int[] data = [1, 9, 8, 7, 2];
        fixed (int* pointer = data)
        {
            new NativeBatch<int>(pointer, data.Length).Slice(1, 3).Sort(new Ascending());
        }

        data.Should().Equal(1, 7, 8, 9, 2);
    }

    [Fact]
    public unsafe void Sort_EmptyAndSingleElementBatches_AreNoOps()
    {
        int[] data = [42];

        new NativeBatch<int>(null, 0).Sort(new Ascending());

        fixed (int* pointer = data)
        {
            new NativeBatch<int>(pointer, 1).Sort(new Ascending());
        }

        data.Should().Equal(42);
    }

    [Fact]
    public unsafe void Sort_StructElements_OrdersByKeyField()
    {
        Payload[] data =
        [
            new() { Key = 3, Ordinal = 0 },
            new() { Key = 1, Ordinal = 1 },
            new() { Key = 2, Ordinal = 2 },
            new() { Key = 1, Ordinal = 3 },
        ];

        fixed (Payload* pointer = data)
        {
            new NativeBatch<Payload>(pointer, data.Length).Sort(new ByKey());
        }

        data.Select(payload => payload.Key).Should().Equal(1, 1, 2, 3);
    }

    [Fact]
    public unsafe void Sort_IsNotStable_TiesMayReorder()
    {
        // Documents the contract rather than the implementation: introsort makes no stability
        // promise. If a future change accidentally made it stable this test still passes, which is
        // fine — the point is that callers must not rely on the opposite.
        const int count = 200;
        var data = new Payload[count];
        for (var i = 0; i < count; i++)
        {
            data[i] = new Payload { Key = i % 4, Ordinal = i };
        }

        fixed (Payload* pointer = data)
        {
            new NativeBatch<Payload>(pointer, count).Sort(new ByKey());
        }

        data.Select(payload => payload.Key).Should().OnlyContain(key => key >= 0 && key < 4);
        data.Select(payload => payload.Ordinal).Distinct().Should().HaveCount(count);
    }

    [Fact]
    public unsafe void Sort_CalledTwice_IsIdempotent()
    {
        int[] data = [5, 3, 9, 1, 7, 2, 8, 4, 6, 0];
        fixed (int* pointer = data)
        {
            var batch = new NativeBatch<int>(pointer, data.Length);
            batch.Sort(new Ascending());
            batch.Sort(new Ascending());
        }

        data.Should().Equal(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
    }
}
