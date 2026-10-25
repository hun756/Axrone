namespace Axrone.Scene.Assets.Tests;

using System.Buffers.Binary;
using Axrone.Geometry;
using Axrone.Numeric;
using Axrone.Scene;
using Axrone.Scene.Assets;
using FluentAssertions;
using Xunit;

public sealed class SceneAssetUtilitiesTests
{
    [Fact]
    public void MeshBounds_UsesDeclaredBounds()
    {
        var declared = new BoundingSphere3(new Vec3(1.0f, 2.0f, 3.0f), 4.0f);
        SceneMeshDefinition mesh = CreateMesh(
            Interleaved((0.0f, 0.0f, 0.0f), (10.0f, 0.0f, 0.0f)),
            new SceneVertexAttribute(SceneMeshSemantic.Position, 3, 0, 12),
            bounds: declared);

        SceneMeshBounds.Resolve(mesh).Should().Be(declared);
    }

    [Fact]
    public void MeshBounds_ComputesFromPositionAttribute()
    {
        SceneMeshDefinition mesh = CreateMesh(
            Interleaved((0.0f, 0.0f, 0.0f), (2.0f, 0.0f, 0.0f), (0.0f, 2.0f, 0.0f)),
            new SceneVertexAttribute(SceneMeshSemantic.Position, 3, 0, 12));

        BoundingSphere3? bounds = SceneMeshBounds.Resolve(mesh);

        bounds.Should().NotBeNull();
        bounds!.Value.Center.Should().Be(new Vec3(1.0f, 1.0f, 0.0f));
        bounds.Value.Radius.Should().BeApproximately(MathF.Sqrt(2.0f), 1e-5f);
    }

    [Fact]
    public void MeshBounds_HandlesInterleavedOffsets()
    {
        SceneMeshDefinition mesh = CreateMesh(
            InterleavedWithOffset((0.0f, 0.0f, 0.0f), (4.0f, 6.0f, 0.0f)),
            new SceneVertexAttribute(SceneMeshSemantic.Position, 3, offset: 4, stride: 16));

        SceneMeshBounds.Resolve(mesh)!.Value.Center.Should().Be(new Vec3(2.0f, 3.0f, 0.0f));
    }

    [Fact]
    public void MeshBounds_ReturnsNullWithoutPositionAttribute()
    {
        SceneMeshDefinition mesh = CreateMesh(
            new byte[24],
            new SceneVertexAttribute(SceneMeshSemantic.Normal, 3, 0, 12));

        SceneMeshBounds.Resolve(mesh).Should().BeNull();
        SceneMeshBounds.TryCompute(mesh, out _).Should().BeFalse();
    }

    [Fact]
    public void Keywords_ResolveMasksToNames()
    {
        SceneMaterialFeatures features =
            SceneMaterialFeatures.AlbedoMap |
            SceneMaterialFeatures.AlphaTest |
            SceneMaterialFeatures.Sheen;

        string[] keywords = SceneMaterialKeywordMap.Resolve(features);

        keywords.Should().Equal("ALBEDO_MAP", "ALPHA_TEST", "SHEEN");
    }

    [Fact]
    public void Keywords_CollectAppendsInDeclarationOrder()
    {
        var destination = new List<string> { "EXISTING" };

        SceneMaterialKeywordMap.Collect(
            SceneMaterialFeatures.NormalMap | SceneMaterialFeatures.TwoSided,
            destination);

        destination.Should().Equal("EXISTING", "NORMAL_MAPPING", "TWO_SIDED");
    }

    [Theory]
    [InlineData("VERTEX_COLOR", SceneMaterialFeatures.VertexColor)]
    [InlineData("METALLIC_ROUGHNESS_MAP", SceneMaterialFeatures.MetallicRoughnessMap)]
    [InlineData("TRANSMISSION", SceneMaterialFeatures.Transmission)]
    public void Keywords_TryGetFeatureRoundTrips(string keyword, SceneMaterialFeatures expected)
    {
        SceneMaterialKeywordMap.TryGetFeature(keyword, out SceneMaterialFeatures feature).Should().BeTrue();
        feature.Should().Be(expected);
    }

    [Fact]
    public void Keywords_UnknownNameIsRejected()
    {
        SceneMaterialKeywordMap.TryGetFeature("NOT_A_KEYWORD", out SceneMaterialFeatures feature)
            .Should().BeFalse();
        feature.Should().Be(SceneMaterialFeatures.None);
    }

    private static SceneMeshDefinition CreateMesh(
        byte[] vertices,
        SceneVertexAttribute attribute,
        BoundingSphere3? bounds = null) =>
        new(
            SceneAssetId<MeshAssetTag>.FromName("mesh/test"),
            vertices,
            [attribute],
            bounds: bounds);

    private static byte[] Interleaved(params (float X, float Y, float Z)[] positions)
    {
        byte[] data = new byte[positions.Length * 12];
        for (int i = 0; i < positions.Length; i++)
        {
            WritePosition(data.AsSpan(i * 12), positions[i]);
        }

        return data;
    }

    private static byte[] InterleavedWithOffset(params (float X, float Y, float Z)[] positions)
    {
        byte[] data = new byte[positions.Length * 16];
        for (int i = 0; i < positions.Length; i++)
        {
            WritePosition(data.AsSpan((i * 16) + 4), positions[i]);
        }

        return data;
    }

    private static void WritePosition(Span<byte> destination, (float X, float Y, float Z) position)
    {
        BinaryPrimitives.WriteSingleLittleEndian(destination[..4], position.X);
        BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(4, 4), position.Y);
        BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(8, 4), position.Z);
    }
}
