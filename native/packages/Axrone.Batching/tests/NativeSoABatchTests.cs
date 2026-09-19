namespace Axrone.Batching.Tests;

/// <summary>Coverage for the SoA batch views.</summary>
public class NativeSoABatchTests
{
    [Fact]
    public unsafe void SoA2_SlicesEveryStreamTogether()
    {
        int* positions = stackalloc int[4] { 10, 20, 30, 40 };
        int* velocities = stackalloc int[4] { 1, 2, 3, 4 };

        var batch = new NativeSoABatch2<int, int>(positions, velocities, 4);
        var tail = batch.Slice(1, 3);

        tail.Span1.ToArray().Should().Equal(20, 30, 40);
        tail.Span2.ToArray().Should().Equal(2, 3, 4);
    }

    [Fact]
    public unsafe void SoA2_WritesThroughToBothBackingStreams()
    {
        int* first = stackalloc int[2] { 0, 0 };
        int* second = stackalloc int[2] { 0, 0 };

        var batch = new NativeSoABatch2<int, int>(first, second, 2);
        batch.Span1[0] = 7;
        batch.Span2[1] = 9;

        first[0].Should().Be(7);
        second[1].Should().Be(9);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 3)]
    [InlineData(2, 2)]
    [InlineData(3, 1)]
    public unsafe void SoA2_SliceOutsideRange_Throws(int start, int length)
    {
        int* first = stackalloc int[2];
        int* second = stackalloc int[2];

        var batch = new NativeSoABatch2<int, int>(first, second, 2);

        var act = () => batch.Slice(start, length);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1, int.MaxValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public unsafe void SoA2_SliceCannotOverflowPastTheEnd(int start, int length)
    {
        int* first = stackalloc int[2];
        int* second = stackalloc int[2];

        var batch = new NativeSoABatch2<int, int>(first, second, 2);

        var act = () => batch.Slice(start, length);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public unsafe void SoA2_ConstructorRejectsNegativeLength()
    {
        int* first = stackalloc int[1];
        int* second = stackalloc int[1];

        var act = () => new NativeSoABatch2<int, int>(first, second, -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public unsafe void SoA2_ConstructorRejectsNullStreamForNonEmptyBatch()
    {
        int* second = stackalloc int[2];

        var act = () => new NativeSoABatch2<int, int>(null, second, 2);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public unsafe void SoA2_EmptyBatchAcceptsNullStreams()
    {
        var batch = new NativeSoABatch2<int, int>(null, null, 0);

        batch.IsEmpty.Should().BeTrue();
        batch.Length.Should().Be(0);
    }

    [Fact]
    public unsafe void SoA3_KeepsThreeStreamsAligned()
    {
        int* x = stackalloc int[3] { 1, 2, 3 };
        int* y = stackalloc int[3] { 4, 5, 6 };
        int* z = stackalloc int[3] { 7, 8, 9 };

        var batch = new NativeSoABatch3<int, int, int>(x, y, z, 3);
        var tail = batch.Slice(1, 2);

        tail.Span1.ToArray().Should().Equal(2, 3);
        tail.Span2.ToArray().Should().Equal(5, 6);
        tail.Span3.ToArray().Should().Equal(8, 9);
        tail.Slice(0, 1).Span2[0].Should().Be(5);
    }

    [Fact]
    public unsafe void SoA3_RejectsNullStreamForNonEmptyBatch()
    {
        int* second = stackalloc int[1];
        int* third = stackalloc int[1];

        var act = () => new NativeSoABatch3<int, int, int>(null, second, third, 1);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public unsafe void SoA4_KeepsFourStreamsAligned()
    {
        int* a = stackalloc int[4] { 1, 2, 3, 4 };
        int* b = stackalloc int[4] { 5, 6, 7, 8 };
        int* c = stackalloc int[4] { 9, 10, 11, 12 };
        int* d = stackalloc int[4] { 13, 14, 15, 16 };

        var batch = new NativeSoABatch4<int, int, int, int>(a, b, c, d, 4);
        var middle = batch.Slice(1, 2);

        middle.Span1.ToArray().Should().Equal(2, 3);
        middle.Span2.ToArray().Should().Equal(6, 7);
        middle.Span3.ToArray().Should().Equal(10, 11);
        middle.Span4.ToArray().Should().Equal(14, 15);
        middle.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public unsafe void SoA4_RejectsNullStreamForNonEmptyBatch()
    {
        int* second = stackalloc int[1];
        int* third = stackalloc int[1];
        int* fourth = stackalloc int[1];

        var act = () => new NativeSoABatch4<int, int, int, int>(second, third, fourth, null, 1);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public unsafe void SoA4_SliceOutsideRange_Throws()
    {
        int* a = stackalloc int[2];
        int* b = stackalloc int[2];
        int* c = stackalloc int[2];
        int* d = stackalloc int[2];

        var batch = new NativeSoABatch4<int, int, int, int>(a, b, c, d, 2);

        var act = () => batch.Slice(1, 2);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
