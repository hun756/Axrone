using Axrone.Terrain;
using FluentAssertions;
using Xunit;

namespace Axrone.Terrain.Tests;

public sealed class TerrainSplatTests
{
    private static TerrainDescriptor CreateDescriptor(
        float width = 64f,
        float length = 64f,
        float maxHeight = 20f,
        int resolution = 33) => new(width, length, maxHeight, resolution);

    private static TerrainLayer CreateLayer(string id = "layer_1", float tiling = 8f) =>
        new() { Id = id, Name = "Grass", TextureAsset = string.Empty, Tiling = tiling };

    private static int ChannelSumAt(ReadOnlySpan<byte> splat, int texel) =>
        splat[texel * 4] + splat[(texel * 4) + 1] + splat[(texel * 4) + 2] + splat[(texel * 4) + 3];

    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    public void CreateBuffer_AssignsFullWeightToFirstLayer(int resolution)
    {
        byte[] splat = TerrainSplatPainter.CreateBuffer(resolution);

        splat.Should().HaveCount(resolution * resolution * 4);
        splat[0].Should().Be(255);
        splat[1].Should().Be(0);
        ChannelSumAt(splat, 0).Should().Be(255);
        ChannelSumAt(splat, (resolution * resolution) - 1).Should().Be(255);
    }

