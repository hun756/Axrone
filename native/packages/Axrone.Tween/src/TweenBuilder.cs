namespace Axrone.Tween;

using System.Numerics;
using Axrone.Utility.Builders;

/// <summary>
/// Fluent builder for <see cref="TweenSpec"/> with from/to lane accessors.
/// </summary>
/// <remarks>
/// <c>By</c> without a preceding <c>From</c> starts at zero. Both lanes must be set (the mask
/// reports which side is missing) and their arities must match — a float start with a
/// vector end fails validation instead of silently reinterpreting lanes.
/// </remarks>
public sealed class TweenBuilder : AggregateBuilder<TweenBuilder, TweenSpecState, TweenSpec>
{
    private static class Slots
    {
        public const int From = 0;
        public const int To = 1;

        public static PropertyBitmask64 Required => new((1UL << From) | (1UL << To));
    }

    public TweenBuilder()
    {
        State = TweenSpecState.Default;
    }

    protected override TweenBuilder Self => this;

    protected override PropertyBitmask64 RequiredMask => Slots.Required;

    /// <summary>Sets the start value (single lane).</summary>
    public TweenBuilder From(float start)
    {
        State.Start = Vector128.Create(start, 0f, 0f, 0f);
        State.FromArity = 1;
        MarkSet(Slots.From);
        return this;
    }

    /// <summary>Sets the end value (single lane).</summary>
    public TweenBuilder To(float end)
    {
        State.End = Vector128.Create(end, 0f, 0f, 0f);
        State.ToArity = 1;
        State.ChannelCount = 1;
        MarkSet(Slots.To);
        return this;
    }

    /// <summary>Sets the end relative to the start (single lane).</summary>
    public TweenBuilder By(float delta)
    {
        State.End = Vector128.Create(State.Start.GetElement(0) + delta, 0f, 0f, 0f);
        State.ToArity = 1;
        State.ChannelCount = 1;
        MarkSet(Slots.To);
        return this;
    }

    /// <summary>Sets the start value (two lanes).</summary>
    public TweenBuilder From(Vector2 start)
    {
        State.Start = Vector128.Create(start.X, start.Y, 0f, 0f);
        State.FromArity = 2;
        MarkSet(Slots.From);
        return this;
    }

    /// <summary>Sets the end value (two lanes).</summary>
    public TweenBuilder To(Vector2 end)
    {
        State.End = Vector128.Create(end.X, end.Y, 0f, 0f);
        State.ToArity = 2;
        State.ChannelCount = 2;
        MarkSet(Slots.To);
        return this;
    }

    /// <summary>Sets the end relative to the start (two lanes).</summary>
    public TweenBuilder By(Vector2 delta)
    {
        State.End = Vector128.Create(
            State.Start.GetElement(0) + delta.X,
            State.Start.GetElement(1) + delta.Y,
            0f,
            0f);
        State.ToArity = 2;
        State.ChannelCount = 2;
        MarkSet(Slots.To);
        return this;
    }

    /// <summary>Sets the start value (three lanes).</summary>
    public TweenBuilder From(Vector3 start)
    {
        State.Start = Vector128.Create(start.X, start.Y, start.Z, 0f);
        State.FromArity = 3;
        MarkSet(Slots.From);
        return this;
    }

    /// <summary>Sets the end value (three lanes).</summary>
    public TweenBuilder To(Vector3 end)
    {
        State.End = Vector128.Create(end.X, end.Y, end.Z, 0f);
        State.ToArity = 3;
        State.ChannelCount = 3;
        MarkSet(Slots.To);
        return this;
    }

    /// <summary>Sets the end relative to the start (three lanes).</summary>
    public TweenBuilder By(Vector3 delta)
    {
        State.End = Vector128.Create(
            State.Start.GetElement(0) + delta.X,
            State.Start.GetElement(1) + delta.Y,
            State.Start.GetElement(2) + delta.Z,
            0f);
        State.ToArity = 3;
        State.ChannelCount = 3;
        MarkSet(Slots.To);
        return this;
    }

    /// <summary>Sets the start value (four lanes).</summary>
    public TweenBuilder From(Vector4 start)
    {
        State.Start = start.AsVector128();
        State.FromArity = 4;
        MarkSet(Slots.From);
        return this;
    }

