namespace Axrone.Raycast;

using Axrone.Geometry;
using Axrone.Numeric;

/// <summary>Line sweep over a <see cref="RaycastEngine3D"/>.</summary>
/// <param name="Start">Sweep start position.</param>
/// <param name="End">Sweep end position.</param>
/// <param name="LayerMask">Layers the sweep accepts.</param>
/// <param name="MaxIterations">Conservative-advancement sub-intervals.</param>
/// <param name="Tolerance">Bisection tolerance in normalized time.</param>
public readonly record struct RaycastSweepQuery3D(
    Vec3 Start,
    Vec3 End,
    uint LayerMask = uint.MaxValue,
    int MaxIterations = 16,
    float Tolerance = 1e-3f);

/// <summary>First contact of a sweep, in normalized time.</summary>
/// <param name="Hit">Whether the sweep reached a target.</param>
/// <param name="Time">Normalized time of impact in [0, 1].</param>
/// <param name="Normal">Unit surface normal at the contact.</param>
/// <param name="WitnessCast">Cast reference point at the moment of impact.</param>
/// <param name="WitnessTarget">Contact point on the target.</param>
public readonly record struct TimeOfImpact3D(
    bool Hit,
    float Time,
    Vec3 Normal,
    Vec3 WitnessCast,
    Vec3 WitnessTarget);

/// <summary>
/// Continuous queries over a dynamic 3D raycast world: conservative advancement for
/// point sweeps, a direct sphere/box sweep, moving-origin sampling and predictive aim.
/// Every method is allocation-free and uses the engine's concurrent read path.
/// </summary>
public sealed class ContinuousRaycaster3D
{
    private const float Epsilon = 1e-6f;
    private const int BisectionIterations = 12;

    private readonly RaycastEngine3D _engine;

