using System.Numerics;
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
    public void BrushRaise_ChangesHeightsWithEdgeFalloff()
    {
        TerrainDescriptor descriptor = CreateDescriptor();
        var heights = new float[33 * 33];
        var brush = new TerrainBrushOptions
        {
            Kind = TerrainBrushKind.Raise,
            Radius = 12f,
            Strength = 1f,
        };

        bool changed = TerrainSculptor.ApplyBrushStamp(heights, descriptor, brush, 0f, 0f);

        float center = heights[(16 * 33) + 16];
        float edge = heights[(16 * 33) + 20];
        float outside = heights[0];

        changed.Should().BeTrue();
        center.Should().BeGreaterThan(0f);
        edge.Should().BeGreaterThan(0f);
        edge.Should().BeLessThan(center);
        outside.Should().Be(0f);
    }

    [Fact]
    public void Lower_MirrorsRaiseAndClampsAtZero()
    {
        TerrainDescriptor descriptor = CreateDescriptor();
        var heights = new float[33 * 33];
        Array.Fill(heights, 0.5f);
        var brush = new TerrainBrushOptions
        {
            Kind = TerrainBrushKind.Lower,
            Radius = 12f,
            Strength = 1f,
        };

        TerrainSculptor.ApplyBrushStamp(heights, descriptor, brush, 0f, 0f);
        heights[(16 * 33) + 16].Should().BeLessThan(0.5f);

        var floor = new float[33 * 33];
        bool changed = TerrainSculptor.ApplyBrushStamp(floor, descriptor, brush, 0f, 0f);

        changed.Should().BeFalse();
        floor.Should().OnlyContain(value => value == 0f);
    }

    [Fact]
    public void Smooth_ConvergesSpikeTowardNeighborhoodMean()
    {
        TerrainDescriptor descriptor = CreateDescriptor();
        var heights = new float[33 * 33];
        heights[(16 * 33) + 16] = 1f;
        var brush = new TerrainBrushOptions
        {
            Kind = TerrainBrushKind.Smooth,
            Radius = 8f,
            Strength = 1f,
        };

        TerrainSculptor.ApplyBrushStamp(heights, descriptor, brush, 0f, 0f);

        heights[(16 * 33) + 16].Should().BeLessThan(1f);
        heights[(16 * 33) + 15].Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Flatten_PullsNeighborhoodTowardCenterHeight()
    {
        TerrainDescriptor descriptor = CreateDescriptor();
        var heights = new float[33 * 33];
        for (var index = 0; index < heights.Length; index++)
        {
            heights[index] = (index % 33) / 32f;
        }

        float centerBefore = heights[(16 * 33) + 16];
        var brush = new TerrainBrushOptions
        {
            Kind = TerrainBrushKind.Flatten,
            Radius = 16f,
            Strength = 1f,
        };

        TerrainSculptor.ApplyBrushStamp(heights, descriptor, brush, 0f, 0f);

        float left = heights[(16 * 33) + 12];
        float right = heights[(16 * 33) + 20];
        MathF.Abs(left - centerBefore).Should().BeLessThan(MathF.Abs((12f / 32f) - centerBefore));
        MathF.Abs(right - centerBefore).Should().BeLessThan(MathF.Abs((20f / 32f) - centerBefore));
    }

    [Fact]
    public void Brush_RejectsMismatchedWorkingBuffer()
    {
        var heights = new float[10];
        var descriptor = CreateDescriptor();
        var brush = new TerrainBrushOptions();

        heights.Invoking(h => TerrainSculptor.ApplyBrushStamp(h, descriptor, brush, 0f, 0f))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.HeightmapSizeMismatch);
    }

    [Fact]
    public void Brush_ReturnsFalseOutsideFootprint()
    {
        var heights = new float[33 * 33];
        var brush = new TerrainBrushOptions
        {
            Kind = TerrainBrushKind.Raise,
            Radius = 5f,
            Strength = 1f,
        };

        bool changed = TerrainSculptor.ApplyBrushStamp(heights, CreateDescriptor(), brush, 200f, 200f);

        changed.Should().BeFalse();
        heights.Should().OnlyContain(value => value == 0f);
    }

    [Fact]
    public void Brush_RaiseClampsAtOneAndReportsNoChange()
    {
        var heights = new float[33 * 33];
        Array.Fill(heights, 1f);
        var brush = new TerrainBrushOptions
        {
            Kind = TerrainBrushKind.Raise,
            Radius = 10f,
            Strength = 1f,
        };

        bool changed = TerrainSculptor.ApplyBrushStamp(heights, CreateDescriptor(), brush, 0f, 0f);

        changed.Should().BeFalse();
        heights.Should().OnlyContain(value => value == 1f);
    }

    [Fact]
    public void Raycast_HitsFlatTerrainAtExpectedPoint()
    {
        TerrainDescriptor descriptor = CreateDescriptor(maxHeight: 10f);
        TerrainHeightmap heightmap = TerrainHeightmap.CreateFlat(33, 0.5f);
        var ray = new TerrainRay(new Vector3(4f, 50f, -6f), new Vector3(0f, -1f, 0f));

        TerrainRaycastHit? hit = TerrainRaycaster.Raycast(heightmap, descriptor, ray);

        hit.Should().NotBeNull();
        hit!.Value.Point.X.Should().BeApproximately(4f, 0.1f);
        hit!.Value.Point.Z.Should().BeApproximately(-6f, 0.1f);
        hit!.Value.Point.Y.Should().BeApproximately(5f, 0.1f);
        hit!.Value.U.Should().BeApproximately((4f + 32f) / 64f, 0.01f);
        hit!.Value.V.Should().BeApproximately((-6f + 32f) / 64f, 0.01f);
    }

    [Fact]
    public void Raycast_HitsNoiseTerrainFromObliqueAngle()
    {
        TerrainDescriptor descriptor = CreateDescriptor(maxHeight: 20f, resolution: 65);
        TerrainHeightmap heightmap = TerrainNoiseGenerator.Generate(
            descriptor,
            new TerrainNoiseOptions { Seed = 21 });
        var ray = new TerrainRay(new Vector3(-80f, 60f, -80f), new Vector3(1f, -0.6f, 1f));

        TerrainRaycastHit? hit = TerrainRaycaster.Raycast(heightmap, descriptor, ray);

        hit.Should().NotBeNull();
        MathF.Abs(hit!.Value.Point.X).Should().BeLessThanOrEqualTo(32f);
        MathF.Abs(hit!.Value.Point.Z).Should().BeLessThanOrEqualTo(32f);

        float sampled = heightmap.SampleHeight(hit!.Value.U, hit!.Value.V) * descriptor.MaxHeight;
        hit!.Value.Point.Y.Should().BeApproximately(sampled, 0.1f);
    }

    [Fact]
    public void Raycast_MissesOutsideFootprint()
    {
        TerrainHeightmap heightmap = TerrainHeightmap.CreateFlat(33, 0.2f);
        var ray = new TerrainRay(new Vector3(500f, 10f, 500f), new Vector3(1f, 0f, 0f));

        TerrainRaycaster.Raycast(heightmap, CreateDescriptor(), ray, 2000f).Should().BeNull();
    }

    [Fact]
    public void Raycast_ReturnsNullForZeroDirection()
    {
        TerrainHeightmap heightmap = TerrainHeightmap.CreateFlat(33, 0.5f);
        var ray = new TerrainRay(new Vector3(0f, 50f, 0f), Vector3.Zero);

        TerrainRaycaster.Raycast(heightmap, CreateDescriptor(), ray).Should().BeNull();
    }

    [Fact]
    public void Raycast_MissesWhenStartingBelowAndGoingUp()
    {
        TerrainDescriptor descriptor = CreateDescriptor(maxHeight: 10f);
        TerrainHeightmap heightmap = TerrainHeightmap.CreateFlat(33, 0.5f);
        var ray = new TerrainRay(new Vector3(0f, -1f, 0f), new Vector3(0f, 1f, 0f));

        TerrainRaycaster.Raycast(heightmap, descriptor, ray).Should().BeNull();
    }

    [Fact]
    public void Raycast_MissesHorizontalRayAboveTerrain()
    {
        TerrainDescriptor descriptor = CreateDescriptor(maxHeight: 10f);
        TerrainHeightmap heightmap = TerrainHeightmap.CreateFlat(33, 0.2f);
        var ray = new TerrainRay(new Vector3(-50f, 50f, 0f), new Vector3(1f, 0f, 0f));

        TerrainRaycaster.Raycast(heightmap, descriptor, ray).Should().BeNull();
    }

    [Fact]
    public void Raycast_MissesBeyondMaxDistance()
    {
        TerrainDescriptor descriptor = CreateDescriptor(maxHeight: 10f);
        TerrainHeightmap heightmap = TerrainHeightmap.CreateFlat(33, 0.5f);
        var ray = new TerrainRay(new Vector3(0f, 50f, 0f), new Vector3(0f, -1f, 0f));

        TerrainRaycaster.Raycast(heightmap, descriptor, ray).Should().NotBeNull();
        TerrainRaycaster.Raycast(heightmap, descriptor, ray, 1f).Should().BeNull();
    }

    [Fact]
    public void Raycast_HitsAtFootprintEdge()
    {
        TerrainDescriptor descriptor = CreateDescriptor(maxHeight: 10f);
        TerrainHeightmap heightmap = TerrainHeightmap.CreateFlat(33, 0.5f);
        var ray = new TerrainRay(new Vector3(32f, 50f, 0f), new Vector3(0f, -1f, 0f));

        TerrainRaycastHit? hit = TerrainRaycaster.Raycast(heightmap, descriptor, ray);

        hit.Should().NotBeNull();
        hit!.Value.Point.X.Should().BeApproximately(32f, 0.5f);
        hit!.Value.Point.Y.Should().BeApproximately(5f, 0.1f);
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
