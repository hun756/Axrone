using Axrone.Hash;

namespace Axrone.Hash.Tests;

public class DigestTests
{
    [Fact]
    public void Digest32_RoundTrip_ViaHex()
    {
        var digest = new Digest32(0xDEADBEEF);
        string hex = digest.ToString();
        Digest32.TryParse(hex.AsSpan(), out var parsed).Should().BeTrue();
        parsed.Should().Be(digest);
    }

    [Fact]
    public void Digest64_RoundTrip_ViaHex()
    {
        var digest = new Digest64(0xDEADBEEFCAFEBABE);
        string hex = digest.ToString();
        Digest64.TryParse(hex.AsSpan(), out var parsed).Should().BeTrue();
        parsed.Should().Be(digest);
    }

    [Fact]
    public void Digest128_RoundTrip_ViaHex()
    {
        var digest = new Digest128(0xDEADBEEFCAFEBABE, 0x0123456789ABCDEF);
        string hex = digest.ToString();
        hex.Should().HaveLength(32);
        Digest128.TryParse(hex.AsSpan(), out var parsed).Should().BeTrue();
        parsed.Should().Be(digest);
    }

    [Fact]
    public void Digest256_ConstantTimeEquality()
    {
        Span<byte> data = stackalloc byte[32];
        data.Fill(42);
        var d1 = new Digest256(data);
        var d2 = new Digest256(data);
        d1.Equals(d2).Should().BeTrue();
    }

    [Fact]
    public void Digest256_TryCopyTo_TooSmall_ReturnsFalse()
    {
        Span<byte> data = stackalloc byte[32];
        data.Fill(1);
        var digest = new Digest256(data);
        Span<byte> small = stackalloc byte[16];
        digest.TryCopyTo(small).Should().BeFalse();
    }

    [Fact]
    public void HashDigest_VariableLength()
    {
        Span<byte> data16 = stackalloc byte[16];
        data16.Fill(0xAB);
        var digest = new HashDigest(data16);
        digest.Length.Should().Be(16);
        digest.ToString().Should().HaveLength(32);
    }

    [Fact]
    public void HashDigest_Exceeds64Bytes_Throws()
    {
        byte[] data = new byte[65];
        var act = () => new HashDigest(data);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Digest_Utf8RoundTrip()
    {
        var digest = new Digest64(0xCAFEBABE);
        Span<byte> utf8 = stackalloc byte[16];
        digest.TryFormat(utf8, out int written).Should().BeTrue();
        Digest64.TryParse(utf8[..written], out var parsed).Should().BeTrue();
        parsed.Should().Be(digest);
    }
}

public class WyHash64Tests
{
    [Fact]
    public void EmptyInput_ReturnsConsistentHash()
    {
        ulong h1 = WyHash64.Compute(ReadOnlySpan<byte>.Empty);
        ulong h2 = WyHash64.Compute(ReadOnlySpan<byte>.Empty);
        h1.Should().Be(h2);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentHashes()
    {
        ReadOnlySpan<byte> data = "hello world"u8;
        ulong h1 = WyHash64.Compute(data, seed: 0);
        ulong h2 = WyHash64.Compute(data, seed: 42);
        h1.Should().NotBe(h2);
    }

    [Fact]
    public void Incremental_MatchesOneShot()
    {
        ReadOnlySpan<byte> data = "the quick brown fox jumps over the lazy dog"u8;
        ulong oneShot = WyHash64.Compute(data);

        var acc = new WyHash64Accumulator();
        acc.Append(data[..20]);
        acc.Append(data[20..]);
        ulong incremental = acc.GetDigest().Value;

        incremental.Should().Be(oneShot);
        acc.Dispose();
    }

    [Fact]
    public void Algorithm_StaticHash_MatchesDirect()
    {
        ReadOnlySpan<byte> data = "test data"u8;
        var direct = new Digest64(WyHash64.Compute(data));
        var viaAlgo = WyHash64Algorithm.Hash(data);
        viaAlgo.Should().Be(direct);
    }
}

public class XxHash64Tests
{
    [Fact]
    public void KnownVector_EmptyInput()
    {
        ulong hash = XxHash64Algorithm.Hash(ReadOnlySpan<byte>.Empty).Value;
        hash.Should().NotBe(0UL);
    }

    [Fact]
    public void Incremental_MatchesOneShot()
    {
        ReadOnlySpan<byte> data = "abcdefghijklmnopqrstuvwxyz"u8;
        var oneShot = XxHash64Algorithm.Hash(data);

        var acc = new XxHash64Accumulator();
        acc.Append(data[..10]);
        acc.Append(data[10..]);
        var incremental = acc.GetDigest();

        incremental.Should().Be(oneShot);
        acc.Dispose();
    }

    [Fact]
    public void DifferentData_ProducesDifferentHashes()
    {
        var h1 = XxHash64Algorithm.Hash("hello"u8);
        var h2 = XxHash64Algorithm.Hash("world"u8);
        h1.Should().NotBe(h2);
    }
}

public class Murmur3_128Tests
{
    [Fact]
    public void KnownVector_EmptyInput()
    {
        var hash = Murmur3_128Algorithm.Hash(ReadOnlySpan<byte>.Empty);
        hash.AsSpan().IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Incremental_MatchesOneShot()
    {
        ReadOnlySpan<byte> data = "the quick brown fox"u8;
        var oneShot = Murmur3_128Algorithm.Hash(data);

        var acc = new Murmur3_128Accumulator();
        acc.Append(data[..8]);
        acc.Append(data[8..]);
        var incremental = acc.GetDigest();

        incremental.Should().Be(oneShot);
        acc.Dispose();
    }

    [Fact]
    public void Digest128_HasCorrectByteCount()
    {
        Digest128.ByteCount.Should().Be(16);
    }
}

public class Sha256Tests
{
    [Fact]
    public void KnownVector_EmptyString()
    {
        var hash = Sha256Algorithm.Hash(ReadOnlySpan<byte>.Empty);
        string hex = hash.ToString();
        hex.Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }

