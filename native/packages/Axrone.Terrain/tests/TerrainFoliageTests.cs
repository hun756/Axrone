using Axrone.Terrain;
using FluentAssertions;
using Xunit;

namespace Axrone.Terrain.Tests;

public sealed class TerrainFoliageTests
{
    private static TerrainDescriptor CreateDescriptor(
        float width = 64f,
        float length = 64f,
        float maxHeight = 20f,
        int resolution = 33) => new(width, length, maxHeight, resolution);

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

    private static byte[] PaintEverywhere()
    {
        byte[] density = TerrainFoliageDensity.CreateBuffer(64);
        Array.Fill(density, (byte)255);
        return density;
    }

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
    public void DensityBuffer_StartsEmpty()
    {
        byte[] density = TerrainFoliageDensity.CreateBuffer(64);

        density.Should().HaveCount(64 * 64);
        density.Should().OnlyContain(value => value == 0);
    }

    [Fact]
    public void DensityBuffer_RejectsInvalidResolution()
    {
        FluentActions.Invoking(() => TerrainFoliageDensity.CreateBuffer(100))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.InvalidResolution);
    }

    [Fact]
    public void DensityStamp_RaisesOnlyInsideRadius()
    {
        byte[] density = TerrainFoliageDensity.CreateBuffer(64);
        var brush = new TerrainBrushOptions { Radius = 10f, Strength = 1f };

        bool changed = TerrainFoliageDensity.ApplyStamp(density, 64, CreateDescriptor(), brush, 0f, 0f);

        changed.Should().BeTrue();
        TerrainFoliageDensity.Sample(density, 64, 0.5f, 0.5f).Should().BeGreaterThan(0f);
        TerrainFoliageDensity.Sample(density, 64, 0f, 0f).Should().Be(0f);
    }

    [Fact]
    public void DensityStamp_EraseRemovesPaintedDensity()
    {
        byte[] density = TerrainFoliageDensity.CreateBuffer(64);
        var brush = new TerrainBrushOptions { Radius = 12f, Strength = 1f };
        TerrainDescriptor descriptor = CreateDescriptor();

        for (var stamp = 0; stamp < 12; stamp++)
        {
            TerrainFoliageDensity.ApplyStamp(density, 64, descriptor, brush, 0f, 0f);
        }

        TerrainFoliageDensity.Sample(density, 64, 0.5f, 0.5f).Should().BeGreaterThan(0.8f);

        for (var stamp = 0; stamp < 24; stamp++)
        {
            TerrainFoliageDensity.ApplyStamp(density, 64, descriptor, brush, 0f, 0f, erase: true);
        }

        TerrainFoliageDensity.Sample(density, 64, 0.5f, 0.5f).Should().Be(0f);
    }

    [Fact]
    public void DensityStamp_RejectsMismatchedBuffer()
    {
        var density = new byte[16];

        density.Invoking(buffer => TerrainFoliageDensity.ApplyStamp(
                buffer,
                64,
                CreateDescriptor(),
                new TerrainBrushOptions(),
                0f,
                0f))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.SplatSizeMismatch);
    }

    [Fact]
    public void DensitySample_ReturnsExactCornerValues()
    {
        byte[] density = TerrainFoliageDensity.CreateBuffer(64);
        density[0] = 128;
        density[63] = 255;

        TerrainFoliageDensity.Sample(density, 64, 0f, 0f).Should().BeApproximately(128f / 255f, 0.01f);
        TerrainFoliageDensity.Sample(density, 64, 1f, 0f).Should().BeApproximately(1f, 0.01f);
    }

    [Fact]
    public void DensitySample_ClampsOutsideUnitRange()
    {
        byte[] density = TerrainFoliageDensity.CreateBuffer(64);
        density[0] = 200;

        float inside = TerrainFoliageDensity.Sample(density, 64, 0f, 0f);
        float outside = TerrainFoliageDensity.Sample(density, 64, -0.5f, -0.5f);

        outside.Should().BeApproximately(inside, 1e-5f);
    }

    [Fact]
    public void DensityCodec_RoundTripsPaintedBuffers()
    {
        byte[] density = TerrainFoliageDensity.CreateBuffer(64);
        var brush = new TerrainBrushOptions { Radius = 16f, Strength = 1f };
        TerrainFoliageDensity.ApplyStamp(density, 64, CreateDescriptor(), brush, 4f, -6f);

        string payload = TerrainFoliageDensity.Encode(density);
        TerrainFoliageDensity.IsPayload(payload).Should().BeTrue();
        TerrainFoliageDensity.Decode(payload, 64).Should().Equal(density);
    }

    [Fact]
    public void DensityCodec_RejectsForeignPayloadsAndWrongResolution()
    {
        TerrainFoliageDensity.IsPayload("atsp1:AAAA").Should().BeFalse();

        "nope"
            .Invoking(value => TerrainFoliageDensity.Decode(value, 64))
            .Should().Throw<TerrainException>();

        string payload = TerrainFoliageDensity.Encode(TerrainFoliageDensity.CreateBuffer(64));
        payload.Invoking(value => TerrainFoliageDensity.Decode(value, 128))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.SplatSizeMismatch);
    }

    [Fact]
    public void DensityCodec_IsPayloadRejectsNonPayloads()
    {
        TerrainFoliageDensity.IsPayload(null).Should().BeFalse();
        TerrainFoliageDensity.IsPayload(string.Empty).Should().BeFalse();
    }

    [Fact]
    public void Scatter_IsDeterministicForIdenticalInputs()
    {
        TerrainHeightmap heightmap = TerrainHeightmap.CreateFlat(33, 0.25f);
        TerrainDescriptor descriptor = CreateDescriptor();
        byte[] density = PaintEverywhere();
        TerrainFoliageLayer layer = CreateLayer();

        TerrainFoliageInstance[] first =
            TerrainFoliageScatter.Scatter(heightmap, descriptor, density, 64, layer, 1337);
        TerrainFoliageInstance[] second =
            TerrainFoliageScatter.Scatter(heightmap, descriptor, density, 64, layer, 1337);
        TerrainFoliageInstance[] reseeded =
            TerrainFoliageScatter.Scatter(heightmap, descriptor, density, 64, layer, 7331);

        first.Should().NotBeEmpty();
        second.Should().Equal(first);
        reseeded.Should().NotEqual(first);
    }

    [Fact]
    public void Scatter_ScalesInstanceCountWithPaintedDensity()
    {
        TerrainHeightmap heightmap = TerrainHeightmap.CreateFlat(33, 0.25f);
        TerrainDescriptor descriptor = CreateDescriptor();
        TerrainFoliageLayer layer = CreateLayer(density: 1f);

        byte[] half = PaintEverywhere();
        Array.Fill(half, (byte)128);

        TerrainFoliageInstance[] empty = TerrainFoliageScatter.Scatter(
            heightmap,
            descriptor,
            TerrainFoliageDensity.CreateBuffer(64),
            64,
            layer,
            42);
        TerrainFoliageInstance[] sparse = TerrainFoliageScatter.Scatter(heightmap, descriptor, half, 64, layer, 42);
        TerrainFoliageInstance[] dense = TerrainFoliageScatter.Scatter(
            heightmap,
            descriptor,
            PaintEverywhere(),
            64,
            layer,
            42);

        empty.Should().BeEmpty();
        sparse.Should().NotBeEmpty();
        dense.Length.Should().BeGreaterThan((int)(sparse.Length * 1.5));
        dense.Length.Should().BeLessThanOrEqualTo(TerrainLimits.MaxFoliageInstances);
    }

    [Fact]
    public void Scatter_RespectsMaxSlopeFilter()
    {
        const int resolution = 33;
        var heights = new float[resolution * resolution];
        for (var z = 0; z < resolution; z++)
        {
            for (var x = 0; x < resolution; x++)
            {
                heights[(z * resolution) + x] = x / (float)(resolution - 1);
            }
        }

        TerrainHeightmap ramp = TerrainHeightmap.FromRawHeights(resolution, heights);
        TerrainDescriptor descriptor = CreateDescriptor();
        byte[] density = PaintEverywhere();

        TerrainFoliageInstance[] permissive = TerrainFoliageScatter.Scatter(
            ramp,
            descriptor,
            density,
            64,
            CreateLayer(maxSlopeDeg: 90f),
            42);
        TerrainFoliageInstance[] blocked = TerrainFoliageScatter.Scatter(
            ramp,
            descriptor,
            density,
            64,
            CreateLayer(maxSlopeDeg: 5f),
            42);

        permissive.Should().NotBeEmpty();
        blocked.Should().BeEmpty();
    }

    [Fact]
    public void Scatter_PlacesInstancesOnSurfaceWithinBounds()
    {
        TerrainDescriptor descriptor = CreateDescriptor();
        TerrainFoliageInstance[] instances = TerrainFoliageScatter.Scatter(
            TerrainHeightmap.CreateFlat(33, 0.5f),
            descriptor,
            PaintEverywhere(),
            64,
            CreateLayer(),
            7);

        foreach (TerrainFoliageInstance instance in instances)
        {
            MathF.Abs(instance.X).Should().BeLessThanOrEqualTo(descriptor.Width / 2f);
            MathF.Abs(instance.Z).Should().BeLessThanOrEqualTo(descriptor.Length / 2f);
            instance.Y.Should().BeApproximately(descriptor.MaxHeight * 0.5f, 1e-4f);
            instance.Scale.Should().BeGreaterThanOrEqualTo(0.8f).And.BeLessThanOrEqualTo(1.4f);
        }
    }

    [Fact]
    public void Scatter_ReturnsEmptyForZeroLayerDensity()
    {
        TerrainFoliageInstance[] instances = TerrainFoliageScatter.Scatter(
            TerrainHeightmap.CreateFlat(33),
            CreateDescriptor(),
            PaintEverywhere(),
            64,
            CreateLayer(density: 0f),
            1);

        instances.Should().BeEmpty();
    }

    [Fact]
    public void Scatter_KeepsScaleWithinConfiguredRange()
    {
        TerrainFoliageInstance[] instances = TerrainFoliageScatter.Scatter(
            TerrainHeightmap.CreateFlat(33, 0.5f),
            CreateDescriptor(),
            PaintEverywhere(),
            64,
            CreateLayer(minScale: 0.5f, maxScale: 2f),
            99);

        foreach (TerrainFoliageInstance instance in instances)
        {
            instance.Scale.Should().BeGreaterThanOrEqualTo(0.5f).And.BeLessThanOrEqualTo(2f);
        }
    }

    [Fact]
    public void Scatter_RespectsInstanceCap()
    {
        TerrainFoliageInstance[] instances = TerrainFoliageScatter.Scatter(
            TerrainHeightmap.CreateFlat(33, 0.5f),
            CreateDescriptor(width: 512f, length: 512f),
            PaintEverywhere(),
            64,
            CreateLayer(density: 1f),
            42);

        instances.Length.Should().BeLessThanOrEqualTo(TerrainLimits.MaxFoliageInstances);
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
