namespace Axrone.Hash.Tests;

using Axrone.Hash;
using FluentAssertions;
using Xunit;

public sealed class NonCryptoHashes32Tests
{
    private static readonly byte[] Abc = "abc"u8.ToArray();

    [Fact]
    public void Crc32_KnownVectors()
    {
        Crc32Algorithm.Hash(ReadOnlySpan<byte>.Empty).Value.Should().Be(0u);
        Crc32Algorithm.Hash("123456789"u8).Value.Should().Be(0xCBF43926u);
        Crc32Algorithm.Hash("a"u8).Value.Should().Be(0xE8B7BE43u);
        Crc32Algorithm.Hash(Abc).Value.Should().Be(0x352441C2u);
        Crc32Algorithm.Hash("message digest"u8).Value.Should().Be(0x20159D7Fu);
    }

    [Fact]
    public void Crc32c_KnownVectors()
    {
        Crc32cAlgorithm.Hash("123456789"u8).Value.Should().Be(0xE3069283u);
        Crc32cAlgorithm.Hash(Abc).Value.Should().Be(0x364B3FB7u);
    }

    [Fact]
    public void Djb2AndDjb2a_KnownVectors()
    {
        Djb2Algorithm.Hash(ReadOnlySpan<byte>.Empty).Value.Should().Be(5381u);
        Djb2Algorithm.Hash("a"u8).Value.Should().Be(177670u);
        Djb2aAlgorithm.Hash(ReadOnlySpan<byte>.Empty).Value.Should().Be(5381u);
        Djb2aAlgorithm.Hash("a"u8).Value.Should().Be(180708u);
        Djb2Algorithm.Hash("hello"u8).Value.Should().NotBe(Djb2aAlgorithm.Hash("hello"u8).Value);
    }

    [Fact]
    public void Sdbm_KnownVectors()
    {
        SdbmAlgorithm.Hash(ReadOnlySpan<byte>.Empty).Value.Should().Be(0u);
        SdbmAlgorithm.Hash("a"u8).Value.Should().Be(97u);
    }

    [Fact]
    public void Fnv1_KnownVectors()
    {
        Fnv1_32Algorithm.Hash(ReadOnlySpan<byte>.Empty).Value.Should().Be(0x811C9DC5u);
        Fnv1_32Algorithm.Hash("a"u8).Value.Should().Be(0x050C5D7Eu);
        Fnv1_32Algorithm.Hash("a"u8).Value.Should().NotBe(Fnv1a32Algorithm.Hash("a"u8).Value);
    }

    [Fact]
    public void Murmur3_32_KnownVectors()
    {
        Murmur3_32Algorithm.Hash(ReadOnlySpan<byte>.Empty).Value.Should().Be(0u);
        Murmur3_32Algorithm.Hash("a"u8).Value.Should().Be(0x3C2569B2u);
        Murmur3_32Algorithm.Hash(Abc).Value.Should().Be(0xB3DD93FAu);
        Murmur3_32Algorithm.Hash("abcd"u8).Value.Should().Be(0x43ED676Au);
        Murmur3_32Algorithm.Hash("Hello, world!"u8).Value.Should().Be(0xC0363E43u);
    }

    [Fact]
    public void XxHash32_KnownVectors()
    {
        XxHash32Algorithm.Hash(ReadOnlySpan<byte>.Empty).Value.Should().Be(0x02CC5D05u);
        XxHash32Algorithm.Hash("a"u8).Value.Should().Be(0x550D7456u);
        XxHash32Algorithm.Hash(Abc).Value.Should().Be(0x32D153FFu);
    }

    [Theory]
    [InlineData(HashAlgorithmId.Crc32)]
    [InlineData(HashAlgorithmId.Crc32c)]
    [InlineData(HashAlgorithmId.Djb2)]
    [InlineData(HashAlgorithmId.Djb2a)]
    [InlineData(HashAlgorithmId.Sdbm)]
    [InlineData(HashAlgorithmId.Fnv1_32)]
    [InlineData(HashAlgorithmId.Murmur3_32)]
    [InlineData(HashAlgorithmId.XxHash32)]
    public void IncrementalSplitMatchesOneShot(HashAlgorithmId id)
    {
        byte[] data = "the quick brown fox jumps over the lazy dog"u8.ToArray();
        uint expected = OneShot(id, data);

        using IHasher hasher = HashEngine.CreateHasher(id);
        hasher.DigestLength.Should().Be(4);
        hasher.Append(data.AsSpan(0, 7));
        hasher.Append(data.AsSpan(7, 13));
        hasher.Append(data.AsSpan(20));
        Digest32 digest = Digest32.FromSpan(hasher.GetCurrentDigest().AsSpan());
        digest.Value.Should().Be(expected);
    }

    [Fact]
    public void Crc32_SeedChangesTheDigest()
    {
        uint plain = Crc32Algorithm.Hash("abc"u8).Value;
        var seeded = new Crc32Accumulator(123);
        seeded.Append("abc"u8);
        seeded.GetDigest().Value.Should().NotBe(plain);
    }

    private static uint OneShot(HashAlgorithmId id, ReadOnlySpan<byte> data) => id switch
    {
        HashAlgorithmId.Crc32 => Crc32Algorithm.Hash(data).Value,
        HashAlgorithmId.Crc32c => Crc32cAlgorithm.Hash(data).Value,
        HashAlgorithmId.Djb2 => Djb2Algorithm.Hash(data).Value,
        HashAlgorithmId.Djb2a => Djb2aAlgorithm.Hash(data).Value,
        HashAlgorithmId.Sdbm => SdbmAlgorithm.Hash(data).Value,
        HashAlgorithmId.Fnv1_32 => Fnv1_32Algorithm.Hash(data).Value,
        HashAlgorithmId.Murmur3_32 => Murmur3_32Algorithm.Hash(data).Value,
        HashAlgorithmId.XxHash32 => XxHash32Algorithm.Hash(data).Value,
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };
}
