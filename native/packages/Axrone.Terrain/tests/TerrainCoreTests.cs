using Axrone.Terrain;
using FluentAssertions;
using Xunit;

namespace Axrone.Terrain.Tests;

public sealed class TerrainCoreTests
{
    private static TerrainDescriptor CreateDescriptor(
        float width = 100f,
        float length = 80f,
        float maxHeight = 30f,
        int resolution = 33) => new(width, length, maxHeight, resolution);

    [Fact]
    public void Exception_StoresCodeAndPrefixesMessage()
    {
        var exception = new TerrainException("bad resolution", TerrainErrorCode.InvalidResolution);

        exception.Message.Should().Be("Terrain: bad resolution");
        exception.Code.Should().Be(TerrainErrorCode.InvalidResolution);
    }

    [Fact]
    public void Descriptor_AcceptsEverySupportedResolution()
    {
        foreach (int resolution in TerrainLimits.Resolutions)
        {
            TerrainLimits.IsResolution(resolution).Should().BeTrue();
            CreateDescriptor(resolution: resolution).Invoking(d => d.Validate()).Should().NotThrow();
        }
    }

    [Fact]
    public void Descriptor_RejectsUnsupportedResolution()
    {
        CreateDescriptor(resolution: 64)
            .Invoking(d => d.Validate())
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.InvalidResolution);
    }

    [Theory]
    [InlineData(0f, 80f, 30f)]
    [InlineData(100f, -5f, 30f)]
    [InlineData(100f, 80f, float.NaN)]
    public void Descriptor_RejectsInvalidDimensions(float width, float length, float maxHeight)
    {
        new TerrainDescriptor(width, length, maxHeight, 33)
            .Invoking(d => d.Validate())
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.InvalidDimensions);
    }

    [Fact]
    public void Descriptor_AcceptsZeroMaxHeight()
    {
        CreateDescriptor(maxHeight: 0f).Invoking(d => d.Validate()).Should().NotThrow();
    }

    [Fact]
    public void NoiseOptions_HaveDocumentedDefaults()
    {
        var options = new TerrainNoiseOptions();

        options.Seed.Should().Be(1337UL);
        options.Frequency.Should().Be(0.02f);
        options.Octaves.Should().Be(4);
        options.Persistence.Should().Be(0.5f);
        options.Lacunarity.Should().Be(2f);
    }

    [Theory]
    [InlineData(0f, 4, 0.5f, 2f)]
    [InlineData(0.02f, 0, 0.5f, 2f)]
    [InlineData(0.02f, 9, 0.5f, 2f)]
    [InlineData(0.02f, 4, 0f, 2f)]
    [InlineData(0.02f, 4, 1.5f, 2f)]
    [InlineData(0.02f, 4, 0.5f, 0.5f)]
    public void NoiseOptions_RejectOutOfRangeParameters(
        float frequency,
        int octaves,
        float persistence,
        float lacunarity)
    {
        var options = new TerrainNoiseOptions
        {
            Frequency = frequency,
            Octaves = octaves,
            Persistence = persistence,
            Lacunarity = lacunarity,
        };

        options.Invoking(o => o.Validate())
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.ValidationFailed);
    }

    [Fact]
    public void NoiseOptions_AcceptBoundaryValues()
    {
        new TerrainNoiseOptions { Persistence = 1f }.Invoking(o => o.Validate()).Should().NotThrow();
        new TerrainNoiseOptions { Lacunarity = 1f }.Invoking(o => o.Validate()).Should().NotThrow();
        new TerrainNoiseOptions { Octaves = 1 }.Invoking(o => o.Validate()).Should().NotThrow();
        new TerrainNoiseOptions { Octaves = 8 }.Invoking(o => o.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Heightmap_CreateFlatFillsUniformValues()
    {
        TerrainHeightmap heightmap = TerrainHeightmap.CreateFlat(33, 0.25f);

        heightmap.Resolution.Should().Be(33);
        heightmap.Length.Should().Be(33 * 33);
        heightmap.GetHeight(0, 0).Should().BeApproximately(0.25f, 1e-6f);
        heightmap.GetHeight(32, 32).Should().BeApproximately(0.25f, 1e-6f);
    }

    [Fact]
    public void Heightmap_RejectsMismatchedRawBuffers()
    {
        new float[10]
            .Invoking(buffer => TerrainHeightmap.FromRawHeights(33, buffer))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.HeightmapSizeMismatch);
    }

    [Fact]
    public void Heightmap_ClampsRawHeightsAndCopiesBuffer()
    {
        var raw = new float[33 * 33];
        raw[0] = -2f;
        raw[1] = 3f;
        raw[2] = 0.5f;

        TerrainHeightmap heightmap = TerrainHeightmap.FromRawHeights(33, raw);
        raw[2] = 0.9f;

        heightmap.GetHeight(0, 0).Should().Be(0f);
        heightmap.GetHeight(1, 0).Should().Be(1f);
        heightmap.GetHeight(2, 0).Should().BeApproximately(0.5f, 1e-6f);
    }

    [Fact]
    public void Heightmap_RejectsNonFiniteRawValues()
    {
        var raw = new float[33 * 33];
        raw[0] = float.PositiveInfinity;

        raw.Invoking(buffer => TerrainHeightmap.FromRawHeights(33, buffer))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.ValidationFailed);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 33)]
    public void Heightmap_RejectsOutOfGridSamples(int x, int z)
    {
        TerrainHeightmap heightmap = TerrainHeightmap.CreateFlat(33);

        heightmap.Invoking(h => h.GetHeight(x, z)).Should().Throw<TerrainException>();
    }

    [Fact]
    public void Heightmap_SamplesBilinearlyBetweenGridPoints()
    {
        var raw = new float[33 * 33];
        raw[0] = 0f;
        raw[1] = 1f;

        TerrainHeightmap heightmap = TerrainHeightmap.FromRawHeights(33, raw);
        const float midpointU = 0.5f / 32f;

        heightmap.SampleHeight(0f, 0f).Should().BeApproximately(0f, 1e-6f);
        heightmap.SampleHeight(1f / 32f, 0f).Should().BeApproximately(1f, 1e-6f);
        heightmap.SampleHeight(midpointU, 0f).Should().BeApproximately(0.5f, 1e-6f);
        heightmap.SampleHeight(-1f, -1f).Should().BeApproximately(0f, 1e-6f);
    }

    [Fact]
    public void Heightmap_NormalizeSpansFullRange()
    {
        var raw = new float[33 * 33];
        Array.Fill(raw, 0.4f);
        raw[0] = 0.2f;
        raw[1] = 0.6f;

        TerrainHeightmap normalized = TerrainHeightmap.FromRawHeights(33, raw).Normalize();
        TerrainStatistics statistics = normalized.ComputeStatistics();

        statistics.Min.Should().BeApproximately(0f, 1e-6f);
        statistics.Max.Should().BeApproximately(1f, 1e-6f);
        normalized.GetHeight(0, 0).Should().BeApproximately(0f, 1e-6f);
        normalized.GetHeight(1, 0).Should().BeApproximately(1f, 1e-6f);
    }

    [Fact]
    public void Heightmap_NormalizeOnConstantProducesZeros()
    {
        TerrainHeightmap normalized = TerrainHeightmap.CreateFlat(33, 0.5f).Normalize();

        foreach (float value in normalized.Heights)
        {
            value.Should().Be(0f);
        }
    }

    [Fact]
    public void Heightmap_CloneIsIndependent()
    {
        TerrainHeightmap heightmap = TerrainHeightmap.CreateFlat(33, 0.5f);
        TerrainHeightmap clone = heightmap.Clone();

        clone.Should().NotBeSameAs(heightmap);
        clone.GetHeight(5, 5).Should().BeApproximately(0.5f, 1e-6f);
    }

    [Fact]
    public void Heightmap_StatisticsReportMinMaxMean()
    {
        var raw = new float[33 * 33];
        raw[0] = 0.1f;
        raw[1] = 0.9f;
        raw[2] = 0.5f;

        TerrainStatistics statistics = TerrainHeightmap.FromRawHeights(33, raw).ComputeStatistics();

        statistics.Min.Should().BeApproximately(0f, 1e-6f);
        statistics.Max.Should().BeApproximately(0.9f, 1e-6f);
        statistics.Mean.Should().BeGreaterThan(0f).And.BeLessThan(1f);
    }

    [Fact]
    public void Heightmap_SampleHeightClampsAtFarCorner()
    {
        var raw = new float[33 * 33];
        raw[(32 * 33) + 32] = 0.8f;

        TerrainHeightmap heightmap = TerrainHeightmap.FromRawHeights(33, raw);

        heightmap.SampleHeight(1f, 1f).Should().BeApproximately(0.8f, 1e-6f);
        heightmap.SampleHeight(5f, 5f).Should().BeApproximately(0.8f, 1e-6f);
    }

    [Fact]
    public void Decoder_RejectsMalformedPixelBuffers()
    {
        new byte[7]
            .Invoking(pixels => TerrainHeightmapDecoder.DecodeFromRgba(pixels, 2, 2, 33))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.SourceDecodeFailed);
    }

    [Fact]
    public void Decoder_RejectsImagesSmallerThanTwoByTwo()
    {
        new byte[3 * 4]
            .Invoking(pixels => TerrainHeightmapDecoder.DecodeFromRgba(pixels, 1, 3, 33))
            .Should().Throw<TerrainException>()
            .Which.Code.Should().Be(TerrainErrorCode.SourceDecodeFailed);
    }

    [Fact]
    public void Decoder_ResamplesLuminanceGradient()
    {
        const int width = 4;
        const int height = 4;
        var pixels = new byte[width * height * 4];

        for (var z = 0; z < height; z++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = (byte)Math.Round((x / (double)(width - 1)) * 255);
                int offset = ((z * width) + x) * 4;
                pixels[offset] = value;
                pixels[offset + 1] = value;
                pixels[offset + 2] = value;
                pixels[offset + 3] = 255;
            }
        }

        TerrainHeightmap heightmap = TerrainHeightmapDecoder.DecodeFromRgba(pixels, width, height, 33);

        heightmap.GetHeight(0, 0).Should().BeApproximately(0f, 0.01f);
        heightmap.GetHeight(32, 0).Should().BeApproximately(1f, 0.01f);
        heightmap.GetHeight(16, 16).Should().BeGreaterThan(0.3f).And.BeLessThan(0.7f);
    }

    [Fact]
    public void Decoder_AppliesRec709LuminanceCoefficients()
    {
        const int width = 2;
        const int height = 2;
        var pixels = new byte[width * height * 4];

        pixels[0] = 255;
        pixels[3] = 255;
        for (var index = 1; index < 4; index++)
        {
            int offset = index * 4;
            pixels[offset] = 255;
            pixels[offset + 1] = 255;
            pixels[offset + 2] = 255;
            pixels[offset + 3] = 255;
        }

        TerrainHeightmap heightmap = TerrainHeightmapDecoder.DecodeFromRgba(pixels, width, height, 33);

        heightmap.GetHeight(0, 0).Should().BeApproximately(0.2126f, 0.01f);
        heightmap.GetHeight(32, 0).Should().BeApproximately(1f, 0.01f);
    }

    [Fact]
    public void Noise_IsDeterministicForIdenticalSeeds()
    {
        TerrainDescriptor descriptor = CreateDescriptor(resolution: 65);
        TerrainHeightmap first = TerrainNoiseGenerator.Generate(descriptor, new TerrainNoiseOptions { Seed = 42 });
        TerrainHeightmap second = TerrainNoiseGenerator.Generate(descriptor, new TerrainNoiseOptions { Seed = 42 });

        first.Heights.SequenceEqual(second.Heights).Should().BeTrue();
    }

    [Fact]
    public void Noise_DiffersForDifferentSeeds()
    {
        TerrainDescriptor descriptor = CreateDescriptor(resolution: 33);
        TerrainHeightmap first = TerrainNoiseGenerator.Generate(descriptor, new TerrainNoiseOptions { Seed = 1 });
        TerrainHeightmap second = TerrainNoiseGenerator.Generate(descriptor, new TerrainNoiseOptions { Seed = 2 });

        var differing = 0;
        for (var index = 0; index < first.Length; index++)
        {
            if (first.Heights[index] != second.Heights[index])
            {
                differing++;
            }
        }

        differing.Should().BeGreaterThan(first.Length / 2);
    }

    [Fact]
    public void Noise_StaysWithinUnitRange()
    {
        TerrainHeightmap heightmap = TerrainNoiseGenerator.Generate(
            CreateDescriptor(),
            new TerrainNoiseOptions { Seed = 1337, Octaves = 6 });

        foreach (float value in heightmap.Heights)
        {
            value.Should().BeGreaterThanOrEqualTo(0f).And.BeLessThanOrEqualTo(1f);
        }
    }
}
