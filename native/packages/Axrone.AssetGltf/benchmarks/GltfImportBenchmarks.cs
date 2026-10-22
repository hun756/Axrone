using System.Buffers.Binary;
using System.Text;
using Axrone.AssetGltf;
using BenchmarkDotNet.Attributes;

namespace Axrone.AssetGltf.Benchmarks;

/// <summary>Import throughput and allocation benchmarks for the canonical glTF pipeline.</summary>
[MemoryDiagnoser]
public class GltfImportBenchmarks
{
    private byte[] _glb = [];

    [GlobalSetup]
    public void Setup()
    {
        _glb = BuildGlb(50_000, 75_000);
    }

    [Benchmark]
    public async ValueTask<GltfImportResult> ImportMesh()
    {
        return await new GltfImporter().ImportAsync(_glb).ConfigureAwait(false);
    }

    private static byte[] BuildGlb(int vertexCount, int triangleCount)
    {
        int positionsLength = vertexCount * 12;
        int uvLength = vertexCount * 8;
        int indicesLength = triangleCount * 3 * 2;
        int indicesOffset = positionsLength + uvLength;
        byte[] bin = new byte[indicesOffset + indicesLength];
        for (int i = 0; i < vertexCount; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bin.AsSpan(i * 12, 4), i * 0.01f);
        }

        for (int i = 0; i < triangleCount * 3; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bin.AsSpan(indicesOffset + i * 2, 2), (ushort)(i % vertexCount));
        }

        string json = $$"""
        {
          "asset": {"version": "2.0"},
          "scenes": [{"nodes": [0]}],
          "nodes": [{"name": "Benchmark", "mesh": 0}],
          "meshes": [{"primitives": [{"attributes": {"POSITION": 0, "TEXCOORD_0": 1}, "indices": 2}]}],
          "accessors": [
            {"bufferView": 0, "componentType": 5126, "count": {{vertexCount}}, "type": "VEC3"},
            {"bufferView": 1, "componentType": 5126, "count": {{vertexCount}}, "type": "VEC2"},
            {"bufferView": 2, "componentType": 5123, "count": {{triangleCount * 3}}, "type": "SCALAR"}
          ],
          "bufferViews": [
            {"buffer": 0, "byteOffset": 0, "byteLength": {{positionsLength}}},
            {"buffer": 0, "byteOffset": {{positionsLength}}, "byteLength": {{uvLength}}},
            {"buffer": 0, "byteOffset": {{indicesOffset}}, "byteLength": {{indicesLength}}}
          ],
          "buffers": [{"byteLength": {{bin.Length}}}]
        }
        """;

        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
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

        int binOffset = 20 + jsonBytes.Length + jsonPad;
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(binOffset, 4), (uint)(bin.Length + binPad));
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(binOffset + 4, 4), 0x004E4942);
        bin.CopyTo(glb, binOffset + 8);
        return glb;
    }
}
