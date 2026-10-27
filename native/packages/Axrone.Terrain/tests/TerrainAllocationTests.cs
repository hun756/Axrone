using Axrone.Terrain;
using FluentAssertions;
using Xunit;

namespace Axrone.Terrain.Tests;

public sealed class TerrainAllocationTests
{
    [Fact]
    public void SampleHeight_DoesNotAllocateInSteadyState()
    {
        TerrainHeightmap heightmap = TerrainHeightmap.CreateFlat(33, 0.5f);
        heightmap.SampleHeight(0.25f, 0.75f);

        long before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0f;
        for (var index = 0; index < 1_000; index++)
        {
            sum += heightmap.SampleHeight(index * 0.001f, 0.5f);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();

        sum.Should().BeGreaterThan(0f);
        (after - before).Should().Be(0);
    }
}
