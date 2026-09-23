namespace Axrone.Tween;

using System.Numerics;
using Axrone.Utility.Builders;

/// <summary>
/// Mutable accumulator for <see cref="TweenBuilder"/>; materializes a <see cref="TweenSpec"/>.
/// </summary>
public struct TweenSpecState : IAggregateDefinition<TweenSpecState, TweenSpec>, IEquatable<TweenSpecState>
{
    /// <summary>Working defaults; start/end lanes still required.</summary>
    public static TweenSpecState Default => new()
    {
        Easing = EasingKind.Linear,
        Mode = PlaybackMode.Once,
        LoopCount = 1,
        TimeScale = 1.0f,
    };

    public Vector128<float> Start { get; set; }
    public Vector128<float> End { get; set; }
    public int FromArity { get; set; }
    public int ToArity { get; set; }
    public byte ChannelCount { get; set; }
    public DurationNs Duration { get; set; }
    public DurationNs Delay { get; set; }
    public EasingKind Easing { get; set; }
    public Func<float, float>? CustomEasing { get; set; }
    public PlaybackMode Mode { get; set; }
    public int LoopCount { get; set; }
    public float TimeScale { get; set; }
    public Action<float>? OnUpdateFloat { get; set; }
    public Action<Vector2>? OnUpdateVector2 { get; set; }
    public Action<Vector3>? OnUpdateVector3 { get; set; }
    public Action<Vector4>? OnUpdateVector4 { get; set; }
    public Action? OnStart { get; set; }
    public Action? OnComplete { get; set; }
    public Action? OnStepComplete { get; set; }
    public Action? OnKill { get; set; }
    public DurationNs RepeatDelay { get; set; }

    /// <inheritdoc/>
    public static TweenSpec Materialize(in TweenSpecState state) => new(
        state.Start,
        state.End,
        state.ChannelCount,
        state.Duration,
        state.Delay,
        state.Easing,
        state.CustomEasing,
        state.Mode,
        state.LoopCount,
        state.TimeScale,
        state.OnUpdateFloat,
        state.OnUpdateVector2,
        state.OnUpdateVector3,
        state.OnUpdateVector4,
        state.OnStart,
        state.OnComplete,
        state.OnStepComplete,
        state.OnKill,
        state.RepeatDelay);

    /// <inheritdoc/>
    public static bool TryValidate(in TweenSpecState state, out BuilderDiagnostic diagnostic)
    {
        if (state.FromArity != state.ToArity)
        {
            diagnostic = BuilderDiagnostic.Fail(
                BuilderStatusCode.ValidationFailed,
                $"From ({state.FromArity} lanes) and To ({state.ToArity} lanes) must match.");
            return false;
        }

        if (state.ChannelCount is < 1 or > 4)
        {
            diagnostic = BuilderDiagnostic.Fail(
                BuilderStatusCode.ValidationFailed,
                "Channel count must be between 1 and 4.");
            return false;
        }

        if (state.Easing == EasingKind.Custom && state.CustomEasing is null)
        {
            diagnostic = BuilderDiagnostic.Fail(
                BuilderStatusCode.ValidationFailed,
                "Custom easing requires an easing function.");
            return false;
        }

        if (state.TimeScale <= 0.0f || float.IsNaN(state.TimeScale))
        {
            diagnostic = BuilderDiagnostic.Fail(
                BuilderStatusCode.ValidationFailed,
                "TimeScale must be positive.");
            return false;
        }

        if (state.RepeatDelay.Value < 0)
        {
            diagnostic = BuilderDiagnostic.Fail(
                BuilderStatusCode.ValidationFailed,
                "RepeatDelay cannot be negative.");
            return false;
        }

        diagnostic = BuilderDiagnostic.Ok;
        return true;
    }

    /// <inheritdoc/>
    public bool Equals(TweenSpecState other) =>
        Start.Equals(other.Start) &&
        End.Equals(other.End) &&
        FromArity == other.FromArity &&
        ToArity == other.ToArity &&
        ChannelCount == other.ChannelCount &&
        Duration.Equals(other.Duration) &&
        Delay.Equals(other.Delay) &&
        Easing == other.Easing &&
        Mode == other.Mode &&
        LoopCount == other.LoopCount &&
        TimeScale.Equals(other.TimeScale);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TweenSpecState other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Start, End, ChannelCount, Duration, Easing, Mode, LoopCount, TimeScale);

    public static bool operator ==(TweenSpecState left, TweenSpecState right) => left.Equals(right);

    public static bool operator !=(TweenSpecState left, TweenSpecState right) => !left.Equals(right);
}
