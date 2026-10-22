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
    public async Task Import_ExternalBuffer_UsesInjectedResolver()
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
              "buffers": [{"byteLength": 42, "uri": "model.bin"}]
            }
            """;

        GltfResourceRequest? observedRequest = null;
        var importer = new GltfAssetImporter((request, _) =>
        {
            observedRequest = request;
            return ValueTask.FromResult<GltfResolvedResource?>(
                new GltfResolvedResource(bin, "application/octet-stream"));
        });
        var context = new AssetImportContext(
            new AssetTextSource(json),
            new AssetKey("test/external"),
            new AssetUri("model.gltf"));

        AssetImportResult result = await importer.ImportAsync(context);

        observedRequest.Should().NotBeNull();
        observedRequest!.Value.Kind.Should().Be("buffer");
        result.Additional.Should().Contain(write => write.Kind == "gltf.mesh");
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

    [Fact]
    public async Task Import_GlbBytes_ReturnsDocumentAndPrefab()
    {
        byte[] glb = BuildTriangleGlb();
        var importer = new GltfAssetImporter();
        var context = new AssetImportContext(
            new AssetBytesSource(glb),
            new AssetKey("test/glb"),
            new AssetUri("model.glb"));

        importer.CanImport(context).Should().BeTrue();

        AssetImportResult result = await importer.ImportAsync(context);

        result.Primary.Kind.Should().Be("gltf.document");
        result.Additional.Should().NotBeNull();
        result.Additional!.Should().Contain(write => write.Kind == "gltf.mesh");
        result.Additional.Should().Contain(write => write.Kind == "gltf.prefab");
        result.Diagnostics.Should().BeNullOrEmpty();
    }

    [Fact]
    public void CanImport_False_ForNonGltfBytes()
    {
        var importer = new GltfAssetImporter();
        var context = new AssetImportContext(
            new AssetBytesSource([1, 2, 3, 4]),
            new AssetKey("test/binary"),
            new AssetUri("model.bin"));

        importer.CanImport(context).Should().BeFalse();
    }

    [Fact]
    public async Task Import_EmptyGlb_ThrowsCodedError()
    {
        var importer = new GltfAssetImporter();
        var context = new AssetImportContext(
            new AssetBytesSource([]),
            new AssetKey("test/empty"),
            new AssetUri("empty.glb"));

        Func<Task> act = () => importer.ImportAsync(context).AsTask();

        await act.Should().ThrowAsync<GltfException>();
    }

    [Fact]
    public async Task Import_RichGltf_EmitsTextureMaterialAndAnimationWrites()
    {
        const string json = """
            {
              "asset": {"version": "2.0"},
              "scene": 0,
              "scenes": [{"nodes": [0]}],
              "nodes": [{"name": "N", "mesh": 0}],
              "meshes": [{"primitives": [{"attributes": {"POSITION": 0}, "indices": 1, "material": 0}]}],
              "materials": [{"pbrMetallicRoughness": {"baseColorTexture": {"index": 0}}}],
              "textures": [{"source": 0, "sampler": 0}],
              "images": [{"uri": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="}],
              "samplers": [{}],
              "accessors": [
                {"bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3"},
                {"bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR"},
                {"bufferView": 2, "componentType": 5126, "count": 2, "type": "SCALAR"},
                {"bufferView": 3, "componentType": 5126, "count": 2, "type": "VEC3"}
              ],
              "bufferViews": [
                {"buffer": 0, "byteOffset": 0, "byteLength": 36},
                {"buffer": 0, "byteOffset": 36, "byteLength": 6},
                {"buffer": 0, "byteOffset": 44, "byteLength": 8},
                {"buffer": 0, "byteOffset": 52, "byteLength": 24}
              ],
              "buffers": [{"byteLength": 76}],
              "animations": [{
                "samplers": [{"input": 2, "output": 3, "interpolation": "LINEAR"}],
                "channels": [{"sampler": 0, "target": {"node": 0, "path": "translation"}}]
              }]
            }
            """;

        byte[] bin = new byte[76];
        float[] positions = [0, 0, 0, 1, 0, 0, 0, 1, 0];
        Buffer.BlockCopy(positions, 0, bin, 0, 36);
        float[] translations = [0, 0, 0, 1, 0, 0];
        Buffer.BlockCopy(translations, 0, bin, 52, 24);
        BinaryPrimitives.WriteSingleLittleEndian(bin.AsSpan(44, 4), 0f);
        BinaryPrimitives.WriteSingleLittleEndian(bin.AsSpan(48, 4), 1f);

        var importer = new GltfAssetImporter();
        var context = new AssetImportContext(
            new AssetBytesSource(Glb(System.Text.Encoding.UTF8.GetBytes(json), bin)),
            new AssetKey("test/rich"),
            new AssetUri("rich.glb"));

        AssetImportResult result = await importer.ImportAsync(context);

        result.Additional.Should().NotBeNull();
        var kinds = result.Additional!.Select(static write => write.Kind).ToHashSet(StringComparer.Ordinal);
        kinds.Should().Contain("gltf.mesh");
        kinds.Should().Contain("gltf.prefab");
        kinds.Should().Contain("gltf.material");
        kinds.Should().Contain("gltf.texture");
        kinds.Should().Contain("gltf.animation");
        result.Diagnostics.Should().BeNullOrEmpty();
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
