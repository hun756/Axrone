using Axrone.Terrain;
using FluentAssertions;
using Xunit;

namespace Axrone.Terrain.Tests;

public sealed class TerrainSplatTests
{
    private static TerrainLayer CreateLayer(string id = "layer_1", float tiling = 8f) =>
        new() { Id = id, Name = "Grass", TextureAsset = string.Empty, Tiling = tiling };

    [Fact]
    public void Codec_RoundTripsWeightBufferExactly()
    {
        const int resolution = 64;
        var splat = new byte[resolution * resolution * 4];
        for (var index = 0; index < splat.Length; index += 4)
        {
            splat[index] = 255;
        }

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
        string payload = TerrainSplatCodec.Encode(new byte[64 * 64 * 4]);

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
