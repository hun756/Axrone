using System.Numerics;
using Axrone.Hash;

namespace Axrone.Hash.Tests;

public class HashMixingTests
{
    [Fact]
    public void Fmix64_Zero_StaysZero()
    {
        HashMixing.Fmix64(0UL).Should().Be(0UL);
    }

    [Fact]
    public void Fmix64_IsDeterministic()
    {
        const ulong key = 0x123456789ABCDEF0UL;

        HashMixing.Fmix64(key).Should().Be(HashMixing.Fmix64(key));
    }

    [Fact]
    public void Fmix64_AvalanchesSingleBitFlips()
    {
        ulong[] keys = [1UL, 0x9E3779B97F4A7C15UL, 0xFFFFFFFFFFFFFFFFUL, 0x8000000000000000UL];

        foreach (ulong key in keys)
        {
            ulong mixed = HashMixing.Fmix64(key);
            ulong flipped = HashMixing.Fmix64(key ^ 1UL);
            int differing = BitOperations.PopCount(mixed ^ flipped);

            differing.Should().BeInRange(16, 48);
        }
    }
}
