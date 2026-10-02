using System.Runtime.InteropServices;
using Axrone.Hash;

namespace Axrone.Hash.Tests;

public class Fnv1a32Tests
{
    [Fact]
    public void Empty_IsOffsetBasis()
    {
        Fnv1a32.Compute(ReadOnlySpan<byte>.Empty).Should().Be(0x811C9DC5u);
    }

    [Fact]
    public void KnownVector_A()
    {
        ReadOnlySpan<byte> data = [(byte)'a'];
        Fnv1a32.Compute(data).Should().Be(0xE40C292Cu);
    }

    [Fact]
    public void FloatLanes_HashBitPatterns()
    {
        ReadOnlySpan<float> zeros = [0f, 0f];
        ReadOnlySpan<float> ones = [1f, 1f];

        Fnv1a32.Compute(zeros).Should().NotBe(Fnv1a32.Compute(ones));
        Fnv1a32.Compute(zeros).Should().Be(Fnv1a32.Compute([0f, 0f]));
    }

    [Fact]
    public void CharSpan_MatchesByteView()
    {
        const string source = "void main() { }";
        Fnv1a32.Compute(source.AsSpan()).Should().Be(
            Fnv1a32.Compute(MemoryMarshal.AsBytes(source.AsSpan())));
    }
}
