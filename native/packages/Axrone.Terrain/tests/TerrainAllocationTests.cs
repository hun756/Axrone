using System.Numerics;
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

    [Fact]
    public void Raycast_DoesNotAllocateInSteadyState()
    {
        TerrainDescriptor descriptor = new(64f, 64f, 20f, 33);
        TerrainHeightmap heightmap = TerrainHeightmap.CreateFlat(33, 0.5f);
        var ray = new TerrainRay(new Vector3(0f, 50f, 0f), new Vector3(0f, -1f, 0f));
        TerrainRaycaster.Raycast(heightmap, descriptor, ray);

        long before = GC.GetAllocatedBytesForCurrentThread();
        var hits = 0;
        for (var index = 0; index < 100; index++)
        {
            if (TerrainRaycaster.TryRaycast(heightmap, descriptor, ray, 1000f, out _))
            {
                hits++;
            }
        }

        long after = GC.GetAllocatedBytesForCurrentThread();

        hits.Should().Be(100);
        (after - before).Should().Be(0);
    }

    [Fact]
    public void BrushRaiseStamp_DoesNotAllocateInSteadyState()
    {
        TerrainDescriptor descriptor = new(64f, 64f, 20f, 33);
        var heights = new float[33 * 33];
        var brush = new TerrainBrushOptions { Radius = 8f, Strength = 0.5f, Kind = TerrainBrushKind.Raise };
        TerrainSculptor.ApplyBrushStamp(heights, descriptor, brush, 0f, 0f);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
        {
            TerrainSculptor.ApplyBrushStamp(heights, descriptor, brush, 0f, 0f);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();

        (after - before).Should().Be(0);
    }
}