    [Fact]
    public void CreateBuffer_RejectsUnsupportedResolution()
    {
        FluentActions.Invoking(() => TerrainSplatPainter.CreateBuffer(100))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.InvalidResolution);
    }

    [Fact]
    public void Stamp_IncreasesTargetChannelAndPreservesInvariant()
    {
        const int resolution = 64;
        byte[] splat = TerrainSplatPainter.CreateBuffer(resolution);
        var brush = new TerrainBrushOptions { Radius = 20f, Strength = 1f, Falloff = 1f };

        bool changed = TerrainSplatPainter.ApplyStamp(
            splat,
            resolution,
            CreateDescriptor(),
            brush,
            1,
            0f,
            0f);

        changed.Should().BeTrue();
        int centerTexel = ((resolution / 2) * resolution) + (resolution / 2);
        splat[(centerTexel * 4) + 1].Should().BeGreaterThan(0);
        splat[centerTexel * 4].Should().BeLessThan(255);

        for (var texel = 0; texel < resolution * resolution; texel++)
        {
            ChannelSumAt(splat, texel).Should().Be(255);
        }
    }

    [Fact]
    public void Stamp_AppliesWeakerWeightAtEdge()
    {
        const int resolution = 64;
        byte[] splat = TerrainSplatPainter.CreateBuffer(resolution);
        var brush = new TerrainBrushOptions { Radius = 24f, Strength = 1f, Falloff = 1f };

        TerrainSplatPainter.ApplyStamp(splat, resolution, CreateDescriptor(), brush, 1, 0f, 0f);

        const int half = resolution / 2;
        int centerWeight = splat[(((half * resolution) + half) * 4) + 1];
        int edgeWeight = splat[(((half * resolution) + half + 10) * 4) + 1];

        centerWeight.Should().BeGreaterThan(edgeWeight);
        edgeWeight.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Stamp_ConvergesTowardFullTargetWeight()
    {
        const int resolution = 64;
        byte[] splat = TerrainSplatPainter.CreateBuffer(resolution);
        var brush = new TerrainBrushOptions { Radius = 20f, Strength = 1f, Falloff = 1f };
        int centerTexel = ((resolution / 2) * resolution) + (resolution / 2);

        for (var stroke = 0; stroke < 25; stroke++)
        {
            TerrainSplatPainter.ApplyStamp(splat, resolution, CreateDescriptor(), brush, 2, 0f, 0f);
        }

        splat[(centerTexel * 4) + 2].Should().BeGreaterThan(240);
        ChannelSumAt(splat, centerTexel).Should().Be(255);
    }

    [Fact]
    public void Stamp_PreservesInvariantAfterRepeatedPainting()
    {
        const int resolution = 64;
        byte[] splat = TerrainSplatPainter.CreateBuffer(resolution);
        var brush = new TerrainBrushOptions { Radius = 30f, Strength = 1f, Falloff = 1f };

        for (var stroke = 0; stroke < 50; stroke++)
        {
            TerrainSplatPainter.ApplyStamp(splat, resolution, CreateDescriptor(), brush, 3, 0f, 0f);
        }

        for (var texel = 0; texel < resolution * resolution; texel++)
        {
            ChannelSumAt(splat, texel).Should().Be(255);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Stamp_RejectsOutOfRangeLayerIndex(int layerIndex)
    {
        const int resolution = 64;
        byte[] splat = TerrainSplatPainter.CreateBuffer(resolution);

        splat.Invoking(buffer => TerrainSplatPainter.ApplyStamp(
                buffer,
                resolution,
                CreateDescriptor(),
                new TerrainBrushOptions(),
                layerIndex,
                0f,
                0f))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.ValidationFailed);
    }

    [Fact]
    public void Stamp_RejectsMismatchedBuffer()
    {
        var splat = new byte[16];

        splat.Invoking(buffer => TerrainSplatPainter.ApplyStamp(
                buffer,
                64,
                CreateDescriptor(),
                new TerrainBrushOptions(),
                0,
                0f,
                0f))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.SplatSizeMismatch);
    }

    [Fact]
    public void Sample_ReturnsNormalizedWeightsSummingToOne()
    {
        const int resolution = 64;
        byte[] splat = TerrainSplatPainter.CreateBuffer(resolution);
        var brush = new TerrainBrushOptions { Radius = 24f, Strength = 1f };
        TerrainSplatPainter.ApplyStamp(splat, resolution, CreateDescriptor(), brush, 1, 0f, 0f);

        SplatWeights weights = TerrainSplatPainter.Sample(splat, resolution, 0.5f, 0.5f);

        weights.Sum.Should().BeApproximately(1f, 0.01f);
        weights.G.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Sample_ReturnsExactTexelWeightsAtCorners()
    {
        const int resolution = 64;
        byte[] splat = TerrainSplatPainter.CreateBuffer(resolution);

        SplatWeights corner = TerrainSplatPainter.Sample(splat, resolution, 0f, 0f);
        corner.R.Should().BeApproximately(1f, 0.01f);
        corner.G.Should().BeApproximately(0f, 0.01f);

        SplatWeights opposite = TerrainSplatPainter.Sample(splat, resolution, 1f, 1f);
        opposite.R.Should().BeApproximately(1f, 0.01f);
    }

    [Fact]
    public void Sample_ClampsOutsideUnitRange()
    {
        const int resolution = 64;
        byte[] splat = TerrainSplatPainter.CreateBuffer(resolution);

        SplatWeights inside = TerrainSplatPainter.Sample(splat, resolution, 0f, 0f);
        SplatWeights outside = TerrainSplatPainter.Sample(splat, resolution, -1f, -1f);

        outside.R.Should().BeApproximately(inside.R, 1e-5f);
        outside.G.Should().BeApproximately(inside.G, 1e-5f);
    }

    [Fact]
    public void Codec_RoundTripsWeightBufferExactly()
    {
        const int resolution = 64;
        byte[] splat = TerrainSplatPainter.CreateBuffer(resolution);
        var brush = new TerrainBrushOptions { Radius = 18f, Strength = 0.7f };
        TerrainSplatPainter.ApplyStamp(splat, resolution, CreateDescriptor(), brush, 2, 4f, -3f);

        string payload = TerrainSplatCodec.Encode(splat);
        TerrainSplatCodec.IsPayload(payload).Should().BeTrue();

        byte[] decoded = TerrainSplatCodec.Decode(payload, resolution);
        decoded.Should().Equal(splat);
    }

    [Fact]
    public void Codec_RejectsMissingPrefix()
    {
        "not-a-splat"
            .Invoking(payload => TerrainSplatCodec.Decode(payload, 64))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.SourceDecodeFailed);
    }

    [Fact]
    public void Codec_RejectsLengthMismatch()
    {
        string payload = TerrainSplatCodec.Encode(TerrainSplatPainter.CreateBuffer(64));

        payload.Invoking(value => TerrainSplatCodec.Decode(value, 128))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.SplatSizeMismatch);
    }

    [Fact]
    public void Codec_RejectsInvalidBase64()
    {
        "atsp1:!!!invalid-base64!!!"
            .Invoking(value => TerrainSplatCodec.Decode(value, 64))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.SourceDecodeFailed);
    }

    [Fact]
    public void Codec_IsPayloadRejectsNonPayloads()
    {
        TerrainSplatCodec.IsPayload(null).Should().BeFalse();
        TerrainSplatCodec.IsPayload(string.Empty).Should().BeFalse();
        TerrainSplatCodec.IsPayload("athf1:AAAA").Should().BeFalse();
    }

    [Fact]
    public void Layers_AcceptUpToMaximumCount()
    {
        var layers = new TerrainLayer[TerrainLimits.MaxLayers];
        for (var index = 0; index < layers.Length; index++)
        {
            layers[index] = CreateLayer($"layer_{index}");
        }

        layers.Invoking(value => TerrainValidation.ValidateLayers(value)).Should().NotThrow();
    }

    [Fact]
    public void Layers_RejectTooMany()
    {
        var layers = new TerrainLayer[TerrainLimits.MaxLayers + 1];
        for (var index = 0; index < layers.Length; index++)
        {
            layers[index] = CreateLayer($"layer_{index}");
        }

        layers.Invoking(value => TerrainValidation.ValidateLayers(value))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.ValidationFailed);
    }

    [Fact]
    public void Layers_RejectInvalidTilingAndEmptyId()
    {
        new[] { CreateLayer(tiling: 0f) }
            .Invoking(value => TerrainValidation.ValidateLayers(value))
            .Should().Throw<TerrainException>();

        new[] { CreateLayer(id: string.Empty) }
            .Invoking(value => TerrainValidation.ValidateLayers(value))
            .Should().Throw<TerrainException>();
    }
}
