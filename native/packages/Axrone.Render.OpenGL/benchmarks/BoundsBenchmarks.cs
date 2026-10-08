using Axrone.Geometry;
using Axrone.Numeric;
using Axrone.Render.OpenGL.Mesh;

namespace Axrone.Render.OpenGL.Benchmarks;

/// <summary>
/// Benchmarks AABB operations: construction from point clouds,
/// incremental expansion, point containment tests, and box intersection tests.
/// </summary>
[MemoryDiagnoser]
public class BoundsBenchmarks
{
    private Vec3[] _vecPoints = null!;
    private Aabb3D _bounds;
    private Aabb3D _otherBounds;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Frac(float value) => value - MathF.Floor(value);

    [GlobalSetup]
    public void Setup()
    {
        // 1000 deterministic points in a unit cube (golden-ratio multiples:
        // well-distributed, reproducible across runtimes, no RNG dependency).
        _vecPoints = new Vec3[1000];

        for (int i = 0; i < _vecPoints.Length; i++)
        {
            _vecPoints[i] = new Vec3(
                Frac(i * 0.6180339f),
                Frac(i * 0.3819660f),
                Frac(i * 0.2360680f));
        }

        _bounds = Aabb3D.CreateFromPoints(_vecPoints);
        _otherBounds = new Aabb3D(new Vec3(0.5f, 0.5f, 0.5f), new Vec3(1.5f, 1.5f, 1.5f));
    }

    /// <summary>
    /// Computes a bounding box enclosing 1000 points in a single pass.
    /// </summary>
    [Benchmark(Baseline = true)]
    public Aabb3D FromPoints_1000()
    {
        return Aabb3D.CreateFromPoints(_vecPoints);
    }

    /// <summary>
    /// Expands an empty bounds point-by-point for 1000 points.
    /// Tests incremental expansion cost (creates a new struct per point).
    /// </summary>
    [Benchmark]
    public Aabb3D Expand_1000()
    {
        var bounds = Aabb3D.Empty;

        for (int i = 0; i < _vecPoints.Length; i++)
        {
            var point = new Aabb3D(_vecPoints[i], _vecPoints[i]);
            bounds = Aabb3D.CreateMerged(in bounds, in point);
        }

        return bounds;
    }

    /// <summary>
    /// Tests point containment for 1000 points against a fixed bounds.
    /// </summary>
    [Benchmark]
    public int Contains_Test()
    {
        int inside = 0;

        for (int i = 0; i < _vecPoints.Length; i++)
        {
            if (_bounds.Contains(_vecPoints[i]))
            {
                inside++;
            }
        }

        return inside;
    }

    /// <summary>
    /// Tests intersection between two overlapping bounds 1000 times.
    /// </summary>
    [Benchmark]
    public int Intersects_Test()
    {
        int intersections = 0;

        for (int i = 0; i < 1000; i++)
        {
            if (_bounds.Overlaps(in _otherBounds))
            {
                intersections++;
            }
        }

        return intersections;
    }
}