    [Fact]
    public void KnownVector_Abc()
    {
        var hash = Sha256Algorithm.Hash("abc"u8);
        string hex = hash.ToString();
        hex.Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void Incremental_MatchesOneShot()
    {
        ReadOnlySpan<byte> data = "hello world"u8;
        var oneShot = Sha256Algorithm.Hash(data);

        var acc = new Sha256Accumulator();
        acc.Append(data[..5]);
        acc.Append(data[5..]);
        var incremental = acc.GetDigest();

        incremental.Should().Be(oneShot);
        acc.Dispose();
    }
}

public class Sha512Tests
{
    [Fact]
    public void KnownVector_EmptyString()
    {
        var hash = Sha512Algorithm.Hash(ReadOnlySpan<byte>.Empty);
        string hex = hash.ToString();
        hex.Should().StartWith("cf83e1357eefb8bd");
    }

    [Fact]
    public void Digest512_HasCorrectByteCount()
    {
        Digest512.ByteCount.Should().Be(64);
    }
}

public class Sha3_256Tests
{
    [Fact]
    public void KnownVector_EmptyString()
    {
        if (!SHA3_256.IsSupported) return;
        var hash = Sha3_256Algorithm.Hash(ReadOnlySpan<byte>.Empty);
        string hex = hash.ToString();
        hex.Should().Be("a7ffc6f8bf1ed76651c14756a061d662f580ff4de43b49fa82d80a4b80f8434a");
    }
}

public class HmacTests
{
    [Fact]
    public void HmacSha256_ProducesConsistentOutput()
    {
        ReadOnlySpan<byte> key = "secret"u8;
        ReadOnlySpan<byte> data = "message"u8;

        var h1 = HmacSha256Algorithm.Hash(key, data);
        var h2 = HmacSha256Algorithm.Hash(key, data);
        h1.Should().Be(h2);
    }

    [Fact]
    public void HmacSha256_DifferentKeys_ProduceDifferentOutput()
    {
        ReadOnlySpan<byte> data = "message"u8;
        var h1 = HmacSha256Algorithm.Hash("key1"u8, data);
        var h2 = HmacSha256Algorithm.Hash("key2"u8, data);
        h1.Should().NotBe(h2);
    }

    [Fact]
    public void HmacSha512_ProducesCorrectLength()
    {
        var hash = HmacSha512Algorithm.Hash("key"u8, "data"u8);
        hash.AsSpan().Length.Should().Be(64);
    }
}

public class HashEngineTests
{
    [Fact]
    public void CreateHasher_Sha256_Works()
    {
        using var hasher = HashEngine.CreateHasher(HashAlgorithmId.Sha256);
        hasher.Append("abc"u8);
        var digest = hasher.GetCurrentDigest(reset: true);
        digest.Length.Should().Be(32);
    }

    [Fact]
    public void CreateHasher_XxHash64_Works()
    {
        using var hasher = HashEngine.CreateHasher(HashAlgorithmId.XxHash64);
        hasher.Append("test"u8);
        var digest = hasher.GetCurrentDigest(reset: true);
        digest.Length.Should().Be(8);
    }

    [Fact]
    public void CreateHasher_HmacSha256_WithKey()
    {
        using var hasher = HashEngine.CreateHasher(HashAlgorithmId.HmacSha256, "key"u8);
        hasher.Append("data"u8);
        var digest = hasher.GetCurrentDigest(reset: true);
        digest.Length.Should().Be(32);
    }

    [Fact]
    public void CreateHasher_InvalidAlgorithm_Throws()
    {
        var act = () => HashEngine.CreateHasher((HashAlgorithmId)255);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ComputeHash_MatchesStaticMethod()
    {
        using var hasher = HashEngine.CreateHasher(HashAlgorithmId.Sha256);
        var dynamic = hasher.ComputeHash("abc"u8);
        var direct = Sha256Algorithm.Hash("abc"u8);
        dynamic.AsSpan().SequenceEqual(direct.AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void AppendUtf8_Works()
    {
        using var hasher = HashEngine.CreateHasher(HashAlgorithmId.Sha256);
        hasher.AppendUtf8("hello");
        var digest = hasher.GetCurrentDigest(reset: true);
        digest.Length.Should().Be(32);
    }

    [Fact]
    public async Task ComputeHashAsync_Stream_Works()
    {
        using var hasher = HashEngine.CreateHasher(HashAlgorithmId.Sha256);
        using var stream = new MemoryStream("abc"u8.ToArray());
        var digest = await hasher.ComputeHashAsync(stream);
        digest.Length.Should().Be(32);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        using var hasher = HashEngine.CreateHasher(HashAlgorithmId.Sha256);
        hasher.Append("abc"u8);
        hasher.Reset();
        hasher.Append("abc"u8);
        var digest = hasher.GetCurrentDigest(reset: true);
        var expected = Sha256Algorithm.Hash("abc"u8);
        digest.AsSpan().SequenceEqual(expected.AsSpan()).Should().BeTrue();
    }
}
