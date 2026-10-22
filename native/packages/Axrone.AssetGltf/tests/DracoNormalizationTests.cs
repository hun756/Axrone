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
    private const string ColorGlbJson = """
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

    [Fact]
    public async Task Import_RawIntegerColorFromDraco_PreservesValues()
    {
        GltfMeshDefinition definition = await ImportColorsAsync<RawIntegerDracoPolicy>();

        ReadColor(definition, 0).Should().Equal(0, 0, 0, 255);
        ReadColor(definition, 1).Should().Equal(128, 64, 255, 255);
        ReadColor(definition, 2).Should().Equal(255, 255, 255, 255);
    }

    [Fact]
    public async Task Import_PreNormalizedFloatColorFromDraco_UsesAccessorUnits()
    {
        GltfMeshDefinition definition = await ImportColorsAsync<PreNormalizedDracoPolicy>();

        ReadColor(definition, 0).Should().Equal(0, 0, 0, 255);
        ReadColor(definition, 1).Should().Equal(128, 64, 255, 255);
        ReadColor(definition, 2).Should().Equal(255, 255, 255, 255);
    }

    [Fact]
    public async Task Import_NormalizedColorWithUnknownStorageType_Throws()
    {
        var importer = new GltfImporter(new GltfImporterOptions { Id = "draco-unknown.glb" });
        Func<Task> act = () => importer
            .ImportAsync<UnknownStorageDracoPolicy, NoMeshoptCompressionPolicy, PassthroughTranscoderPolicy>(
                Glb(Encoding.UTF8.GetBytes(ColorGlbJson), [1, 0, 0, 0]))
            .AsTask();

        await act.Should().ThrowAsync<GltfAccessorError>();
    }

    private static async Task<GltfMeshDefinition> ImportColorsAsync<TDracoPolicy>()
        where TDracoPolicy : struct, IDracoMeshPolicy
    {
        var importer = new GltfImporter(new GltfImporterOptions { Id = "draco-color.glb" });
        GltfImportResult result = await importer
            .ImportAsync<TDracoPolicy, NoMeshoptCompressionPolicy, PassthroughTranscoderPolicy>(
                Glb(Encoding.UTF8.GetBytes(ColorGlbJson), [1, 0, 0, 0]));
        return result.Meshes[0].Definition;
    }

    private static byte[] ReadColor(GltfMeshDefinition definition, int vertex)
    {
        GltfVertexAttribute color = definition.Attributes.Single(static a => a.Semantic == GltfMeshSemantic.Color0);
        return definition.Vertices.Span
            .Slice(color.Offset + (vertex * color.Stride), color.ComponentCount)
            .ToArray();
    }

    private static GltfDracoDecodedMesh CreateMesh(
        float[] colors,
        GltfDracoAttributeDataType colorDataType) =>
        new()
        {
            VertexCount = 3,
            Attributes = ImmutableDictionary.CreateRange(new Dictionary<int, float[]>
            {
                [0] = [0, 0, 0, 1, 0, 0, 0, 1, 0],
                [1] = colors,
            }),
            AttributeDataTypes = ImmutableDictionary.CreateRange(new Dictionary<int, GltfDracoAttributeDataType>
            {
                [0] = GltfDracoAttributeDataType.Float32,
                [1] = colorDataType,
            }),
            Indices = [0, 1, 2],
        };

    private static GltfDracoDecodeStatus Succeed(
        GltfDracoDecodedMesh mesh,
        [NotNullWhen(true)] out GltfDracoDecodedMesh? result,
        out long decodedBytes)
    {
        result = mesh;
        decodedBytes = mesh.Indices.Length * sizeof(uint);
        foreach (float[] values in mesh.Attributes.Values)
        {
            decodedBytes += values.Length * sizeof(float);
        }

        return GltfDracoDecodeStatus.Success;
    }

    /// <summary>Raw unsigned-byte colors, as Draco stores normalized COLOR_0.</summary>
    private readonly struct RawIntegerDracoPolicy : IDracoMeshPolicy
    {
        public static bool IsSupported => true;

        public static GltfDracoDecodeStatus TryDecodeMesh(
            ReadOnlySpan<byte> dracoBytes,
            GltfDracoDecodeLimits limits,
            [NotNullWhen(true)] out GltfDracoDecodedMesh? mesh,
            out long decodedBytes) =>
            Succeed(
                CreateMesh(
                    [0, 0, 0, 255, 128, 64, 255, 255, 255, 255, 255, 255],
                    GltfDracoAttributeDataType.UInt8),
                out mesh,
                out decodedBytes);
    }

    /// <summary>Float colors already scaled into [0, 1] by the encoder.</summary>
    private readonly struct PreNormalizedDracoPolicy : IDracoMeshPolicy
    {
        public static bool IsSupported => true;

        public static GltfDracoDecodeStatus TryDecodeMesh(
            ReadOnlySpan<byte> dracoBytes,
            GltfDracoDecodeLimits limits,
            [NotNullWhen(true)] out GltfDracoDecodedMesh? mesh,
            out long decodedBytes) =>
            Succeed(
                CreateMesh(
                    [0, 0, 0, 1, 0.5f, 0.25f, 1, 1, 1, 1, 1, 1],
                    GltfDracoAttributeDataType.Float32),
                out mesh,
                out decodedBytes);
    }

    /// <summary>Storage type is not reported for the normalized attribute.</summary>
    private readonly struct UnknownStorageDracoPolicy : IDracoMeshPolicy
    {
        public static bool IsSupported => true;

        public static GltfDracoDecodeStatus TryDecodeMesh(
            ReadOnlySpan<byte> dracoBytes,
            GltfDracoDecodeLimits limits,
            [NotNullWhen(true)] out GltfDracoDecodedMesh? mesh,
            out long decodedBytes) =>
            Succeed(
                CreateMesh(
                    [0, 0, 0, 1, 0.5f, 0.25f, 1, 1, 1, 1, 1, 1],
                    GltfDracoAttributeDataType.Unknown),
                out mesh,
                out decodedBytes);
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
