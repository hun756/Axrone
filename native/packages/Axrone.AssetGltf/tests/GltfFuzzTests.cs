namespace Axrone.AssetGltf.Tests;

/// <summary>
/// Deterministic mutation smoke: untrusted or corrupted input must fail with a
/// coded <see cref="GltfException"/>, never with an internal exception type.
/// </summary>
public sealed class GltfFuzzTests
{
    [Fact]
    public async Task Import_MutatedGlb_OnlyThrowsCodedErrors()
    {
        byte[] baseline = BuildTriangleGlb();
        var importer = new GltfImporter(new GltfImporterOptions
        {
            Id = "fuzz.glb",
            Limits = new GltfImportLimits
            {
                MaxSourceBytes = 1 << 20,
                MaxBufferBytes = 1 << 20,
                MaxDecodedBytes = 1 << 20,
            },
        });

#pragma warning disable CA5394 // Deterministic PRNG keeps fuzz failures reproducible.
        var random = new Random(20261022);
        for (int iteration = 0; iteration < 500; iteration++)
        {
            byte[] mutated = (byte[])baseline.Clone();
            int mutations = random.Next(1, 6);
            for (int m = 0; m < mutations; m++)
            {
                mutated[random.Next(mutated.Length)] = (byte)random.Next(256);
            }

            try
            {
                await importer.ImportAsync(mutated);
            }
            catch (GltfException)
            {
                // Coded failures are the contract for untrusted input.
            }
            catch (Exception exception)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Import threw an uncoded exception at iteration {iteration}:{Environment.NewLine}{exception}");
            }
        }
#pragma warning restore CA5394
    }

    [Fact]
    public void DecodeDataUri_Base64_RoundTrips()
    {
        GltfResourceRuntimeCache
            .DecodeDataUri("data:application/octet-stream;base64,AQID")
            .ToArray()
            .Should().Equal(1, 2, 3);
    }

    [Theory]
    [InlineData("data:application/octet-stream;base64,!!!!")]
    [InlineData("not-a-data-uri,abc")]
    [InlineData("data:text/plain")]
    public void DecodeDataUri_Malformed_ThrowsCodedError(string candidate)
    {
        Action act = () => GltfResourceRuntimeCache.DecodeDataUri(candidate);

        act.Should().Throw<GltfException>();
    }

    private static byte[] BuildTriangleGlb()
    {
        const string json = """
            {
              "asset": {"version": "2.0"},
              "scene": 0,
              "scenes": [{"nodes": [0]}],
              "nodes": [{"name": "N", "mesh": 0}],
              "meshes": [{"primitives": [{"attributes": {"POSITION": 0}, "indices": 1}]}],
              "accessors": [
                {"bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3"},
                {"bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR"}
              ],
              "bufferViews": [
                {"buffer": 0, "byteOffset": 0, "byteLength": 36},
                {"buffer": 0, "byteOffset": 36, "byteLength": 6}
              ],
              "buffers": [{"byteLength": 44}]
            }
            """;

        byte[] bin = new byte[44];
        float[] positions = [0, 0, 0, 1, 0, 0, 0, 1, 0];
        Buffer.BlockCopy(positions, 0, bin, 0, 36);

        return Glb(System.Text.Encoding.UTF8.GetBytes(json), bin);
    }

    private static byte[] Glb(byte[] json, byte[] bin)
    {
        int jsonPad = (4 - (json.Length % 4)) % 4;
        int binPad = (4 - (bin.Length % 4)) % 4;
        int total = 12 + 8 + json.Length + jsonPad + 8 + bin.Length + binPad;
        byte[] glb = new byte[total];
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(0, 4), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4, 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8, 4), (uint)total);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12, 4), (uint)(json.Length + jsonPad));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16, 4), 0x4E4F534A);
        json.CopyTo(glb, 20);
        for (int i = 0; i < jsonPad; i++)
        {
            glb[20 + json.Length + i] = 0x20;
        }

        int offset = 20 + json.Length + jsonPad;
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(offset, 4), (uint)(bin.Length + binPad));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(offset + 4, 4), 0x004E4942);
        bin.CopyTo(glb, offset + 8);
        return glb;
    }
}