    /// <summary>Sets the end value (four lanes).</summary>
    public TweenBuilder To(Vector4 end)
    {
        State.End = end.AsVector128();
        State.ToArity = 4;
        State.ChannelCount = 4;
        MarkSet(Slots.To);
        return this;
    }

    /// <summary>Sets the end relative to the start (four lanes).</summary>
    public TweenBuilder By(Vector4 delta)
    {
        State.End = Vector128.Add(State.Start, delta.AsVector128());
        State.ToArity = 4;
        State.ChannelCount = 4;
        MarkSet(Slots.To);
        return this;
    }

    /// <summary>Sets the playthrough length.</summary>
    public TweenBuilder Duration(DurationNs duration)
    {
        State.Duration = duration;
        return this;
    }

    /// <summary>Sets the playthrough length in seconds.</summary>
    public TweenBuilder DurationSeconds(float seconds)
    {
        State.Duration = DurationNs.FromSeconds(seconds);
        return this;
    }

    /// <summary>Sets the hold before the first playthrough.</summary>
    public TweenBuilder Delay(DurationNs delay)
    {
        State.Delay = delay;
        return this;
    }

    /// <summary>Sets the hold before the first playthrough in seconds.</summary>
    public TweenBuilder DelaySeconds(float seconds)
    {
        State.Delay = DurationNs.FromSeconds(seconds);
        return this;
    }

    /// <summary>Selects a built-in curve.</summary>
    public TweenBuilder Ease(EasingKind easing)
    {
        State.Easing = easing;
        return this;
    }

    /// <summary>Selects a custom curve function.</summary>
    public TweenBuilder Ease(Func<float, float> easing)
    {
        State.Easing = EasingKind.Custom;
        State.CustomEasing = easing;
        return this;
    }

    /// <summary>Sets loop behavior.</summary>
    public TweenBuilder Mode(PlaybackMode mode)
    {
        State.Mode = mode;
        return this;
    }

    /// <summary>Sets total playthroughs (1 plays once); negative means infinite.</summary>
    public TweenBuilder Loops(int count)
    {
        State.LoopCount = count;
        return this;
    }

    /// <summary>Sets the playback rate multiplier; must be positive.</summary>
    public TweenBuilder TimeScale(float scale)
    {
        State.TimeScale = scale;
        return this;
    }

    /// <summary>Per-tick callback for single-lane tweens.</summary>
    public TweenBuilder OnUpdate(Action<float> callback)
    {
        State.OnUpdateFloat = callback;
        return this;
    }

    /// <summary>Per-tick callback for two-lane tweens.</summary>
    public TweenBuilder OnUpdate(Action<Vector2> callback)
    {
        State.OnUpdateVector2 = callback;
        return this;
    }

    /// <summary>Per-tick callback for three-lane tweens.</summary>
    public TweenBuilder OnUpdate(Action<Vector3> callback)
    {
        State.OnUpdateVector3 = callback;
        return this;
    }

    /// <summary>Per-tick callback for four-lane tweens.</summary>
    public TweenBuilder OnUpdate(Action<Vector4> callback)
    {
        State.OnUpdateVector4 = callback;
        return this;
    }

    /// <summary>Fires once when playback starts.</summary>
    public TweenBuilder OnStart(Action callback)
    {
        State.OnStart = callback;
        return this;
    }

    /// <summary>Fires once when playback finishes.</summary>
    public TweenBuilder OnComplete(Action callback)
    {
        State.OnComplete = callback;
        return this;
    }

    /// <summary>Fires at the end of every playthrough, including the last.</summary>
    public TweenBuilder OnStepComplete(Action callback)
    {
        State.OnStepComplete = callback;
        return this;
    }

    /// <summary>Fires when cancelled before completion.</summary>
    public TweenBuilder OnKill(Action callback)
    {
        State.OnKill = callback;
        return this;
    }

    /// <inheritdoc/>
    public override void Reset()
    {
        base.Reset();
        State = TweenSpecState.Default;
    }

    /// <inheritdoc/>
    public override TweenBuilder Fork() => CopyTo(new TweenBuilder());
}
