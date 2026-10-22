using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Axrone.AssetGltf.Tests;

/// <summary>
/// KHR_draco_mesh_compression allows TRIANGLES and TRIANGLE_STRIP. The decoded
/// index stream must be expanded according to the primitive mode.
/// </summary>
public sealed class DracoTopologyTests
{
    [Fact]
    public async Task Import_DracoTriangleStrip_ExpandsToTriangles()
    {
        const string json = """
            {
              "asset": {"version": "2.0"},
              "extensionsUsed": ["KHR_draco_mesh_compression"],
              "extensionsRequired": ["KHR_draco_mesh_compression"],
              "scene": 0,
              "scenes": [{"nodes": [0]}],
              "nodes": [{"name": "N", "mesh": 0}],
              "meshes": [{"primitives": [{
                "attributes": {"POSITION": 0},
                "indices": 1,
                "mode": 5,
                "extensions": {
                  "KHR_draco_mesh_compression": {
                    "bufferView": 0,
                    "attributes": {"POSITION": 0}
                  }
                }
              }]}],
              "accessors": [
                {"componentType": 5126, "count": 6, "type": "VEC3"},
                {"componentType": 5123, "count": 6, "type": "SCALAR"}
              ],
              "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": 4}],
              "buffers": [{"byteLength": 4}]
            }
            """;

        byte[] glb = Glb(
            System.Text.Encoding.UTF8.GetBytes(json),
            [1, 0, 0, 0]);
        var importer = new GltfImporter(new GltfImporterOptions { Id = "draco-strip.glb" });
        GltfImportResult result = await importer
            .ImportAsync<StripDracoPolicy, NoMeshoptCompressionPolicy, PassthroughTranscoderPolicy>(glb);

        GltfMeshDefinition definition = result.Meshes[0].Definition;
        definition.Topology.Should().Be(GltfMeshTopology.Triangles);
        definition.VertexCount.Should().Be(6);
        definition.IndexCount.Should().Be(12);
        definition.Index16Bit.Should().BeTrue();

        MemoryMarshal.Cast<byte, ushort>(definition.Indices.Span).ToArray()
            .Should().Equal(0, 1, 2, 2, 1, 3, 2, 3, 4, 4, 3, 5);
    }

    /// <summary>Decodes a six-vertex triangle strip.</summary>
    private readonly struct StripDracoPolicy : IDracoMeshPolicy
    {
        public static bool IsSupported => true;

        public static GltfDracoDecodeStatus TryDecodeMesh(
            ReadOnlySpan<byte> dracoBytes,
            GltfDracoDecodeLimits limits,
            [NotNullWhen(true)] out GltfDracoDecodedMesh? mesh,
            out long decodedBytes)
        {
            float[] positions =
            [
                0, 0, 0,
                1, 0, 0,
                0, 1, 0,
                1, 1, 0,
                0, 2, 0,
                1, 2, 0,
            ];

            mesh = new GltfDracoDecodedMesh
            {
                VertexCount = 6,
                Attributes = ImmutableDictionary.CreateRange(new Dictionary<int, float[]>
                {
                    [0] = positions,
                }),
                AttributeDataTypes = ImmutableDictionary.CreateRange(new Dictionary<int, GltfDracoAttributeDataType>
                {
                    [0] = GltfDracoAttributeDataType.Float32,
                }),
                Indices = [0, 1, 2, 3, 4, 5],
            };
            decodedBytes = positions.Length * sizeof(float);
            return GltfDracoDecodeStatus.Success;
        }
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
