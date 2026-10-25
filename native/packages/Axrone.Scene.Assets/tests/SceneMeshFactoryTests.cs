namespace Axrone.Scene.Assets.Tests;

using System.Runtime.InteropServices;
using Axrone.Geometry;
using Axrone.Numeric;
using Axrone.Scene;
using Axrone.Scene.Assets;
using FluentAssertions;
using Xunit;

public sealed class SceneMeshFactoryTests
{
    [Fact]
    public void CreatePlane_BuildsInterleavedQuad()
    {
        SceneMeshDefinition mesh = SceneMeshFactory.CreatePlane(
            SceneAssetId<MeshAssetTag>.FromName("mesh/plane"));

        mesh.VertexCount.Should().Be(4);
        mesh.IndexCount.Should().Be(6);
        mesh.IndexFormat.Should().Be(SceneIndexFormat.UInt16);
        mesh.VertexStride.Should().Be(VertexP3N3T2.ByteStride);
        mesh.Attributes.Should().HaveCount(3);
        mesh.Topology.Should().Be(SceneMeshTopology.Triangles);
        mesh.Bounds!.Value.Center.Should().Be(new Vec3(0.0f, 0.0f, 0.0f));
        mesh.Bounds.Value.Radius.Should().BeApproximately(MathF.Sqrt(0.5f), 1e-5f);
    }

    [Fact]
    public void CreateBox_ProducesSixFaces()
    {
        SceneMeshDefinition mesh = SceneMeshFactory.CreateBox(
            SceneAssetId<MeshAssetTag>.FromName("mesh/box"));

        mesh.VertexCount.Should().Be(24);
        mesh.IndexCount.Should().Be(36);
        mesh.Bounds!.Value.Center.Should().Be(new Vec3(0.0f, 0.0f, 0.0f));
        mesh.Bounds.Value.Radius.Should().BeApproximately(MathF.Sqrt(0.75f), 1e-5f);
    }

    [Fact]
    public void CreateSphere_UsesCompactIndices()
    {
        SceneMeshDefinition mesh = SceneMeshFactory.CreateSphere(
            SceneAssetId<MeshAssetTag>.FromName("mesh/sphere"),
            radius: 0.5f,
            segments: 24);

        mesh.VertexCount.Should().Be(325);
        mesh.IndexFormat.Should().Be(SceneIndexFormat.UInt16);
        mesh.Bounds!.Value.Radius.Should().BeApproximately(0.5f, 1e-5f);
    }

    [Fact]
    public void CreateCylinder_ProducesClosedBody()
    {
        SceneMeshDefinition mesh = SceneMeshFactory.CreateCylinder(
            SceneAssetId<MeshAssetTag>.FromName("mesh/cylinder"),
            radius: 1.0f,
            height: 2.0f,
            radialSegments: 8);

        mesh.VertexCount.Should().BeGreaterThan(16);
        mesh.IndexCount.Should().BeGreaterThan(16);
        mesh.Bounds!.Value.Center.Should().Be(new Vec3(0.0f, 0.0f, 0.0f));
        mesh.Bounds.Value.Radius.Should().BeApproximately(MathF.Sqrt(1.0f + 1.0f), 1e-4f);
    }

    [Fact]
    public void CreateQuad_XzOrientationFacesUp()
    {
        SceneMeshDefinition mesh = SceneMeshFactory.CreateQuad(
            SceneAssetId<MeshAssetTag>.FromName("mesh/quad-xz"),
            orientation: SceneQuadOrientation.Xz);

        ReadOnlySpan<VertexP3N3T2> vertices =
            MemoryMarshal.Cast<byte, VertexP3N3T2>(mesh.Vertices.Span);

        vertices.Length.Should().Be(4);
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].Position.Y.Should().Be(0.0f);
            vertices[i].Normal.Y.Should().Be(1.0f);
        }
    }

    [Fact]
    public void CreateQuad_YzOrientationFacesRight()
    {
        SceneMeshDefinition mesh = SceneMeshFactory.CreateQuad(
            SceneAssetId<MeshAssetTag>.FromName("mesh/quad-yz"),
            orientation: SceneQuadOrientation.Yz);

        ReadOnlySpan<VertexP3N3T2> vertices =
            MemoryMarshal.Cast<byte, VertexP3N3T2>(mesh.Vertices.Span);

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].Position.X.Should().Be(0.0f);
            vertices[i].Normal.X.Should().Be(1.0f);
        }
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(-1.0f)]
    [InlineData(float.NaN)]
    public void InvalidDimensions_Throw(float width)
    {
        Action act = () => SceneMeshFactory.CreatePlane(
            SceneAssetId<MeshAssetTag>.FromName("mesh/invalid"),
            width: width);

        act.Should().Throw<SceneAssetException>()
            .Where(exception => exception.Code == SceneAssetErrorCodes.Invalid);
    }
}
