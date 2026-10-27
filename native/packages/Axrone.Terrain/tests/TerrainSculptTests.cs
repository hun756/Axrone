using Axrone.Terrain;
using FluentAssertions;
using Xunit;

namespace Axrone.Terrain.Tests;

public sealed class TerrainSculptTests
{
    private static TerrainDescriptor CreateDescriptor(
        float width = 64f,
        float length = 64f,
        float maxHeight = 20f,
        int resolution = 33) => new(width, length, maxHeight, resolution);

    [Fact]
    public void BrushOptions_HaveDocumentedDefaults()
    {
        var options = new TerrainBrushOptions();

        options.Kind.Should().Be(TerrainBrushKind.Raise);
        options.Radius.Should().Be(8f);
        options.Strength.Should().Be(0.5f);
        options.Falloff.Should().Be(1f);
        options.FlattenTarget.Should().Be(0f);
    }

    [Theory]
    [InlineData(0f, 0.5f, 1f, 0f)]
    [InlineData(8f, 0f, 1f, 0f)]
    [InlineData(8f, 1.5f, 1f, 0f)]
    [InlineData(8f, 0.5f, 0f, 0f)]
    [InlineData(8f, 0.5f, 1f, 2f)]
    public void BrushOptions_RejectOutOfRangeParameters(
        float radius,
        float strength,
        float falloff,
        float flattenTarget)
    {
        var options = new TerrainBrushOptions
        {
            Radius = radius,
            Strength = strength,
            Falloff = falloff,
            FlattenTarget = flattenTarget,
        };

        options.Invoking(o => o.Validate())
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.ValidationFailed);
    }

    [Fact]
    public void BrushOptions_RejectUnknownKind()
    {
        var options = new TerrainBrushOptions { Kind = (TerrainBrushKind)42 };

        options.Invoking(o => o.Validate())
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.ValidationFailed);
    }

    [Fact]
    public void BrushOptions_AcceptBoundaryValues()
    {
        new TerrainBrushOptions { Strength = 1f }.Invoking(o => o.Validate()).Should().NotThrow();
        new TerrainBrushOptions { Falloff = 8f }.Invoking(o => o.Validate()).Should().NotThrow();
        new TerrainBrushOptions { FlattenTarget = 0f }.Invoking(o => o.Validate()).Should().NotThrow();
        new TerrainBrushOptions { FlattenTarget = 1f }.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Fact]
    public void HeightCodec_RoundTripsWithinQuantizationTolerance()
    {
        TerrainDescriptor descriptor = CreateDescriptor();
        TerrainHeightmap heightmap = TerrainNoiseGenerator.Generate(
            descriptor,
            new TerrainNoiseOptions { Seed = 7 });

        string payload = TerrainHeightCodec.Encode(heightmap.Heights);
        TerrainHeightCodec.IsPayload(payload).Should().BeTrue();

        float[] decoded = TerrainHeightCodec.Decode(payload, 33);
        decoded.Should().HaveCount(33 * 33);
        for (var index = 0; index < decoded.Length; index++)
        {
            MathF.Abs(decoded[index] - heightmap.Heights[index]).Should().BeLessThan(1e-4f);
        }
    }

    [Fact]
    public void HeightCodec_RejectsMissingPrefix()
    {
        "bogus-payload"
            .Invoking(payload => TerrainHeightCodec.Decode(payload, 33))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.SourceDecodeFailed);
    }

    [Fact]
    public void HeightCodec_RejectsLengthMismatch()
    {
        string payload = TerrainHeightCodec.Encode(new float[33 * 33]);

        payload.Invoking(value => TerrainHeightCodec.Decode(value, 65))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.HeightmapSizeMismatch);
    }

    [Fact]
    public void HeightCodec_ClampsOutOfRangeValues()
    {
        var heights = new float[33 * 33];
        heights[0] = -0.5f;
        heights[1] = 1.5f;
        heights[2] = 0.5f;

        float[] decoded = TerrainHeightCodec.Decode(TerrainHeightCodec.Encode(heights), 33);

        decoded[0].Should().BeApproximately(0f, 1e-4f);
        decoded[1].Should().BeApproximately(1f, 1e-4f);
        decoded[2].Should().BeApproximately(0.5f, 1e-4f);
    }

    [Fact]
    public void HeightCodec_IsPayloadRejectsNonPayloads()
    {
        TerrainHeightCodec.IsPayload(null).Should().BeFalse();
        TerrainHeightCodec.IsPayload(string.Empty).Should().BeFalse();
        TerrainHeightCodec.IsPayload("atsp1:AAAA").Should().BeFalse();
    }

    [Fact]
    public void HeightCodec_RejectsInvalidResolution()
    {
        string payload = TerrainHeightCodec.Encode(new float[33 * 33]);

        payload.Invoking(value => TerrainHeightCodec.Decode(value, 100))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.InvalidResolution);
    }

    [Fact]
    public void HeightCodec_RejectsInvalidBase64()
    {
        "athf1:!!!invalid-base64!!!"
            .Invoking(value => TerrainHeightCodec.Decode(value, 33))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.SourceDecodeFailed);
    }
}
