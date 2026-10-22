namespace Axrone.AssetGltf.Tests;

/// <summary>
/// Application-specific attributes (<c>_NAME</c>) stay in the interleaved
/// buffer and are described separately, without entering the standard
/// semantic array.
/// </summary>
public sealed class CustomAttributeTests
{
    [Fact]
    public async Task Import_CustomAttribute_PreservesInterleavedData()
    {
        const string json = """
            {
              "asset": {"version": "2.0"},
              "scene": 0,
              "scenes": [{"nodes": [0]}],
              "nodes": [{"name": "N", "mesh": 0}],
              "meshes": [{"primitives": [{
                "attributes": {"POSITION": 0, "_BATCHID": 1},
                "indices": 2
              }]}],
              "accessors": [
                {"bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3"},
                {"bufferView": 2, "componentType": 5121, "count": 3, "type": "SCALAR"},
                {"bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR"}
              ],
              "bufferViews": [
                {"buffer": 0, "byteOffset": 0, "byteLength": 36},
                {"buffer": 0, "byteOffset": 36, "byteLength": 6},
                {"buffer": 0, "byteOffset": 44, "byteLength": 3}
              ],
              "buffers": [{"byteLength": 48}]
            }
            """;

        byte[] bin = new byte[48];
        float[] positions = [0, 0, 0, 1, 0, 0, 0, 1, 0];
        Buffer.BlockCopy(positions, 0, bin, 0, 36);
        bin[44] = 0;
        bin[45] = 1;
        bin[46] = 2;

        var importer = new GltfImporter(new GltfImporterOptions { Id = "custom.glb" });
        GltfImportResult result = await importer.ImportAsync(Glb(System.Text.Encoding.UTF8.GetBytes(json), bin));

        GltfMeshDefinition definition = result.Meshes[0].Definition;
        definition.Attributes.Should().ContainSingle()
            .Which.Semantic.Should().Be(GltfMeshSemantic.Position);

        GltfCustomVertexAttribute custom = definition.CustomAttributes.Should().ContainSingle().Subject;
        custom.Name.Should().Be("_BATCHID");
        custom.Offset.Should().Be(12);
        custom.Stride.Should().Be(13);
        custom.ComponentType.Should().Be(5121);
        custom.ComponentCount.Should().Be(1);
        custom.Normalized.Should().BeFalse();
        custom.Integer.Should().BeTrue();

        ReadOnlySpan<byte> vertices = definition.Vertices.Span;
        vertices[custom.Offset].Should().Be(0);
        vertices[custom.Offset + custom.Stride].Should().Be(1);
        vertices[custom.Offset + (2 * custom.Stride)].Should().Be(2);

        result.Diagnostics.Should().NotContain(static d => d.Code == "gltf.mesh.attribute.unsupported");
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
