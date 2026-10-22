using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Axrone.AssetGltf.Tests;

/// <summary>
/// Draco stores normalized integer accessors as raw integers. The importer
/// must normalize them before re-quantizing the interleaved vertex buffer.
/// </summary>
public sealed class DracoNormalizationTests
{
    [Fact]
    public async Task Import_NormalizedByteColorFromDraco_PreservesRawValues()
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
                "attributes": {"POSITION": 0, "COLOR_0": 1},
                "extensions": {
                  "KHR_draco_mesh_compression": {
                    "bufferView": 0,
                    "attributes": {"POSITION": 0, "COLOR_0": 1}
                  }
                }
              }]}],
              "accessors": [
                {"componentType": 5126, "count": 3, "type": "VEC3"},
                {"componentType": 5121, "normalized": true, "count": 3, "type": "VEC4"}
              ],
              "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": 4}],
              "buffers": [{"byteLength": 4}]
            }
            """;

        var importer = new GltfImporter(new GltfImporterOptions { Id = "draco-color.glb" });
        GltfImportResult result = await importer
            .ImportAsync<RawIntegerDracoPolicy, NoMeshoptCompressionPolicy, PassthroughTranscoderPolicy>(
                Glb(Encoding.UTF8.GetBytes(json), [1, 0, 0, 0]));

        GltfMeshDefinition definition = result.Meshes[0].Definition;
        GltfVertexAttribute color = definition.Attributes.Single(static a => a.Semantic == GltfMeshSemantic.Color0);
        ReadOnlySpan<byte> vertices = definition.Vertices.Span;

        ReadColor(vertices, color, 0).Should().Equal(0, 0, 0, 255);
        ReadColor(vertices, color, 1).Should().Equal(128, 64, 255, 255);
        ReadColor(vertices, color, 2).Should().Equal(255, 255, 255, 255);
    }

    private static byte[] ReadColor(ReadOnlySpan<byte> vertices, GltfVertexAttribute attribute, int vertex) =>
        vertices
            .Slice(attribute.Offset + (vertex * attribute.Stride), attribute.ComponentCount)
            .ToArray();

    /// <summary>
    /// Returns raw unsigned-byte color values, as the native bridge does for a
    /// normalized COLOR_0 accessor.
    /// </summary>
    private readonly struct RawIntegerDracoPolicy : IDracoMeshPolicy
    {
        public static bool IsSupported => true;

        public static GltfDracoDecodeStatus TryDecodeMesh(
            ReadOnlySpan<byte> dracoBytes,
            GltfDracoDecodeLimits limits,
            [NotNullWhen(true)] out GltfDracoDecodedMesh? mesh,
            out long decodedBytes)
        {
            float[] positions = [0, 0, 0, 1, 0, 0, 0, 1, 0];
            float[] colors =
            [
                0, 0, 0, 255,
                128, 64, 255, 255,
                255, 255, 255, 255,
            ];

            mesh = new GltfDracoDecodedMesh
            {
                VertexCount = 3,
                Attributes = ImmutableDictionary.CreateRange(new Dictionary<int, float[]>
                {
                    [0] = positions,
                    [1] = colors,
                }),
                AttributeDataTypes = ImmutableDictionary.CreateRange(new Dictionary<int, GltfDracoAttributeDataType>
                {
                    [0] = GltfDracoAttributeDataType.Float32,
                    [1] = GltfDracoAttributeDataType.UInt8,
                }),
                Indices = [0, 1, 2],
            };

            decodedBytes = ((positions.Length + colors.Length) * sizeof(float)) + (3 * sizeof(uint));
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
