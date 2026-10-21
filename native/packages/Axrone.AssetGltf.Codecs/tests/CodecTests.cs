namespace Axrone.AssetGltf.Codecs.Tests;

public sealed class CodecTests
{
    [Fact]
    public void MeshoptPolicy_RoundTripsVertexBuffer()
    {
        const int vertexCount = 4;
        const int stride = 8;
        byte[] source = new byte[vertexCount * stride];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = (byte)(i * 7);
        }

        nuint bound = MeshOptimizer.Meshopt.EncodeVertexBufferBound((nuint)vertexCount, (nuint)stride);
        byte[] encoded = new byte[(int)bound];
        nuint encodedSize;
        unsafe
        {
            fixed (byte* destination = encoded)
            fixed (byte* vertices = source)
            {
                encodedSize = MeshOptimizer.Meshopt.EncodeVertexBuffer(
                    destination,
                    (nuint)encoded.Length,
                    vertices,
                    (nuint)vertexCount,
                    (nuint)stride);
            }
        }

        byte[] decoded = new byte[source.Length];
        bool result = MeshoptCompressionPolicy.TryDecompressBufferView(
            encoded.AsSpan(0, (int)encodedSize),
            vertexCount,
            stride,
            "ATTRIBUTES",
            "NONE",
            decoded);

