using System.Runtime.InteropServices;
using Axrone.Hash;

namespace Axrone.Hash.Tests;

public class Fnv1a32Tests
{
    [Fact]
    public void Empty_IsOffsetBasis()
    {
        Fnv1a32Algorithm.Hash(ReadOnlySpan<byte>.Empty).Value.Should().Be(0x811C9DC5u);
    }

    [Fact]
    public void KnownVector_A()
    {
        ReadOnlySpan<byte> data = [(byte)'a'];
        Fnv1a32Algorithm.Hash(data).Value.Should().Be(0xE40C292Cu);
    }

    [Fact]
    public void Incremental_SplitMatchesOneshot()
    {
        ReadOnlySpan<byte> data = [(byte)'f', (byte)'o', (byte)'o', (byte)'b', (byte)'a', (byte)'r'];
        uint expected = Fnv1a32Algorithm.Hash(data).Value;

        var acc = Fnv1a32Algorithm.CreateAccumulator();
        acc.Append(data[..3]);
        acc.Append(data[3..]);
        acc.GetDigest().Value.Should().Be(expected);
    }

    [Fact]
    public void Factory_ResolvesFnv1a32()
    {
        using var hasher = HashEngine.CreateHasher(HashAlgorithmId.Fnv1a32);
        hasher.DigestLength.Should().Be(4);
        ((ReadOnlySpan<byte>)hasher.ComputeHash([(byte)'a'])).ToArray()
            .Should().Equal(0x2C, 0x29, 0x0C, 0xE4);
    }

    [Fact]
    public void FloatLanes_HashBitPatterns()
    {
        static uint HashOf(float a, float b)
        {
            Span<float> lanes = stackalloc float[2] { a, b };
            return Fnv1a32Algorithm.Hash(MemoryMarshal.AsBytes(lanes)).Value;
        }

        HashOf(0f, 0f).Should().NotBe(HashOf(1f, 1f));
        HashOf(0f, 0f).Should().Be(HashOf(0f, 0f));
    }
}
