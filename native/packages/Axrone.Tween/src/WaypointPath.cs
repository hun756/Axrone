namespace Axrone.Tween;

using System.Numerics;

/// <summary>How a waypoint path blends between its control points.</summary>
public enum WaypointMode : byte
{
    /// <summary>Straight segments.</summary>
    Linear = 0,

    /// <summary>Holds each point until the next segment starts.</summary>
    Step = 1,

    /// <summary>Segments with smooth ends.</summary>
    Smoothstep = 2,

    /// <summary>Bernstein curve through all points (endpoints exact).</summary>
    Bezier = 3,

    /// <summary>Uniform spline passing through every point.</summary>
    CatmullRom = 4,
}

/// <summary>
/// Multi-stop path sampled by normalized time. Pairs with the core by driving a 0→1
/// tween whose update callback samples the path — loops, ping-pong, delays, and step
/// callbacks all keep working because the path is just a function of progress.
/// </summary>
/// <remarks>
/// Points are copied once at construction; sampling allocates nothing and touches no
/// locks. Bezier evaluates by De Casteljau (no <c>pow</c> per frame). Catmull-Rom treats
/// a path whose first and last points coincide as a closed loop and wraps its tangents.
/// </remarks>
public sealed class WaypointPath
{
    private readonly Vector4[] _points;

    /// <summary>Creates a path; at least two points are required.</summary>
    public WaypointPath(WaypointMode mode, params Vector4[] points)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Length < 2)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(points), "A path needs at least two points.");
        }

        Mode = mode;
        _points = (Vector4[])points.Clone();
    }

    /// <summary>Creates a path from two-lane points.</summary>
    public WaypointPath(WaypointMode mode, params Vector2[] points)
        : this(mode, Lift(Guard(points), static v => new Vector4(v, 0.0f, 0.0f)))
    {
    }

    /// <summary>Creates a path from three-lane points.</summary>
    public WaypointPath(WaypointMode mode, params Vector3[] points)
        : this(mode, Lift(Guard(points), static v => new Vector4(v, 0.0f)))
    {
    }

    /// <summary>Interpolation mode.</summary>
    public WaypointMode Mode { get; }

    /// <summary>Control point count.</summary>
    public int PointCount => _points.Length;

    /// <summary>Samples the path at normalized time, clamped to the unit interval.</summary>
    public Vector4 Sample(float t)
    {
        t = Math.Clamp(t, 0.0f, 1.0f);
        return Mode switch
        {
            WaypointMode.Step => SampleStep(t),
            WaypointMode.Smoothstep => SampleSegment(t, static u => u * u * (3.0f - (2.0f * u))),
            WaypointMode.Bezier => SampleBezier(t),
            WaypointMode.CatmullRom => SampleCatmullRom(t),
            _ => SampleSegment(t, static u => u),
        };
    }

    private static T[] Guard<T>(T[] source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source;
    }

    private static Vector4[] Lift<T>(T[] source, Func<T, Vector4> lift)
    {
        var lifted = new Vector4[source.Length];
        for (int i = 0; i < lifted.Length; i++)
        {
            lifted[i] = lift(source[i]);
        }

        return lifted;
    }

    private Vector4 SampleStep(float t)
    {
        int index = Math.Min((int)(t * _points.Length), _points.Length - 1);
        return _points[index];
    }

    private Vector4 SampleSegment(float t, Func<float, float> shape)
    {
        float scaled = t * (_points.Length - 1);
        int index = Math.Min((int)scaled, _points.Length - 2);
        float u = shape(scaled - index);
        return _points[index] + ((_points[index + 1] - _points[index]) * u);
    }

    private Vector4 SampleBezier(float t)
    {
        int n = _points.Length;
        Span<Vector4> level = stackalloc Vector4[n];
        _points.CopyTo(level);
        for (int depth = n - 1; depth > 0; depth--)
        {
            for (int i = 0; i < depth; i++)
            {
                level[i] += (level[i + 1] - level[i]) * t;
            }
        }

        return level[0];
    }

    private Vector4 SampleCatmullRom(float t)
    {
        int n = _points.Length;
        float scaled = t * (n - 1);
        int index = Math.Min((int)scaled, n - 2);
        float u = scaled - index;

        Vector4 p0 = Neighbor(index - 1);
        Vector4 p1 = _points[index];
        Vector4 p2 = _points[index + 1];
        Vector4 p3 = Neighbor(index + 2);

        return 0.5f * ((p1 * 2.0f)
            + ((p2 - p0) * u)
            + (((p0 * 2.0f) - (p1 * 5.0f) + (p2 * 4.0f) - p3) * u * u)
            + ((p3 - p0 + (p1 * 3.0f) - (p2 * 3.0f)) * u * u * u));
    }

    private Vector4 Neighbor(int index)
    {
        int n = _points.Length;
        if (_points[0].Equals(_points[n - 1]))
        {
            return _points[(index + n) % n];
        }

        return _points[Math.Clamp(index, 0, n - 1)];
    }
}
