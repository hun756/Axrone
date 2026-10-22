namespace Axrone.AssetGltf.Codecs.Tests;

/// <summary>
/// Coverage for <see cref="NativeDracoMeshPolicy"/>.
/// These tests need the RID-specific <c>axrone_draco</c> native library built
/// by the <c>draco-native</c> CI job; without it they return early so local
/// machines without a C++ toolchain stay green.
/// </summary>
public sealed class NativeDracoTests
{
    private const string RequireNativeVariable = "AXRONE_REQUIRE_NATIVE_DRACO";

    private static string TestData(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", name);

    /// <summary>
    /// Skips on machines without a C++ toolchain, but fails when
    /// <c>AXRONE_REQUIRE_NATIVE_DRACO=1</c> is set (CI) so a missing or
    /// mis-staged native library cannot silently disable this suite.
    /// </summary>
    private static bool NativeDracoAvailable()
    {
        if (NativeDracoMeshPolicy.IsNativeLibraryAvailable)
        {
            return true;
        }

        if (Environment.GetEnvironmentVariable(RequireNativeVariable) == "1")
        {
            throw new InvalidOperationException(
                $"{RequireNativeVariable}=1 but the axrone_draco native library was not found. " +
                "Build native/extern/draco-bridge and stage it under runtimes/<rid>/native.");
        }

        return false;
    }

    [Fact]
    public void NativeDraco_DecodesGridMesh()
    {
        if (!NativeDracoAvailable())
        {
            return;
        }

        byte[] drc = File.ReadAllBytes(TestData("draco-grid-1k.drc"));
        bool decoded = NativeDracoMeshPolicy.TryDecodeMesh(drc, out GltfDracoDecodedMesh? mesh);

        decoded.Should().BeTrue();
        mesh.Should().NotBeNull();
        mesh!.VertexCount.Should().Be(625);
        mesh.Indices.Should().HaveCount(1152 * 3);
        mesh.Attributes.Should().HaveCount(1);
        mesh.Attributes.Values.Sum(static a => a.Length).Should().Be(625 * 3);
    }

    [Fact]
    public void NativeDraco_DecodesBunnyMesh()
    {
        if (!NativeDracoAvailable())
        {
            return;
        }

        byte[] drc = File.ReadAllBytes(TestData("draco-bunny.drc"));
        bool decoded = NativeDracoMeshPolicy.TryDecodeMesh(drc, out GltfDracoDecodedMesh? mesh);

        decoded.Should().BeTrue();
        mesh.Should().NotBeNull();
        mesh!.VertexCount.Should().Be(34834);
        mesh.Indices.Should().HaveCount(69451 * 3);
        mesh.Attributes.Should().HaveCount(2);
    }

    [Fact]
    public async Task NativeDraco_ImportsGlbPrimitive()
    {
        if (!NativeDracoAvailable())
        {
            return;
        }

        byte[] encoded = File.ReadAllBytes(TestData("draco-grid-1k.drc"));
        bool decoded = NativeDracoMeshPolicy.TryDecodeMesh(encoded, out GltfDracoDecodedMesh? probe);
        decoded.Should().BeTrue();
        probe!.Attributes.Should().HaveCount(1);
        int positionId = probe.Attributes.Keys.Single();

        string json = $$"""
        {
          "asset": {"version": "2.0"},
          "extensionsUsed": ["KHR_draco_mesh_compression"],
          "extensionsRequired": ["KHR_draco_mesh_compression"],
          "scene": 0,
          "scenes": [{"nodes": [0]}],
          "nodes": [{"name": "Draco", "mesh": 0}],
          "meshes": [{"primitives": [{
            "attributes": {"POSITION": 0},
            "extensions": {
              "KHR_draco_mesh_compression": {
                "bufferView": 0,
                "attributes": {"POSITION": {{positionId}}}
              }
            }
          }]}],
          "accessors": [{"componentType": 5126, "count": 625, "type": "VEC3"}],
          "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": {{encoded.Length}}}],
          "buffers": [{"byteLength": {{encoded.Length}}}]
        }
        """;

        byte[] glb = Glb(json, encoded);
        var importer = new GltfImporter(new GltfImporterOptions { Id = "draco-native.glb" });
        GltfImportResult result = await importer
            .ImportAsync<NativeDracoMeshPolicy, NoMeshoptCompressionPolicy, PassthroughTranscoderPolicy>(glb);

        result.Meshes[0].Definition.VertexCount.Should().Be(625);
        result.Meshes[0].Definition.IndexCount.Should().Be(1152 * 3);
    }

    [Fact]
    public async Task NativeDraco_RejectsNonTriangleMode()
    {
        if (!NativeDracoAvailable())
        {
            return;
        }

        byte[] encoded = File.ReadAllBytes(TestData("draco-grid-1k.drc"));
        int positionId = NativeDracoMeshPolicy
            .TryDecodeMesh(encoded, out GltfDracoDecodedMesh? probe)
            ? probe!.Attributes.Keys.Single()
            : 0;

        string json = $$"""
        {
          "asset": {"version": "2.0"},
          "extensionsUsed": ["KHR_draco_mesh_compression"],
          "extensionsRequired": ["KHR_draco_mesh_compression"],
          "scene": 0,
          "scenes": [{"nodes": [0]}],
          "nodes": [{"name": "Draco", "mesh": 0}],
          "meshes": [{"primitives": [{
            "attributes": {"POSITION": 0},
            "mode": 1,
            "extensions": {
              "KHR_draco_mesh_compression": {
                "bufferView": 0,
                "attributes": {"POSITION": {{positionId}}}
              }
            }
          }]}],
          "accessors": [{"componentType": 5126, "count": 625, "type": "VEC3"}],
          "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": {{encoded.Length}}}],
          "buffers": [{"byteLength": {{encoded.Length}}}]
        }
        """;

        var importer = new GltfImporter(new GltfImporterOptions { Id = "draco-mode.glb" });
        Func<Task> act = () => importer
            .ImportAsync<NativeDracoMeshPolicy, NoMeshoptCompressionPolicy, PassthroughTranscoderPolicy>(
                Glb(json, encoded))
            .AsTask();

        await act.Should().ThrowAsync<GltfSchemaError>();
    }

    [Fact]
    public async Task NativeDraco_RejectsIndexCountMismatch()
    {
        if (!NativeDracoAvailable())
        {
            return;
        }

        byte[] encoded = File.ReadAllBytes(TestData("draco-grid-1k.drc"));
        int positionId = NativeDracoMeshPolicy
            .TryDecodeMesh(encoded, out GltfDracoDecodedMesh? probe)
            ? probe!.Attributes.Keys.Single()
            : 0;

        string json = $$"""
        {
          "asset": {"version": "2.0"},
          "extensionsUsed": ["KHR_draco_mesh_compression"],
          "extensionsRequired": ["KHR_draco_mesh_compression"],
          "scene": 0,
          "scenes": [{"nodes": [0]}],
          "nodes": [{"name": "Draco", "mesh": 0}],
          "meshes": [{"primitives": [{
            "attributes": {"POSITION": 0},
            "indices": 1,
            "extensions": {
              "KHR_draco_mesh_compression": {
                "bufferView": 0,
                "attributes": {"POSITION": {{positionId}}}
              }
            }
          }]}],
          "accessors": [
            {"componentType": 5126, "count": 625, "type": "VEC3"},
            {"componentType": 5125, "count": 100, "type": "SCALAR"}
          ],
          "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": {{encoded.Length}}}],
          "buffers": [{"byteLength": {{encoded.Length}}}]
        }
        """;

        var importer = new GltfImporter(new GltfImporterOptions { Id = "draco-indices.glb" });
        Func<Task> act = () => importer
            .ImportAsync<NativeDracoMeshPolicy, NoMeshoptCompressionPolicy, PassthroughTranscoderPolicy>(
                Glb(json, encoded))
            .AsTask();

        await act.Should().ThrowAsync<GltfAccessorError>();
    }

    [Fact]
    public void NativeDraco_RejectsInvalidData()
    {
        if (!NativeDracoAvailable())
        {
            return;
        }

        NativeDracoMeshPolicy.TryDecodeMesh([], out GltfDracoDecodedMesh? empty).Should().BeFalse();
        NativeDracoMeshPolicy.TryDecodeMesh([1, 2, 3, 4], out GltfDracoDecodedMesh? garbage).Should().BeFalse();
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
