namespace Axrone.Raycast.Tests;

using FluentAssertions;
using Xunit;

public class AlignedMemoryBlockTests
{
    [Fact]
    public void Allocate_SpanRoundTrips()
    {
        using var block = new AlignedMemoryBlock<int>(16);

        block.Length.Should().Be(16);
        block.AsSpan().Length.Should().Be(16);

        block.AsSpan().Fill(7);
        block.AsSpan()[5].Should().Be(7);
    }

    [Fact]
    public void Allocate_Zeroes()
    {
        using var block = new AlignedMemoryBlock<int>(8);

        block.AsSpan().ToArray().Should().AllSatisfy(v => v.Should().Be(0));
    }

    [Fact]
    public unsafe void Pointer_MatchesSpan()
    {
        using var block = new AlignedMemoryBlock<int>(4);

        block.AsSpan()[0] = 42;
        (*block.Pointer).Should().Be(42);
    }

    [Fact]
    public void BadLength_Throws()
    {
        Action zero = () => _ = new AlignedMemoryBlock<int>(0);
        Action negative = () => _ = new AlignedMemoryBlock<int>(-3);

        zero.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Dispose_IsIdempotent_And_PoisonsAccess()
    {
        var block = new AlignedMemoryBlock<int>(4);
        block.Dispose();

        Action again = () => block.Dispose();
        again.Should().NotThrow();

        Action span = () => _ = block.AsSpan();
        span.Should().Throw<ObjectDisposedException>();
    }
}
