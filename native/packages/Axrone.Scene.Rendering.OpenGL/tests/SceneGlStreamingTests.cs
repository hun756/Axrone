namespace Axrone.Scene.Rendering.OpenGL.Tests;

using Axrone.Scene.Assets;
using Axrone.Scene.Rendering.OpenGL;
using FluentAssertions;
using Xunit;

public sealed class SceneGlStreamingTests
{
    [Fact]
    public void AssetCache_SyncsMeshesIncrementally()
    {
        using var fixture = new SceneGlFixture();
        using var cache = new SceneGlAssetCache(fixture.Context, fixture.Assets);

        SceneAssetId<MeshAssetTag> secondId = fixture.Assets.CreateId<MeshAssetTag>("test/mesh2");
        var second = new SceneMeshDefinition(
            secondId,
            new byte[36],
            [new SceneVertexAttribute(SceneMeshSemantic.Position, 3, 0, 12)],
            indices: new byte[6]);
        fixture.Assets.Meshes.Register(second);

        SceneGlUploadStats first = cache.SyncMeshes();

        first.Meshes.Should().Be(2);
        cache.MeshCount.Should().Be(2);
        cache.TryGetMesh(secondId, out _).Should().BeTrue();

        SceneGlUploadStats unchanged = cache.SyncMeshes();
        unchanged.Meshes.Should().Be(0);
        cache.MeshCount.Should().Be(2);

        fixture.Assets.Meshes.Remove(secondId, out _);
        SceneGlUploadStats removed = cache.SyncMeshes();

        removed.Meshes.Should().Be(0);
        cache.MeshCount.Should().Be(1);
        cache.TryGetMesh(secondId, out _).Should().BeFalse();
    }

    [Fact]
    public void AssetCache_ReuploadsAReplacedMesh()
    {
        using var fixture = new SceneGlFixture();
        using var cache = new SceneGlAssetCache(fixture.Context, fixture.Assets);
        cache.SyncMeshes();

        SceneAssetId<MeshAssetTag> id = fixture.Assets.CreateId<MeshAssetTag>("test/replaced");
        var first = new SceneMeshDefinition(
            id,
            new byte[36],
            [new SceneVertexAttribute(SceneMeshSemantic.Position, 3, 0, 12)],
            indices: new byte[6]);
        fixture.Assets.Meshes.Register(first);
        cache.SyncMeshes();
        cache.TryGetMesh(id, out var firstMesh).Should().BeTrue();

        var second = new SceneMeshDefinition(
            id,
            new byte[72],
            [new SceneVertexAttribute(SceneMeshSemantic.Position, 3, 0, 12)],
            vertexCount: 6,
            indices: new byte[6],
            bounds: null);
        fixture.Assets.Meshes.Register(second);

        SceneGlUploadStats replaced = cache.SyncMeshes();

        replaced.Meshes.Should().Be(1);
        cache.TryGetMesh(id, out var secondMesh).Should().BeTrue();
        secondMesh.Should().NotBeSameAs(firstMesh);
    }
}
