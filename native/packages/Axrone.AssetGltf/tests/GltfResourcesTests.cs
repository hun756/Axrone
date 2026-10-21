namespace Axrone.AssetGltf.Tests;

public sealed class GltfResourcesTests
{
    [Fact]
    public void Ktx2_TryParse_MinimalHeader_Succeeds()
    {
        byte[] payload = [9, 8, 7, 6];
        byte[] ktx2 = BuildKtx2(vkFormat: 145, width: 4, height: 4, payload: payload);

        bool ok = Ktx2Decoder.TryParse(ktx2, out GltfCompressedTexturePayload? parsed, out string? error);

        ok.Should().BeTrue(error);
        parsed.Should().NotBeNull();
        parsed!.VkFormat.Should().Be(145);
        parsed.Width.Should().Be(4);
        parsed.Height.Should().Be(4);
        parsed.LevelCount.Should().Be(1);
        parsed.Levels.Should().HaveCount(1);
        parsed.Levels[0].ToArray().Should().Equal(9, 8, 7, 6);
    }

    [Fact]
    public void Ktx2_TryParse_BadMagic_Fails()
    {
        byte[] bad = new byte[80];
        bool ok = Ktx2Decoder.TryParse(bad, out _, out string? error);
        ok.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void MapVkFormat_CoversKnownFormats()
    {
        Ktx2Decoder.MapVkFormat(145).Should().Be(GltfTextureFormat.BC7_RGBA);
        Ktx2Decoder.MapVkFormat(9999).Should().Be(GltfTextureFormat.Unknown);
    }

    [Fact]
    public void DecodeDataUri_Base64_RoundTrips()
    {
        ReadOnlyMemory<byte> bytes = GltfResourceRuntimeCache.DecodeDataUri("data:application/octet-stream;base64,AQID");
        bytes.ToArray().Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ResolveBuffer_GlbChunk_ReturnsSlice()
    {
        byte[] bin = [10, 20, 30, 40, 50];
        var cache = new GltfResourceRuntimeCache(bin, resolver: null);
        var def = new GltfRawModel.Buffer { ByteLength = 4 };

        ReadOnlyMemory<byte> resolved = await cache.ResolveBufferAsync(0, def, CancellationToken.None);

        resolved.ToArray().Should().Equal(10, 20, 30, 40);
    }

    private static byte[] BuildKtx2(int vkFormat, int width, int height, byte[] payload)
    {
        const int headerSize = 80;
        const int levelEntrySize = 24;
        int dataOffset = headerSize + levelEntrySize;
        byte[] outBuf = new byte[dataOffset + payload.Length];

        ReadOnlySpan<byte> id = [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];
        id.CopyTo(outBuf.AsSpan(0, 12));
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(12, 4), (uint)vkFormat);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(20, 4), (uint)width);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(24, 4), (uint)height);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(28, 4), 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(32, 4), 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(36, 4), 1u);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(40, 4), 1u);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(44, 4), 0u);
        BinaryPrimitives.WriteUInt64LittleEndian(outBuf.AsSpan(80, 8), (ulong)dataOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(outBuf.AsSpan(88, 8), (ulong)payload.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(outBuf.AsSpan(96, 8), (ulong)payload.Length);
        Buffer.BlockCopy(payload, 0, outBuf, dataOffset, payload.Length);
        return outBuf;
    }
}
