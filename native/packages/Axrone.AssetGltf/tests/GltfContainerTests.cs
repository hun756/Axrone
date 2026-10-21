namespace Axrone.AssetGltf.Tests;

public sealed class GltfContainerTests
{
    [Fact]
    public void Parse_RawJson_Passthrough()
    {
        byte[] json = System.Text.Encoding.UTF8.GetBytes("{\"asset\":{\"version\":\"2.0\"}}");
        var data = GltfContainer.Parse(json);
        data.Format.Should().Be("gltf");
        data.BinChunk.Should().BeNull();
        data.JsonBytes.Length.Should().Be(json.Length);
    }

    [Fact]
    public void Parse_MinimalGlb_SplitsChunks()
    {
        byte[] json = System.Text.Encoding.UTF8.GetBytes("{\"asset\":{\"version\":\"2.0\"}}");
        byte[] bin = [1, 2, 3, 4];
        byte[] glb = BuildGlb(json, bin);

        var data = GltfContainer.Parse(glb);
        data.Format.Should().Be("glb");
        data.JsonBytes.Length.Should().BeGreaterThanOrEqualTo(json.Length);
        data.JsonBytes.Span[..json.Length].ToArray().Should().Equal(json);
        data.BinChunk.Should().NotBeNull();
        data.BinChunk!.Value.Length.Should().BeGreaterThanOrEqualTo(4);
        data.BinChunk.Value.Span[..4].ToArray().Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void Parse_BadVersion_Throws()
    {
        byte[] json = System.Text.Encoding.UTF8.GetBytes("{}");
        byte[] glb = BuildGlb(json, null, version: 1);
        Action act = () => GltfContainer.Parse(glb);
        act.Should().Throw<GltfContainerError>();
    }

    [Fact]
    public void Parse_MissingJson_Throws()
    {
        byte[] bin = [9, 9];
        byte[] glb = BuildGlbBinOnly(bin);
        Action act = () => GltfContainer.Parse(glb);
        act.Should().Throw<GltfContainerError>();
    }

    private static byte[] BuildGlb(byte[] json, byte[]? bin, uint version = 2)
    {
        int jsonPad = (4 - (json.Length % 4)) % 4;
        int binPad = bin == null ? 0 : (4 - (bin.Length % 4)) % 4;
        int total = 12 + 8 + json.Length + jsonPad + (bin == null ? 0 : 8 + bin.Length + binPad);
        byte[] outBuf = new byte[total];
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(0, 4), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(4, 4), version);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(8, 4), (uint)total);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(12, 4), (uint)(json.Length + jsonPad));
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(16, 4), 0x4E4F534A);
        Buffer.BlockCopy(json, 0, outBuf, 20, json.Length);
        for (int i = 0; i < jsonPad; i++)
        {
            outBuf[20 + json.Length + i] = 0x20;
        }

        if (bin != null)
        {
            int off = 20 + json.Length + jsonPad;
            BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(off, 4), (uint)(bin.Length + binPad));
            BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(off + 4, 4), 0x004E4942);
            Buffer.BlockCopy(bin, 0, outBuf, off + 8, bin.Length);
        }

        return outBuf;
    }

    private static byte[] BuildGlbBinOnly(byte[] bin)
    {
        int total = 12 + 8 + bin.Length;
        byte[] outBuf = new byte[total];
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(0, 4), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(4, 4), 2u);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(8, 4), (uint)total);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(12, 4), (uint)bin.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(16, 4), 0x004E4942);
        Buffer.BlockCopy(bin, 0, outBuf, 20, bin.Length);
        return outBuf;
    }
}
