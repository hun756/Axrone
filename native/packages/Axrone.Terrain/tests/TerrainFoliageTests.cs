using Axrone.Terrain;
using FluentAssertions;
using Xunit;

namespace Axrone.Terrain.Tests;

public sealed class TerrainFoliageTests
{
    private static TerrainFoliageLayer CreateLayer(
        string id = "foliage_1",
        float density = 0.5f,
        float minScale = 0.8f,
        float maxScale = 1.4f,
        float maxSlopeDeg = 45f) => new()
        {
            Id = id,
            Name = "Grass",
            MeshAsset = string.Empty,
            Density = density,
            MinScale = minScale,
            MaxScale = maxScale,
            AlignToNormal = false,
            MaxSlopeDeg = maxSlopeDeg,
        };

    [Fact]
    public void Layers_AcceptValidSet()
    {
        new[] { CreateLayer() }
            .Invoking(layers => TerrainValidation.ValidateFoliageLayers(layers))
            .Should().NotThrow();
    }

    [Fact]
    public void Layers_RejectMoreThanCap()
    {
        var layers = new TerrainFoliageLayer[TerrainLimits.MaxFoliageLayers + 1];
        for (var index = 0; index < layers.Length; index++)
        {
            layers[index] = CreateLayer($"foliage_{index}");
        }

        layers.Invoking(value => TerrainValidation.ValidateFoliageLayers(value))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.ValidationFailed);
    }

    [Theory]
    [InlineData(1.5f, 0.8f, 1.4f, 45f)]
    [InlineData(0.5f, 2f, 1f, 45f)]
    [InlineData(0.5f, 0.8f, 1.4f, 120f)]
    [InlineData(-0.5f, 0.8f, 1.4f, 45f)]
    public void Layers_RejectOutOfRangeValues(
        float density,
        float minScale,
        float maxScale,
        float maxSlopeDeg)
    {
        new[] { CreateLayer(density: density, minScale: minScale, maxScale: maxScale, maxSlopeDeg: maxSlopeDeg) }
            .Invoking(value => TerrainValidation.ValidateFoliageLayers(value))
            .Should().Throw<TerrainException>();
    }

    [Fact]
    public void Layers_RejectEmptyId()
    {
        new[] { CreateLayer(id: string.Empty) }
            .Invoking(value => TerrainValidation.ValidateFoliageLayers(value))
            .Should().Throw<TerrainException>();
    }

    [Fact]
    public void DensityCodec_RoundTripsBuffers()
    {
        const int resolution = 64;
        var density = new byte[resolution * resolution];
        density[0] = 128;
        density[resolution - 1] = 255;

        string payload = TerrainFoliageDensityCodec.Encode(density);
        TerrainFoliageDensityCodec.IsPayload(payload).Should().BeTrue();
        TerrainFoliageDensityCodec.Decode(payload, resolution).Should().Equal(density);
    }

    [Fact]
    public void DensityCodec_RejectsForeignPayloadsAndWrongResolution()
    {
        TerrainFoliageDensityCodec.IsPayload("atsp1:AAAA").Should().BeFalse();

        "nope"
            .Invoking(value => TerrainFoliageDensityCodec.Decode(value, 64))
            .Should().Throw<TerrainException>();

        string payload = TerrainFoliageDensityCodec.Encode(new byte[64 * 64]);
        payload.Invoking(value => TerrainFoliageDensityCodec.Decode(value, 128))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.SplatSizeMismatch);
    }

    [Fact]
    public void DensityCodec_IsPayloadRejectsNonPayloads()
    {
        TerrainFoliageDensityCodec.IsPayload(null).Should().BeFalse();
        TerrainFoliageDensityCodec.IsPayload(string.Empty).Should().BeFalse();
    }

    [Fact]
    public void BatchMesh_ExpandsEveryInstanceWithCardMesh()
    {
        TerrainFoliageSourceMesh card = TerrainFoliageMesh.CreateCardMesh();
        int cardVertexCount = card.VertexCount;

        TerrainFoliageInstance[] instances =
        [
            new TerrainFoliageInstance(0f, 2f, 0f, 0f, 1f),
            new TerrainFoliageInstance(5f, 0f, -3f, MathF.PI / 2f, 2f),
        ];

        TerrainMeshData mesh = TerrainFoliageMesh.BuildBatch(instances);

        mesh.VertexCount.Should().Be(cardVertexCount * 2);
        mesh.TriangleCount.Should().Be((card.Indices.Length / 3) * 2);
        mesh.Positions.Should().HaveCount(mesh.VertexCount * 3);
        mesh.Uvs.Should().HaveCount(mesh.VertexCount * 2);

        mesh.Positions[1].Should().BeApproximately(2f, 1e-5f);
        float secondBaseY = mesh.Positions[((cardVertexCount + 2) * 3) + 1];
        secondBaseY.Should().BeApproximately(2f, 1e-5f);
        mesh.Indices[card.Indices.Length].Should().Be(card.Indices[0] + (uint)cardVertexCount);
    }

    [Fact]
    public void BatchMesh_ProducesEmptyMeshWithoutInstances()
    {
        TerrainMeshData mesh = TerrainFoliageMesh.BuildBatch([]);

        mesh.VertexCount.Should().Be(0);
        mesh.TriangleCount.Should().Be(0);
        mesh.Indices.Should().BeEmpty();
    }

    [Fact]
    public void BatchMesh_RejectsInconsistentSourceMesh()
    {
        FluentActions.Invoking(() => new TerrainFoliageSourceMesh(
                new float[4],
                new float[4],
                new float[2],
                new uint[3]))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.ValidationFailed);
    }

    [Fact]
    public void CardMesh_HasEightVerticesAndTwelveIndices()
    {
        TerrainFoliageSourceMesh card = TerrainFoliageMesh.CreateCardMesh();

        card.VertexCount.Should().Be(8);
        card.Indices.Should().HaveCount(12);
        card.Normals.Should().HaveCount(8 * 3);
        card.Uvs.Should().HaveCount(8 * 2);
    }

    [Fact]
    public void CardMesh_NormalsAreOutwardFacing()
    {
        TerrainFoliageSourceMesh card = TerrainFoliageMesh.CreateCardMesh();

        card.Normals[2].Should().Be(1f);
        card.Normals[12].Should().Be(1f);
    }

    [Fact]
    public void BatchMesh_RotatesPositionsAroundY()
    {
        TerrainFoliageInstance[] instances =
        [
            new TerrainFoliageInstance(0f, 0f, 0f, MathF.PI / 2f, 1f),
        ];

        TerrainMeshData mesh = TerrainFoliageMesh.BuildBatch(instances);
        TerrainFoliageSourceMesh card = TerrainFoliageMesh.CreateCardMesh();
        float sourceX = card.Positions[0];
        float sourceZ = card.Positions[2];

        mesh.Positions[0].Should().BeApproximately(-sourceZ, 1e-5f);
        mesh.Positions[2].Should().BeApproximately(-sourceX, 1e-5f);
    }

    [Fact]
    public void BatchMesh_ExpandsCustomSourceMesh()
    {
        var source = new TerrainFoliageSourceMesh(
            [0f, 0f, 0f, 1f, 0f, 0f, 1f, 1f, 0f],
            [0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f],
            [0f, 0f, 1f, 0f, 1f, 1f],
            [0u, 1u, 2u]);

        TerrainFoliageInstance[] instances =
        [
            new TerrainFoliageInstance(10f, 0f, 20f, 0f, 1f),
            new TerrainFoliageInstance(-5f, 3f, 7f, 0f, 2f),
        ];

        TerrainMeshData mesh = TerrainFoliageMesh.BuildBatch(instances, source);

        mesh.VertexCount.Should().Be(6);
        mesh.TriangleCount.Should().Be(2);
        mesh.Positions[0].Should().BeApproximately(10f, 1e-5f);
        mesh.Positions[1].Should().BeApproximately(0f, 1e-5f);
        mesh.Positions[2].Should().BeApproximately(20f, 1e-5f);
        mesh.Positions[3].Should().BeApproximately(11f, 1e-5f);
        mesh.Positions[9].Should().BeApproximately(-5f, 1e-5f);
        mesh.Positions[10].Should().BeApproximately(3f, 1e-5f);
        mesh.Positions[12].Should().BeApproximately(-3f, 1e-5f);
    }
}
