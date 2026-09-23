namespace Axrone.Tween;

using System.Numerics;

/// <summary>
/// Immutable tween description: packed start/end lanes plus playback configuration.
/// </summary>
/// <param name="Start">Start lanes.</param>
/// <param name="End">End lanes.</param>
/// <param name="ChannelCount">Active lanes, 1..4.</param>
/// <param name="Duration">Playthrough length; zero completes instantly.</param>
/// <param name="Delay">Hold before the first playthrough.</param>
/// <param name="Easing">Curve selector.</param>
/// <param name="CustomEasing">Curve function when <see cref="Easing"/> is Custom.</param>
/// <param name="Mode">Loop behavior.</param>
/// <param name="LoopCount">Total playthroughs (1 plays once); negative means infinite.</param>
/// <param name="TimeScale">Playback rate multiplier; must be positive.</param>
/// <param name="OnUpdateFloat">Per-tick callback for single-lane tweens.</param>
/// <param name="OnUpdateVector2">Per-tick callback for two-lane tweens.</param>
/// <param name="OnUpdateVector3">Per-tick callback for three-lane tweens.</param>
/// <param name="OnUpdateVector4">Per-tick callback for four-lane tweens.</param>
/// <param name="OnStart">Fires once when playback starts.</param>
/// <param name="OnComplete">Fires once when playback finishes.</param>
/// <param name="OnStepComplete">Fires at the end of every playthrough, including the last.</param>
/// <param name="OnKill">Fires when cancelled before completion.</param>
/// <param name="RepeatDelay">Gap inserted between playthroughs; zero plays them back-to-back.</param>
public readonly record struct TweenSpec(
    Vector128<float> Start,
    Vector128<float> End,
    byte ChannelCount,
    DurationNs Duration,
    DurationNs Delay,
    EasingKind Easing,
    Func<float, float>? CustomEasing,
    PlaybackMode Mode,
    int LoopCount,
    float TimeScale,
    Action<float>? OnUpdateFloat,
    Action<Vector2>? OnUpdateVector2,
    Action<Vector3>? OnUpdateVector3,
    Action<Vector4>? OnUpdateVector4,
    Action? OnStart,
    Action? OnComplete,
    Action? OnStepComplete,
    Action? OnKill,
    DurationNs RepeatDelay)
{
    /// <summary>
    /// Wall-clock length of the full schedule: initial delay, every playthrough, and the
    /// gaps between them. Null for infinite loops, which never end by construction.
    /// </summary>
    public DurationNs? TotalDuration
    {
        get
        {
            if (LoopCount < 0)
            {
                return null;
            }

            long playthroughs = Math.Max(LoopCount, 1);
            long gaps = playthroughs - 1;
            return new DurationNs(unchecked(
                Delay.Value + (Duration.Value * playthroughs) + (RepeatDelay.Value * gaps)));
        }
    }
}
