namespace Axrone.Animation;

using System.Diagnostics.Metrics;

/// <summary>OpenTelemetry meter for the animation runtime (cold reporting path).</summary>
public static class AnimationTelemetry
{
    private static readonly Meter s_meter = new("Axrone.Animation", "1.0.0");
    private static readonly Counter<ulong> s_framesEvaluated = s_meter.CreateCounter<ulong>("animation.frames_evaluated");
    private static readonly Counter<ulong> s_clipsSampled = s_meter.CreateCounter<ulong>("animation.clips_sampled");
    private static readonly Counter<ulong> s_ikIterations = s_meter.CreateCounter<ulong>("animation.ik_iterations");
    private static readonly Histogram<double> s_evaluationLatencyMs = s_meter.CreateHistogram<double>("animation.eval_latency_ms");

    /// <summary>Records one composed frame.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordFrameEvaluated() => s_framesEvaluated.Add(1);

    /// <summary>Records one sampled clip.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordClipSampled() => s_clipsSampled.Add(1);

    /// <summary>Records IK iterations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordIkIterations(int iterations) => s_ikIterations.Add((ulong)Math.Max(0, iterations));

    /// <summary>Records evaluation latency.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordEvaluationLatency(double elapsedMs) => s_evaluationLatencyMs.Record(elapsedMs);
}

/// <summary>Lock-free operational counters on isolated cache lines.</summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct AnimationDiagnosticCounters : IEquatable<AnimationDiagnosticCounters>
{
    [FieldOffset(0)]
    private long _framesEvaluated;

    [FieldOffset(64)]
    private long _clipsSampled;

    /// <summary>Records one composed frame.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementFrame() => Interlocked.Increment(ref _framesEvaluated);

    /// <summary>Records one sampled clip.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementClip() => Interlocked.Increment(ref _clipsSampled);

    /// <summary>Frames evaluated.</summary>
    public long FramesEvaluated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _framesEvaluated);
    }

    /// <summary>Clips sampled.</summary>
    public long ClipsSampled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _clipsSampled);
    }

    /// <summary>Captures an atomic snapshot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AnimationMetricsSnapshot CaptureSnapshot() => new(FramesEvaluated, ClipsSampled, IsOperational: true);

    /// <inheritdoc/>
    public bool Equals(AnimationDiagnosticCounters other) =>
        FramesEvaluated == other.FramesEvaluated && ClipsSampled == other.ClipsSampled;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is AnimationDiagnosticCounters other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(FramesEvaluated, ClipsSampled);

    /// <inheritdoc/>
    public static bool operator ==(AnimationDiagnosticCounters left, AnimationDiagnosticCounters right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(AnimationDiagnosticCounters left, AnimationDiagnosticCounters right) => !left.Equals(right);
}

/// <summary>Point-in-time animation metrics.</summary>
public readonly record struct AnimationMetricsSnapshot(long TotalFrames, long TotalClips, bool IsOperational);

/// <summary>Health probe result.</summary>
public readonly record struct AnimationHealthReport(bool IsHealthy, string StatusMessage, AnimationMetricsSnapshot Snapshot);
