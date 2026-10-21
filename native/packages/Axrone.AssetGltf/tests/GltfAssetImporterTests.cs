namespace Axrone.AssetGltf.Tests;

public sealed class GltfAssetImporterTests
{
    [Fact]
    public void CanImport_True_ForGltfJsonText()
    {
        var importer = new GltfAssetImporter();
        var context = new AssetImportContext(
            new AssetTextSource("{\"asset\":{\"version\":\"2.0\"}}"),
            new AssetKey("test/model"),
            new AssetUri("model.gltf"));

        importer.CanImport(context).Should().BeTrue();
    }

    [Fact]
    public void CanImport_False_ForGarbageText()
    {
        var importer = new GltfAssetImporter();
        var context = new AssetImportContext(
            new AssetTextSource("hello"),
            new AssetKey("test/model"),
            new AssetUri("model.gltf"));

        importer.CanImport(context).Should().BeFalse();
    }

    [Fact]
    public async Task Import_TextGltfWithDataUri_MapsWritesAndDependencies()
    {
        float[] positions = [0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f];
        byte[] posBytes = new byte[positions.Length * 4];
        Buffer.BlockCopy(positions, 0, posBytes, 0, posBytes.Length);
        byte[] idxBytes = [0, 0, 1, 0, 2, 0];
        byte[] bin = new byte[posBytes.Length + idxBytes.Length];
        Buffer.BlockCopy(posBytes, 0, bin, 0, posBytes.Length);
        Buffer.BlockCopy(idxBytes, 0, bin, posBytes.Length, idxBytes.Length);
        string dataUri = "data:application/octet-stream;base64," + Convert.ToBase64String(bin);

        string json = $$"""
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
              "buffers": [{"byteLength": 42, "uri": "{{dataUri}}"}]
            }
            """;

        var importer = new GltfAssetImporter();
        var context = new AssetImportContext(
            new AssetTextSource(json),
            new AssetKey("test/model"),
            new AssetUri("model.gltf"));

        AssetImportResult result = await importer.ImportAsync(context);

        result.Primary.Kind.Should().Be("gltf.document");
        result.Primary.Key.Value.Should().Be("test/model#document");
        result.Additional.Should().NotBeNull();
        result.Additional!.Should().HaveCount(2);

        AssetWriteInput<object> mesh = result.Additional.Should().ContainSingle(w => w.Kind == "gltf.mesh").Subject;
        mesh.Key.Value.Should().Be("test/model#mesh/0/primitive/0");

        AssetWriteInput<object> prefab = result.Additional.Should().ContainSingle(w => w.Kind == "gltf.prefab").Subject;
        prefab.Key.Value.Should().Be("test/model#scene/0/prefab");
        prefab.Dependencies.Should().NotBeNull();
        prefab.Dependencies!.Should().ContainSingle().Which.Should().BeOfType<AssetDependencyInput.KeyDependency>().Which.Key.Value.Should().Be("test/model#mesh/0/primitive/0");

        result.Primary.Dependencies.Should().NotBeNull();
        result.Primary.Dependencies!.Should().HaveCount(2);
        result.Diagnostics.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Import_UnsupportedSourceKind_Throws()
    {
        var importer = new GltfAssetImporter();
        var context = new AssetImportContext(
            new AssetJsonSource(new { }),
            new AssetKey("test/model"),
            new AssetUri("model.gltf"));

        Func<Task> act = () => importer.ImportAsync(context).AsTask();

        (await act.Should().ThrowAsync<GltfException>()).And.Code.Should().Be("gltf.source.unsupported");
    }
}
