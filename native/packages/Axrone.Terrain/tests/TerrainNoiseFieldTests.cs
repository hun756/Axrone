using Axrone.Terrain;
using FluentAssertions;
using Xunit;

namespace Axrone.Terrain.Tests;

public sealed class TerrainNoiseFieldTests
{
    [Fact]
    public void FieldSamplesMatchGeneratedHeightmap()
    {
        var descriptor = new TerrainDescriptor(100f, 80f, 30f, 33);
        var options = new TerrainNoiseOptions { Seed = 42, Octaves = 5 };
        var field = new TerrainNoiseField(options);
        TerrainHeightmap heightmap = TerrainNoiseGenerator.Generate(descriptor, options);

        for (var z = 0; z < 33; z++)
        {
            for (var x = 0; x < 33; x++)
            {
                float worldX = (x / 32f) * descriptor.Width;
                float worldZ = (z / 32f) * descriptor.Length;
                field.SampleHeight(worldX, worldZ).Should().BeApproximately(heightmap.GetHeight(x, z), 1e-5f);
            }
        }
    }

    [Fact]
    public void SampleRowMatchesIndividualSamples()
    {
        var field = new TerrainNoiseField(new TerrainNoiseOptions { Seed = 9 });
        var row = new float[16];

        field.SampleRow(-4f, 2.5f, 0.75f, row);

        for (var index = 0; index < row.Length; index++)
        {
            row[index].Should().BeApproximately(field.SampleHeight(-4f + (index * 0.75f), 2.5f), 1e-6f);
        }
    }

    [Fact]
    public void OffsetsTranslateTheField()
    {
        var baseOptions = new TerrainNoiseOptions { Seed = 7 };
        var shiftedOptions = new TerrainNoiseOptions { Seed = 7, OffsetX = 12.5f, OffsetZ = -3f };
        var baseField = new TerrainNoiseField(baseOptions);
        var shiftedField = new TerrainNoiseField(shiftedOptions);

        float expected = baseField.SampleHeight(2f + 12.5f, 4f - 3f);
        shiftedField.SampleHeight(2f, 4f).Should().BeApproximately(expected, 1e-6f);
    }

    [Fact]
    public void FieldIsDeterministic()
    {
        var first = new TerrainNoiseField(new TerrainNoiseOptions { Seed = 123 });
        var second = new TerrainNoiseField(new TerrainNoiseOptions { Seed = 123 });

        for (var index = 0; index < 100; index++)
        {
            float x = index * 0.37f;
            first.SampleHeight(x, -x).Should().Be(second.SampleHeight(x, -x));
        }
    }
}
