using System;
using System.Runtime.InteropServices;
using Axrone.Hash;

namespace Axrone.Hash.Tests;

public class Fnv1a64Tests
{
    [Fact]
    public void Empty_IsOffsetBasis()
    {
        Fnv1a64Algorithm.Hash(ReadOnlySpan<byte>.Empty).Value.Should().Be(14695981039346656037ul);
    }

    [Fact]
    public void KnownVector_A()
    {
        ReadOnlySpan<byte> data = [(byte)'a'];
        Fnv1a64Algorithm.Hash(data).Value.Should().Be(0xAF63DC4C8601EC8Cul);
    }

    [Fact]
    public void Incremental_SplitMatchesOneshot()
    {
        ReadOnlySpan<byte> data =
        [
            (byte)'f', (byte)'o', (byte)'o', (byte)'b', (byte)'a', (byte)'r'
        ];
        ulong expected = Fnv1a64Algorithm.Hash(data).Value;

        var acc = Fnv1a64Algorithm.CreateAccumulator();
        acc.Append(data[..3]);
        acc.Append(data[3..]);
        acc.GetDigest().Value.Should().Be(expected);
    }

    [Fact]
    public void Factory_ResolvesFnv1a64()
    {
        using var hasher = HashEngine.CreateHasher(HashAlgorithmId.Fnv1a64);
        hasher.DigestLength.Should().Be(8);
        ((ReadOnlySpan<byte>)hasher.ComputeHash([(byte)'a'])).ToArray()
            .Should().Equal(0x8C, 0xEC, 0x01, 0x86, 0x4C, 0xDC, 0x63, 0xAF);
    }

    [Fact]
    public void FloatLanes_HashBitPatterns()
    {
        static ulong HashOf(float a, float b)
        {
            Span<float> lanes = stackalloc float[2] { a, b };
            return Fnv1a64Algorithm.Hash(MemoryMarshal.AsBytes(lanes)).Value;
        }

        HashOf(0f, 0f).Should().NotBe(HashOf(1f, 1f));
        HashOf(0f, 0f).Should().Be(HashOf(0f, 0f));
    }
}
