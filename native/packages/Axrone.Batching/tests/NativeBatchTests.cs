namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for the batch view: construction validation, range arithmetic, and the copy semantics
/// callers inherit from <see cref="Span{T}"/>.
/// </summary>
public class NativeBatchTests
{
    private static readonly int[] Sample = [10, 20, 30, 40, 50];

    // ── construction ────────────────────────────────────────────────────

    [Fact]
    public unsafe void Constructor_AcceptsNullPointerForEmptyBatch()
    {
        var batch = new NativeBatch<int>(null, 0);

        batch.Length.Should().Be(0);
        batch.IsEmpty.Should().BeTrue();
        batch.Span.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public unsafe void Constructor_RejectsNegativeLength()
    {
        int* pointer = stackalloc int[1];

        var act = () => new NativeBatch<int>(pointer, -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public unsafe void Constructor_RejectsNullPointerForNonEmptyBatch()
    {
        var act = () => new NativeBatch<int>(null, 4);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public unsafe void Constructor_ExposesLengthAndSpan()
    {
        fixed (int* pointer = Sample)
        {
            var batch = new NativeBatch<int>(pointer, Sample.Length);

            batch.Length.Should().Be(5);
            batch.IsEmpty.Should().BeFalse();
            batch.ReadOnlySpan.ToArray().Should().Equal(Sample);
        }
    }

    // ── indexer ────────────────────────────────────────────────────────

    [Fact]
    public unsafe void Indexer_ReadsAndWritesThroughToBackingMemory()
    {
        int[] backing = [1, 2, 3];
        fixed (int* pointer = backing)
        {
            var batch = new NativeBatch<int>(pointer, backing.Length);

            batch[1] = 99;

            backing[1].Should().Be(99);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public unsafe void Indexer_OutOfRange_Throws(int index)
    {
        fixed (int* pointer = Sample)
        {
            var batch = new NativeBatch<int>(pointer, 3);

            var act = () => _ = batch[index];

            // Span-shaped, so it throws what Span<T> throws rather than what List<T> throws.
            act.Should().Throw<IndexOutOfRangeException>();
        }
    }

    // ── value semantics ─────────────────────────────────────────────────

    [Fact]
    public unsafe void CopySharesTheUnderlyingElements()
    {
        int[] backing = [1, 2, 3];
        fixed (int* pointer = backing)
        {
            var original = new NativeBatch<int>(pointer, backing.Length);

            var copy = original;
            copy[0] = 42;

            // A batch is a range descriptor, not an owning snapshot: copying it must not fork the data.
            original[0].Should().Be(42);
            backing[0].Should().Be(42);
        }
    }

    [Fact]
    public unsafe void Slice_StillWritesThroughToOriginalBacking()
    {
        int[] backing = [1, 2, 3, 4];
        fixed (int* pointer = backing)
        {
            var batch = new NativeBatch<int>(pointer, backing.Length);

            batch.Slice(1, 2)[0] = 77;

            backing[1].Should().Be(77);
        }
    }

    // ── Slice ──────────────────────────────────────────────────────────

    [Fact]
    public unsafe void Slice_ReturnsRequestedRange()
    {
        fixed (int* pointer = Sample)
        {
            var batch = new NativeBatch<int>(pointer, Sample.Length);

            batch.Slice(1, 3).ReadOnlySpan.ToArray().Should().Equal(20, 30, 40);
        }
    }

    [Fact]
    public unsafe void Slice_ToEmptyRange_IsAllowed()
    {
        fixed (int* pointer = Sample)
        {
            var batch = new NativeBatch<int>(pointer, Sample.Length);

            var empty = batch.Slice(2, 0);

            empty.IsEmpty.Should().BeTrue();
            empty.Length.Should().Be(0);
        }
    }

    [Fact]
    public unsafe void Slice_WholeBatch_Succeeds()
    {
        fixed (int* pointer = Sample)
        {
            var batch = new NativeBatch<int>(pointer, Sample.Length);

            batch.Slice(0, Sample.Length).ReadOnlySpan.ToArray().Should().Equal(Sample);
        }
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 6)]
    [InlineData(4, 2)]
    [InlineData(5, 1)]
    public unsafe void Slice_RangeOutsideBatch_Throws(int start, int length)
    {
        fixed (int* pointer = Sample)
        {
            var batch = new NativeBatch<int>(pointer, 5);

            var act = () => batch.Slice(start, length);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    [Theory]
    [InlineData(1, int.MaxValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(2, int.MaxValue - 1)]
    public unsafe void Slice_CannotOverflowPastTheEnd(int start, int length)
    {
        // Signed start + length wraps negative and would sail past the bounds test if the
        // comparison were done in int rather than unsigned.
        fixed (int* pointer = Sample)
        {
            var batch = new NativeBatch<int>(pointer, 5);

            var act = () => batch.Slice(start, length);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    // ── CopyTo / TryCopyTo ──────────────────────────────────────────────

    [Fact]
    public unsafe void CopyTo_ExactFit_Succeeds()
    {
        int[] source = [1, 2, 3];
        int[] target = new int[3];
        fixed (int* sourcePointer = source)
        fixed (int* targetPointer = target)
        {
            new NativeBatch<int>(sourcePointer, 3).CopyTo(new NativeBatch<int>(targetPointer, 3));

            target.Should().Equal(1, 2, 3);
        }
    }

    [Fact]
    public unsafe void CopyTo_LargerDestination_SucceedsAndLeavesTail()
    {
        int[] source = [1, 2, 3];
        int[] target = [-1, -1, -1, -1, -1];
        fixed (int* sourcePointer = source)
        fixed (int* targetPointer = target)
        {
            new NativeBatch<int>(sourcePointer, 3).CopyTo(new NativeBatch<int>(targetPointer, 5));

            target.Should().Equal(1, 2, 3, -1, -1);
        }
    }

    [Fact]
    public unsafe void CopyTo_ShorterDestination_Throws()
    {
        int[] source = [1, 2, 3];
        int[] target = new int[2];
        fixed (int* sourcePointer = source)
        fixed (int* targetPointer = target)
        {
            var batch = new NativeBatch<int>(sourcePointer, 3);
            var destination = new NativeBatch<int>(targetPointer, 2);

            var threw = false;
            try
            {
                batch.CopyTo(destination);
            }
            catch (ArgumentException)
            {
                threw = true;
            }

            // The original silently truncated to min(source, destination); that hides sizing bugs.
            threw.Should().BeTrue("a short destination must report failure rather than drop elements");
        }
    }

    [Fact]
    public unsafe void TryCopyTo_ReportsFailureInsteadOfThrowing()
    {
        int[] source = [1, 2, 3];
        int[] target = new int[2];
        fixed (int* sourcePointer = source)
        fixed (int* targetPointer = target)
        {
            var copied = new NativeBatch<int>(sourcePointer, 3).TryCopyTo(new NativeBatch<int>(targetPointer, 2));

            copied.Should().BeFalse();
        }
    }

    [Fact]
    public unsafe void TryCopyTo_ReturnsTrueWhenItFits()
    {
        int[] source = [1, 2, 3];
        int[] target = new int[4];
        fixed (int* sourcePointer = source)
        fixed (int* targetPointer = target)
        {
            var copied = new NativeBatch<int>(sourcePointer, 3).TryCopyTo(new NativeBatch<int>(targetPointer, 4));

            copied.Should().BeTrue();
            target.AsSpan(0, 3).ToArray().Should().Equal(1, 2, 3);
        }
    }

    // ── Clear / Fill ────────────────────────────────────────────────────

    [Fact]
    public unsafe void Clear_ZeroesEveryElement()
    {
        int[] backing = [1, 2, 3, 4];
        fixed (int* pointer = backing)
        {
            new NativeBatch<int>(pointer, backing.Length).Clear();

            backing.Should().Equal(0, 0, 0, 0);
        }
    }

    [Fact]
    public unsafe void Fill_WritesValueToEveryElement()
    {
        int[] backing = new int[5];
        fixed (int* pointer = backing)
        {
            new NativeBatch<int>(pointer, backing.Length).Fill(7);

            backing.Should().Equal(7, 7, 7, 7, 7);
        }
    }

    [Fact]
    public unsafe void Clear_OnEmptyBatch_IsNoOp()
    {
        var act = () => new NativeBatch<int>(null, 0).Clear();

        act.Should().NotThrow();
    }
}
