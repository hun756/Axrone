namespace Axrone.Utility.Tests.Alignment;

using System.Numerics;
using Axrone.Utility.Alignment;

public class ByteSizeFactoryTests
{
    [Fact]
    public void Zero_IsZero()
    {
        ByteSize.Zero.Value.Should().Be((nuint)0);
    }

    [Fact]
    public void From_Factories_ScaleCorrectly()
    {
        ByteSize.FromBytes(512).Value.Should().Be((nuint)512);
        ByteSize.FromKilobytes(2).Value.Should().Be((nuint)2048);
        ByteSize.FromMegabytes(3).Value.Should().Be((nuint)(3 * 1024 * 1024));
        ByteSize.From(7).Value.Should().Be((nuint)7);
    }

    [Fact]
    public void FromKilobytes_Overflow_Throws()
    {
        Action act = () => ByteSize.FromKilobytes(nuint.MaxValue);

        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public void PointerAlignment_MatchesPointerSize()
    {
        Alignment.PointerAlignment.Value.Should().Be((uint)UIntPtr.Size);
        BitOperations.IsPow2(Alignment.PointerAlignment.Value).Should().BeTrue();
    }
}
