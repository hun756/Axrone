using Axrone.Numeric;
using FluentAssertions;
using Xunit;

namespace Axrone.Numeric.Tests;

public sealed class PerlinNoiseTests
{
    [Fact]
    public void LatticePointsAreZero()
    {
        var noise = new PerlinNoise(seed: 42);

        noise.Sample1D(3f).Should().BeApproximately(0f, 1e-6f);
        noise.Sample2D(0f, 0f).Should().BeApproximately(0f, 1e-6f);
        noise.Sample2D(-3f, 7f).Should().BeApproximately(0f, 1e-6f);
        noise.Sample3D(2f, -4f, 9f).Should().BeApproximately(0f, 1e-6f);
    }

    [Fact]
    public void SamplingIsDeterministicPerSeed()
    {
        var first = new PerlinNoise(seed: 7, octaves: 3);
        var second = new PerlinNoise(seed: 7, octaves: 3);
        var other = new PerlinNoise(seed: 8, octaves: 3);

        for (var index = 0; index < 100; index++)
        {
            float x = index * 0.37f;
            first.Sample2D(x, -x).Should().Be(second.Sample2D(x, -x));
            first.Fbm2D(x, -x).Should().Be(second.Fbm2D(x, -x));
        }

        first.Sample2D(1.25f, 2.5f).Should().NotBe(other.Sample2D(1.25f, 2.5f));
    }

    [Fact]
    public void GradientNoiseStaysInsideUnitRange()
    {
        var noise = new PerlinNoise(seed: 1234);

        for (var y = -16; y <= 16; y++)
        {
            for (var x = -16; x <= 16; x++)
            {
                noise.Sample2D(x * 0.31f, y * 0.27f).Should().BeInRange(-1.01f, 1.01f);
                noise.Sample3D(x * 0.31f, y * 0.27f, (x + y) * 0.13f).Should().BeInRange(-1.01f, 1.01f);
            }
        }
    }

    [Fact]
    public void FbmSumsTheConfiguredOctaves()
    {
        var noise = new PerlinNoise(seed: 5, octaves: 4, persistence: 0.5f, lacunarity: 2f);

        float value = noise.Fbm2D(3.7f, -2.4f);

        float.IsFinite(value).Should().BeTrue();
        value.Should().NotBe(noise.Sample2D(3.7f, -2.4f));
    }

    [Theory]
    [InlineData(0, 1f, 0.5f, 2f)]
    [InlineData(9, 1f, 0.5f, 2f)]
    [InlineData(1, 0f, 0.5f, 2f)]
    [InlineData(1, 1f, 0f, 2f)]
    [InlineData(1, 1f, 1.1f, 2f)]
    [InlineData(1, 1f, 0.5f, 0.5f)]
    public void InvalidOptionsAreRejected(int octaves, float frequency, float persistence, float lacunarity)
    {
        FluentActions.Invoking(() => new PerlinNoise(1, octaves, frequency, persistence, lacunarity))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SamplingDoesNotAllocateInSteadyState()
    {
        var noise = new PerlinNoise(seed: 99, octaves: 4);
        noise.Sample2D(1f, 2f);
        noise.Fbm2D(1f, 2f);

        long before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0f;
        for (var index = 0; index < 1_000; index++)
        {
            sum += noise.Sample2D(index * 0.01f, 0.5f);
            sum += noise.Fbm2D(index * 0.01f, 0.5f);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();

        sum.Should().NotBe(0f);
        (after - before).Should().Be(0);
    }
}