    /// <summary>Creates a continuous query facade over an engine.</summary>
    /// <param name="engine">Engine that receives the sweep queries.</param>
    public ContinuousRaycaster3D(RaycastEngine3D engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <summary>
    /// Sweeps a point from start to end with conservative advancement: the path is cut
    /// into fixed sub-intervals, each is probed with a forward raycast, and the first
    /// contact is tightened by bisection. Tunnelling cannot occur while the step is
    /// shorter than the thinnest target.
    /// </summary>
    /// <param name="query">Sweep descriptor.</param>
    /// <returns>The first impact, or a miss value.</returns>
    public TimeOfImpact3D SweepTest(in RaycastSweepQuery3D query)
    {
        Vec3 displacement = query.End - query.Start;
        float distance = displacement.Length();
        if (distance < Epsilon)
        {
            return default;
        }

        Vec3 direction = displacement / distance;
        int iterations = Math.Max(1, query.MaxIterations);
        float tolerance = MathF.Max(query.Tolerance, Epsilon);
        float step = distance / iterations;

        float previousTime = 0f;
        Vec3 previousOrigin = query.Start;
        for (var index = 0; index < iterations; index++)
        {
            var ray = new Ray3D(previousOrigin, direction);
            var probe = new RaycastQuery3D(ray, step + Epsilon, query.LayerMask);
            if (_engine.Raycast(in probe, out RaycastHit3D hit) && hit.Distance <= step + Epsilon)
            {
                float low = previousTime;
                float high = previousTime + (hit.Distance / distance);
                for (var iteration = 0; iteration < BisectionIterations && high - low > tolerance; iteration++)
                {
                    float middle = (low + high) * 0.5f;
                    Vec3 middleOrigin = query.Start + (displacement * middle);
                    var middleRay = new Ray3D(middleOrigin, direction);
                    var middleProbe = new RaycastQuery3D(
                        middleRay,
                        (distance * (1f - middle)) + Epsilon,
                        query.LayerMask);
                    if (_engine.Raycast(in middleProbe, out _))
                    {
                        low = middle;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                Vec3 impactOrigin = query.Start + (displacement * high);
                var finalRay = new Ray3D(impactOrigin, direction);
                var finalProbe = new RaycastQuery3D(
                    finalRay,
                    (Epsilon * 2f) + step,
                    query.LayerMask);
                Vec3 normal = hit.Normal;
                Vec3 targetPoint = hit.Point;
                if (_engine.Raycast(in finalProbe, out RaycastHit3D finalHit))
                {
                    normal = finalHit.Normal;
                    targetPoint = finalHit.Point;
                }

                return new TimeOfImpact3D(true, high, normal, impactOrigin, targetPoint);
            }

            previousTime = (index + 1f) / iterations;
            previousOrigin = query.Start + (displacement * previousTime);
        }

        return default;
    }

    /// <summary>Sweeps a sphere along the query path with a single exact Minkowski cast.</summary>
    /// <param name="query">Sweep descriptor.</param>
    /// <param name="radius">Sphere radius.</param>
    /// <returns>The first impact, or a miss value.</returns>
    public TimeOfImpact3D SweepSphere(in RaycastSweepQuery3D query, float radius)
    {
        Vec3 displacement = query.End - query.Start;
        float distance = displacement.Length();
        if (distance < Epsilon)
        {
            return default;
        }

        var ray = new Ray3D(query.Start, displacement / distance);
        RaycastShape3D volume = RaycastShape3D.CreateSphere(query.Start, radius);
        if (!_engine.ShapeCast(in ray, in volume, distance, out RaycastHit3D hit, query.LayerMask))
        {
            return default;
        }

        float time = hit.Distance / distance;
        Vec3 witness = query.Start + (ray.Direction * hit.Distance);
        return new TimeOfImpact3D(true, time, hit.Normal, witness, hit.Point);
    }

    /// <summary>
    /// Sweeps an axis-aligned box along the query path. The cast is exact against
    /// axis-aligned targets and conservative against oriented targets.
    /// </summary>
    /// <param name="query">Sweep descriptor.</param>
    /// <param name="halfExtents">Box half extents.</param>
    /// <returns>The first impact, or a miss value.</returns>
    public TimeOfImpact3D SweepBox(in RaycastSweepQuery3D query, Vec3 halfExtents)
    {
        Vec3 displacement = query.End - query.Start;
        float distance = displacement.Length();
        if (distance < Epsilon)
        {
            return default;
        }

        var ray = new Ray3D(query.Start, displacement / distance);
        RaycastShape3D volume = RaycastShape3D.CreateAabb(
            new Aabb3D(query.Start - halfExtents, query.Start + halfExtents));
        if (!_engine.ShapeCast(in ray, in volume, distance, out RaycastHit3D hit, query.LayerMask))
        {
            return default;
        }

        float time = hit.Distance / distance;
        Vec3 witness = query.Start + (ray.Direction * hit.Distance);
        return new TimeOfImpact3D(true, time, hit.Normal, witness, hit.Point);
    }

    /// <summary>
    /// Casts from a moving origin: the origin is sampled between the two endpoints and
    /// the nearest normalized contact wins.
    /// </summary>
    /// <param name="startOrigin">Origin at the start of the motion.</param>
    /// <param name="endOrigin">Origin at the end of the motion.</param>
    /// <param name="direction">Fixed cast direction.</param>
    /// <param name="maxDistance">Distance limit per sample.</param>
    /// <param name="hit">Nearest contact when found.</param>
    /// <param name="layerMask">Layers the cast accepts.</param>
    /// <param name="samples">Number of sub-intervals; at least one.</param>
    /// <returns><see langword="true"/> when a sample reached a shape.</returns>
    public bool ContinuousCast(
        Vec3 startOrigin,
        Vec3 endOrigin,
        Vec3 direction,
        float maxDistance,
        out RaycastHit3D hit,
        uint layerMask = uint.MaxValue,
        int samples = 10)
    {
        int count = Math.Max(1, samples);
        var found = false;
        float bestFraction = float.PositiveInfinity;
        RaycastHit3D best = default;
        for (var index = 0; index <= count; index++)
        {
            float time = (float)index / count;
            Vec3 origin = (startOrigin * (1f - time)) + (endOrigin * time);
            var ray = new Ray3D(origin, direction);
            var query = new RaycastQuery3D(ray, maxDistance, layerMask);
            if (_engine.Raycast(in query, out RaycastHit3D candidate))
            {
                float fraction = time + ((1f - time) * candidate.Fraction);
                if (fraction < bestFraction)
                {
                    bestFraction = fraction;
                    best = candidate;
                    found = true;
                }
            }
        }

        hit = best;
        return found;
    }

    /// <summary>
    /// Aims at a moving target: at each step the ray runs from the predicted origin to
    /// the predicted target, and the first blocking contact wins.
    /// </summary>
    /// <param name="origin">Observer position.</param>
    /// <param name="velocity">Observer velocity.</param>
    /// <param name="targetPosition">Target position.</param>
    /// <param name="targetVelocity">Target velocity.</param>
    /// <param name="maxTime">Prediction horizon in seconds.</param>
    /// <param name="timeStep">Sampling step in seconds.</param>
    /// <param name="hit">First blocking contact when found.</param>
    /// <param name="layerMask">Layers the aim accepts.</param>
    /// <returns><see langword="true"/> when a sample reached a shape.</returns>
    public bool PredictiveRaycast(
        Vec3 origin,
        Vec3 velocity,
        Vec3 targetPosition,
        Vec3 targetVelocity,
        float maxTime,
        float timeStep,
        out RaycastHit3D hit,
        uint layerMask = uint.MaxValue)
    {
        if (!float.IsFinite(maxTime) || maxTime <= 0f ||
            !float.IsFinite(timeStep) || timeStep <= 0f)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(timeStep));
        }

        int steps = (int)MathF.Ceiling(maxTime / timeStep);
        for (var index = 0; index < steps; index++)
        {
            float time = index * timeStep;
            Vec3 currentOrigin = origin + (velocity * time);
            Vec3 currentTarget = targetPosition + (targetVelocity * time);
            Vec3 delta = currentTarget - currentOrigin;
            float distance = delta.Length();
            if (distance < Epsilon)
            {
                continue;
            }

            var ray = new Ray3D(currentOrigin, delta);
            var query = new RaycastQuery3D(ray, distance, layerMask);
            if (_engine.Raycast(in query, out RaycastHit3D candidate))
            {
                hit = candidate;
                return true;
            }
        }

        hit = default;
        return false;
    }
}
