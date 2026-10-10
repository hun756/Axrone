namespace Axrone.Utility.Tests.NativeBuffer;

using Axrone.Utility.NativeBuffer;

public class NativeBufferTests
{
    [Fact]
    public void Ctor_LiftsTinyRequests()
    {
        using NativeBuffer<int> buffer = new(0);

        buffer.Capacity.Should().BeGreaterThanOrEqualTo((nuint)16);
        buffer.Length.Should().Be((nuint)0);
    }

    [Fact]
    public void Append_Grows_PreservingElements()
    {
        using NativeBuffer<int> buffer = new(2);

        for (int i = 0; i < 100; i++)
        {
            buffer.Append(i);
        }

        buffer.Length.Should().Be((nuint)100);
        buffer.Capacity.Should().BeGreaterThanOrEqualTo((nuint)100);
        buffer.AsRef(99).Should().Be(99);
        buffer.AsSpan().ToArray().Should().Equal(Enumerable.Range(0, 100).ToArray());
        buffer.AsReadOnlySpan().Length.Should().Be(100);
    }

    [Fact]
    public void AsRef_OutOfRange_Throws()
    {
        using NativeBuffer<int> buffer = new(4);
        buffer.Append(1);

        Action act = () => buffer.AsRef(7);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetLength_Grows_AndClear_Resets()
    {
        using NativeBuffer<int> buffer = new(4);

        buffer.SetLength(64);
        buffer.Length.Should().Be((nuint)64);
        buffer.Capacity.Should().BeGreaterThanOrEqualTo((nuint)64);

        buffer.Clear();
        buffer.Length.Should().Be((nuint)0);
    }

    [Fact]
    public void EnsureCapacity_IsIdempotentBelowCapacity()
    {
        using NativeBuffer<int> buffer = new(32);

        buffer.EnsureCapacity(8);

        buffer.Capacity.Should().BeGreaterThanOrEqualTo((nuint)32);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        NativeBuffer<int> buffer = new(8);
        buffer.Append(1);

        buffer.Dispose();
        Action again = () => buffer.Dispose();

        again.Should().NotThrow();
    }
}