        result.Should().BeTrue();
        decoded.Should().Equal(source);
    }

    [Fact]
    public async Task MeshoptPolicy_ImportsGlbPrimitive()
    {
        const int vertexCount = 3;
        const int stride = 12;
        float[] positions = [0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f];
        byte[] vertexBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(positions.AsSpan()).ToArray();

        nuint bound = MeshOptimizer.Meshopt.EncodeVertexBufferBound((nuint)vertexCount, (nuint)stride);
        byte[] encoded = new byte[(int)bound];
        nuint encodedSize;
        unsafe
        {
            fixed (byte* destination = encoded)
            fixed (byte* vertices = vertexBytes)
            {
                encodedSize = MeshOptimizer.Meshopt.EncodeVertexBuffer(
                    destination,
                    (nuint)encoded.Length,
                    vertices,
                    (nuint)vertexCount,
                    (nuint)stride);
            }
        }

        string json = $$$"""
        {
          "asset": {"version": "2.0"},
          "extensionsUsed": ["EXT_meshopt_compression"],
          "extensionsRequired": ["EXT_meshopt_compression"],
          "scene": 0,
          "scenes": [{"nodes": [0]}],
          "nodes": [{"name": "Meshopt", "mesh": 0}],
          "meshes": [{"primitives": [{"attributes": {"POSITION": 0}}]}],
          "accessors": [{"bufferView": 0, "componentType": 5126, "count": {{{vertexCount}}}, "type": "VEC3"}],
          "bufferViews": [{
            "buffer": 0,
            "byteOffset": 0,
            "byteLength": {{{vertexCount * stride}}},
            "extensions": {
              "EXT_meshopt_compression": {
                "buffer": 0,
                "byteOffset": 0,
                "byteLength": {{{(int)encodedSize}}},
                "byteStride": {{{stride}}},
                "count": {{{vertexCount}}},
                "mode": "ATTRIBUTES",
                "filter": "NONE"
              }
            }
          }],
          "buffers": [{"byteLength": {{{(int)encodedSize}}}}]
        }
        """;

        byte[] glb = Glb(json, encoded[..(int)encodedSize]);
        var importer = new GltfImporter(new GltfImporterOptions { Id = "meshopt.glb" });
        GltfImportResult result = await importer
            .ImportAsync<NoDracoCompressionPolicy, MeshoptCompressionPolicy, PassthroughTranscoderPolicy>(glb);

        result.Diagnostics.Should().BeEmpty();
        result.Meshes[0].Definition.VertexCount.Should().Be(vertexCount);
        ReadOnlySpan<float> decoded = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(
            result.Meshes[0].Definition.Vertices.Span);
        decoded.ToArray().Should().Equal(positions);
    }


    [Fact]
    public void BasisPolicy_TranscodesKtx2ToBc7()
    {
        byte[] rgba = new byte[4 * 4 * 4];
        for (int i = 0; i < rgba.Length; i++)
        {
            rgba[i] = (byte)(i * 11);
        }

        byte[] ktx2 = BasisUniversal.BasisUniversalCodec.EncodeKtx2(
            rgba,
            4,
            4,
            new BasisUniversal.BasisEncoderOptions
            {
                Format = BasisUniversal.BasisTextureFormat.UastcLdr4x4,
                QualityLevel = 50,
            });

        bool result = BasisKtx2TranscoderPolicy.TryTranscode(
            ktx2,
            GltfTextureFormat.Unknown,
            GltfTextureFormat.BC7_RGBA,
            out GltfTranscodeOutput? output,
            out string? failureReason);

        result.Should().BeTrue(failureReason);
        output.Should().NotBeNull();
        output!.Value.Format.Should().Be(GltfTextureFormat.BC7_RGBA);
        output.Value.Payload.Should().BeOfType<GltfTranscodedTexturePayload>()
            .Which.Levels.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BasisPolicy_TranscodesDuringGlbImport()
    {
        byte[] rgba = new byte[4 * 4 * 4];
        for (int i = 0; i < rgba.Length; i++)
        {
            rgba[i] = (byte)(i * 7);
        }

        byte[] ktx2 = BasisUniversal.BasisUniversalCodec.EncodeKtx2(
            rgba,
            4,
            4,
            new BasisUniversal.BasisEncoderOptions
            {
                Format = BasisUniversal.BasisTextureFormat.UastcLdr4x4,
                QualityLevel = 50,
            });

        string json = $$$"""
        {
          "asset": {"version": "2.0"},
          "extensionsUsed": ["KHR_texture_basisu"],
          "images": [{"bufferView": 0, "mimeType": "image/ktx2"}],
          "textures": [{"source": 0}],
          "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": {{{ktx2.Length}}}}],
          "buffers": [{"byteLength": {{{ktx2.Length}}}}]
        }
        """;

        byte[] glb = Glb(json, ktx2);
        var importer = new GltfImporter(new GltfImporterOptions
        {
            Id = "ktx2.glb",
            TranscodeTargetFormat = GltfTextureFormat.BC7_RGBA,
        });
        GltfImportResult result = await importer
            .ImportAsync<NoDracoCompressionPolicy, NoMeshoptCompressionPolicy, BasisKtx2TranscoderPolicy>(glb);

        result.Diagnostics.Should().BeEmpty();
        result.Textures[0].RuntimeFormat.Should().Be(GltfTextureFormat.BC7_RGBA);
        result.Textures[0].Payload.Should().BeOfType<GltfTranscodedTexturePayload>();
    }

    private static byte[] Glb(string json, byte[] bin)
    {
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
        int jsonPad = (4 - (jsonBytes.Length % 4)) % 4;
        int binPad = (4 - (bin.Length % 4)) % 4;
        int total = 12 + 8 + jsonBytes.Length + jsonPad + 8 + bin.Length + binPad;
        byte[] glb = new byte[total];
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(0, 4), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8, 4), (uint)total);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12, 4), (uint)(jsonBytes.Length + jsonPad));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16, 4), 0x4E4F534A);
        jsonBytes.CopyTo(glb, 20);
        for (int i = 0; i < jsonPad; i++)
        {
            glb[20 + jsonBytes.Length + i] = 0x20;
        }

        int offset = 20 + jsonBytes.Length + jsonPad;
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(offset, 4), (uint)(bin.Length + binPad));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(offset + 4, 4), 0x004E4942);
        bin.CopyTo(glb, offset + 8);
        return glb;
    }
}
