namespace Axrone.AssetGltf.Tests;

public sealed class GltfImportTests
{
    [Fact]
    public async Task Import_SingleTriangleGlb_ReturnsMeshAndPrefab()
    {
        float[] positions = [0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f];
        byte[] posBytes = new byte[positions.Length * 4];
        Buffer.BlockCopy(positions, 0, posBytes, 0, posBytes.Length);
        byte[] idxBytes = [0, 0, 1, 0, 2, 0];

        byte[] bin = new byte[posBytes.Length + idxBytes.Length];
        Buffer.BlockCopy(posBytes, 0, bin, 0, posBytes.Length);
        Buffer.BlockCopy(idxBytes, 0, bin, posBytes.Length, idxBytes.Length);

        const string json = """
            {
              "asset": {"version": "2.0"},
              "scene": 0,
              "scenes": [{"name": "S", "nodes": [0]}],
              "nodes": [{"name": "N", "mesh": 0}],
              "meshes": [{"name": "M", "primitives": [{"attributes": {"POSITION": 0}, "indices": 1, "mode": 4}]}],
              "accessors": [
                {"bufferView": 0, "byteOffset": 0, "componentType": 5126, "count": 3, "type": "VEC3"},
                {"bufferView": 1, "byteOffset": 0, "componentType": 5123, "count": 3, "type": "SCALAR"}
              ],
              "bufferViews": [
                {"buffer": 0, "byteOffset": 0, "byteLength": 36},
                {"buffer": 0, "byteOffset": 36, "byteLength": 6}
              ],
              "buffers": [{"byteLength": 42}]
            }
            """;

        byte[] glb = BuildGlb(System.Text.Encoding.UTF8.GetBytes(json), bin);

        var importer = new GltfImporter(new GltfImporterOptions { Id = "test.glb" });
        GltfImportResult result = await importer.ImportAsync(glb);

        result.Diagnostics.Should().BeEmpty();
        result.Meshes.Should().HaveCount(1);
        result.Meshes[0].Definition.VertexCount.Should().Be(3);
        result.Meshes[0].Definition.IndexCount.Should().Be(3);
        result.Meshes[0].Definition.Index16Bit.Should().BeTrue();
        result.Meshes[0].Definition.Topology.Should().Be(GltfMeshTopology.Triangles);
        result.Meshes[0].Bounds.Should().NotBeNull();
        result.Prefabs.Should().HaveCount(1);
        result.Prefabs[0].RootNodeIds.Should().HaveCount(1);
        result.Prefabs[0].Definition.Actors.Should().HaveCount(1);
        result.Document.Stats.MeshCount.Should().Be(1);
        result.Document.Stats.PrimitiveCount.Should().Be(1);
        result.Document.Stats.SceneCount.Should().Be(1);
        result.Document.Stats.NodeCount.Should().Be(1);
        result.Document.Format.Should().Be("glb");
    }

    [Fact]
    public async Task Import_MultiPrimitiveMesh_UsesCompiledPrimitiveKeys()
    {
        float[] positions = [0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f];
        byte[] posBytes = new byte[positions.Length * 4];
        Buffer.BlockCopy(positions, 0, posBytes, 0, posBytes.Length);
        byte[] idxBytes = [0, 0, 1, 0, 2, 0];

        byte[] bin = new byte[posBytes.Length + idxBytes.Length];
        Buffer.BlockCopy(posBytes, 0, bin, 0, posBytes.Length);
        Buffer.BlockCopy(idxBytes, 0, bin, posBytes.Length, idxBytes.Length);

        const string json = """
            {
              "asset": {"version": "2.0"},
              "scene": 0,
              "scenes": [{"name": "S", "nodes": [0]}],
              "nodes": [{"name": "N", "mesh": 0}],
              "meshes": [{"name": "M", "primitives": [
                {"attributes": {"POSITION": 0}, "indices": 1, "mode": 4},
                {"attributes": {"POSITION": 0}, "indices": 1, "mode": 4}
              ]}],
              "accessors": [
                {"bufferView": 0, "byteOffset": 0, "componentType": 5126, "count": 3, "type": "VEC3"},
                {"bufferView": 1, "byteOffset": 0, "componentType": 5123, "count": 3, "type": "SCALAR"}
              ],
              "bufferViews": [
                {"buffer": 0, "byteOffset": 0, "byteLength": 36},
                {"buffer": 0, "byteOffset": 36, "byteLength": 6}
              ],
              "buffers": [{"byteLength": 42}]
            }
            """;

        byte[] glb = BuildGlb(System.Text.Encoding.UTF8.GetBytes(json), bin);
        var importer = new GltfImporter(new GltfImporterOptions { Id = "multi.glb" });
        GltfImportResult result = await importer.ImportAsync(glb);

        string[] expectedKeys = ["gltf/mesh/0:0", "gltf/mesh/0:1"];
        result.Meshes.Select(static mesh => mesh.Id).Should().Equal(expectedKeys);
        result.Prefabs.Should().ContainSingle();
        result.Prefabs[0].MeshKeys.Should().Equal(expectedKeys);

        GltfPrefabAsset prefab = result.Prefabs[0];
        prefab.Definition.Actors.Should().HaveCount(3);

        string[] rendererMeshIds = prefab.Definition.Actors
            .SelectMany(static actor => actor.Components)
            .OfType<GltfMeshRendererComponent>()
            .Select(static component => component.MeshId)
            .ToArray();

        rendererMeshIds.Should().Equal(expectedKeys);
        rendererMeshIds.Should().OnlyContain(id => result.Meshes.Any(mesh => mesh.Id == id));
    }

    [Fact]
    public async Task Import_SceneLessDocument_ProducesEmptySceneGraph()
    {
        const string json = """
            {
              "asset": {"version": "2.0"},
              "materials": [{"name": "LibraryMaterial"}]
            }
            """;

        byte[] glb = BuildGlb(System.Text.Encoding.UTF8.GetBytes(json), []);
        var importer = new GltfImporter(new GltfImporterOptions { Id = "library.glb" });
        GltfImportResult result = await importer.ImportAsync(glb);

        result.Document.Scenes.Should().BeEmpty();
        result.Prefabs.Should().BeEmpty();
        result.Document.DefaultScene.Should().Be(-1);
        result.Materials.Should().ContainSingle();
        result.Materials[0].Id.Should().Be("gltf/material/0");
        result.Diagnostics.Should().BeEmpty();
    }

    private static byte[] BuildGlb(byte[] json, byte[] bin)
    {
        int jsonPad = (4 - (json.Length % 4)) % 4;
        int binPad = (4 - (bin.Length % 4)) % 4;
        int total = 12 + 8 + json.Length + jsonPad + 8 + bin.Length + binPad;
        byte[] outBuf = new byte[total];
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(0, 4), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(4, 4), 2u);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(8, 4), (uint)total);
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(12, 4), (uint)(json.Length + jsonPad));
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(16, 4), 0x4E4F534A);
        Buffer.BlockCopy(json, 0, outBuf, 20, json.Length);
        for (int i = 0; i < jsonPad; i++)
        {
            outBuf[20 + json.Length + i] = 0x20;
        }

        int off = 20 + json.Length + jsonPad;
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(off, 4), (uint)(bin.Length + binPad));
        BinaryPrimitives.WriteUInt32LittleEndian(outBuf.AsSpan(off + 4, 4), 0x004E4942);
        Buffer.BlockCopy(bin, 0, outBuf, off + 8, bin.Length);
        return outBuf;
    }
}
