namespace Axrone.Hash.Tests;

using Axrone.Hash;
using FluentAssertions;
using Xunit;

public sealed class Sha1Tests
{
    [Fact]
    public void KnownVectors()
    {
        Sha1Algorithm.Hash(ReadOnlySpan<byte>.Empty).ToString()
            .Should().Be("da39a3ee5e6b4b0d3255bfef95601890afd80709");
        Sha1Algorithm.Hash("abc"u8).ToString()
            .Should().Be("a9993e364706816aba3e25717850c26c9cd0d89d");
        Sha1Algorithm.Hash("The quick brown fox jumps over the lazy dog"u8).ToString()
            .Should().Be("2fd4e1c67a2d28fced849ee1bb76e7391b93eb12");
    }

    [Fact]
    public void IncrementalMatchesOneShot()
    {
        ReadOnlySpan<byte> data = "incremental sha-1 check"u8;
        string expected = Sha1Algorithm.Hash(data).ToString();

        var accumulator = new Sha1Accumulator();
        accumulator.Append(data[..5]);
        accumulator.Append(data[5..]);

        accumulator.GetDigest().ToString().Should().Be(expected);
    }

    [Fact]
    public void FactoryResolvesSha1()
    {
        using IHasher hasher = HashEngine.CreateHasher(HashAlgorithmId.Sha1);

        hasher.DigestLength.Should().Be(20);
        hasher.ComputeHash("abc"u8).AsSpan().ToArray()
            .Should().Equal(Convert.FromHexString("a9993e364706816aba3e25717850c26c9cd0d89d"));
    }

    [Fact]
    public void Digest160RoundTrips()
    {
        Digest160 digest = Sha1Algorithm.Hash("abc"u8);
        string text = digest.ToString();

        Digest160.TryParse(text, out Digest160 parsed).Should().BeTrue();
        parsed.Should().Be(digest);
        Digest160.ByteCount.Should().Be(20);
    }
}
