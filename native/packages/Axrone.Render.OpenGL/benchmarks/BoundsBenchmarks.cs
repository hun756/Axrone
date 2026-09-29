using Axrone.Render.OpenGL.Mesh;

namespace Axrone.Render.OpenGL.Benchmarks;

/// <summary>
/// Benchmarks Bounds3D (AABB) operations: construction from point clouds,
/// incremental expansion, point containment tests, and box intersection tests.
/// </summary>
[MemoryDiagnoser]
public class BoundsBenchmarks
{
    private Vector3[] _points = null!;
    private Bounds3D _bounds;
    private Bounds3D _otherBounds;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Frac(float value) => value - MathF.Floor(value);

    [GlobalSetup]
    public void Setup()
    {
        // 1000 deterministic points in a unit cube (golden-ratio multiples:
        // well-distributed, reproducible across runtimes, no RNG dependency).
        _points = new Vector3[1000];

        for (int i = 0; i < _points.Length; i++)
        {
            _points[i] = new Vector3(
                Frac(i * 0.6180339f),
                Frac(i * 0.3819660f),
                Frac(i * 0.2360680f));
        }

        _bounds = Bounds3D.FromPoints(_points);
        _otherBounds = new Bounds3D(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1.5f, 1.5f, 1.5f));
    }

    /// <summary>
    /// Computes a bounding box enclosing 1000 points in a single pass.
    /// </summary>
    [Benchmark(Baseline = true)]
    public Bounds3D FromPoints_1000()
    {
        return Bounds3D.FromPoints(_points);
    }

    /// <summary>
    /// Expands an empty bounds point-by-point for 1000 points.
    /// Tests incremental expansion cost (creates a new struct per point).
    /// </summary>
    [Benchmark]
    public Bounds3D Expand_1000()
    {
        var bounds = Bounds3D.Empty;

        for (int i = 0; i < _points.Length; i++)
        {
            bounds = bounds.Expand(_points[i]);
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

        for (int i = 0; i < _points.Length; i++)
        {
            if (_bounds.Contains(_points[i]))
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
            if (_bounds.Intersects(_otherBounds))
            {
                intersections++;
            }
        }

        return intersections;
    }
}
