namespace Axrone.Tween;

using System.Numerics;

/// <summary>
/// Ready-made impact presets — punch and shake — expressed as single core tweens.
/// </summary>
/// <remarks>
/// Both presets ride a custom curve from zero, so one scheduled slot does the whole
/// effect: per-play execution allocates nothing beyond what any builder-produced spec
/// needs (the spec and its setup-time closure). Punch starts and ends at rest with a
/// decaying oscillation between; shake is a seeded multi-sine noise that settles to
/// rest. The same seed always produces the same stream, which keeps golden tests and
/// replays deterministic.
/// </remarks>
public static class TweenPresets
{
    private const float TwoPi = 2.0f * MathF.PI;
    private const float GoldenRatio = 1.6180339887f;

    /// <summary>
    /// Builds a punch: rest, decaying oscillation peaking near <paramref name="strength"/>,
    /// rest. The curve never exceeds unit magnitude, so the applied peak never exceeds
    /// <paramref name="strength"/>.
    /// </summary>
    public static TweenSpec Punch(float durationSeconds, float strength, int vibrato = 10, float elasticity = 1.0f)
    {
        Validate(durationSeconds, vibrato);
        if (!float.IsFinite(strength))
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(strength));
        }

        if (elasticity < 0.0f || !float.IsFinite(elasticity))
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(elasticity));
        }

        return new TweenBuilder()
            .From(0.0f)
            .To(strength)
            .DurationSeconds(durationSeconds)
            .Ease(t => PunchCurve(t, vibrato, elasticity))
            .Build();
    }

    /// <summary>Directional punch along a two-lane amplitude.</summary>
    public static TweenSpec Punch(float durationSeconds, Vector2 amplitude, int vibrato = 10, float elasticity = 1.0f) =>
        PunchDirectional(durationSeconds, amplitude.X, amplitude.Y, 0.0f, 0.0f, 2, vibrato, elasticity);

    /// <summary>Directional punch along a three-lane amplitude.</summary>
    public static TweenSpec Punch(float durationSeconds, Vector3 amplitude, int vibrato = 10, float elasticity = 1.0f) =>
        PunchDirectional(durationSeconds, amplitude.X, amplitude.Y, amplitude.Z, 0.0f, 3, vibrato, elasticity);

    /// <summary>Directional punch along a four-lane amplitude.</summary>
    public static TweenSpec Punch(float durationSeconds, Vector4 amplitude, int vibrato = 10, float elasticity = 1.0f) =>
        PunchDirectional(durationSeconds, amplitude.X, amplitude.Y, amplitude.Z, amplitude.W, 4, vibrato, elasticity);

    /// <summary>
    /// Builds a shake: seeded noise around rest with a linear settle envelope. The curve
    /// magnitude never exceeds one, so the applied offset never exceeds
    /// <paramref name="strength"/>. Identical seeds produce identical streams.
    /// </summary>
    public static TweenSpec Shake(float durationSeconds, float strength, int vibrato = 10, uint seed = 1)
    {
        Validate(durationSeconds, vibrato);
        if (!float.IsFinite(strength))
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(strength));
        }

        ShakePhases(seed, out float p1, out float p2, out float p3);
        return new TweenBuilder()
            .From(0.0f)
            .To(strength)
            .DurationSeconds(durationSeconds)
            .Ease(t => ShakeCurve(t, vibrato, p1, p2, p3))
            .Build();
    }

    /// <summary>Directional shake along a two-lane amplitude.</summary>
    public static TweenSpec Shake(float durationSeconds, Vector2 amplitude, int vibrato = 10, uint seed = 1) =>
        ShakeDirectional(durationSeconds, amplitude.X, amplitude.Y, 0.0f, 0.0f, 2, vibrato, seed);

    /// <summary>Directional shake along a three-lane amplitude.</summary>
    public static TweenSpec Shake(float durationSeconds, Vector3 amplitude, int vibrato = 10, uint seed = 1) =>
        ShakeDirectional(durationSeconds, amplitude.X, amplitude.Y, amplitude.Z, 0.0f, 3, vibrato, seed);

    /// <summary>Directional shake along a four-lane amplitude.</summary>
    public static TweenSpec Shake(float durationSeconds, Vector4 amplitude, int vibrato = 10, uint seed = 1) =>
        ShakeDirectional(durationSeconds, amplitude.X, amplitude.Y, amplitude.Z, amplitude.W, 4, vibrato, seed);

    private static TweenSpec PunchDirectional(
        float durationSeconds, float x, float y, float z, float w, int lanes, int vibrato, float elasticity)
    {
        Validate(durationSeconds, vibrato);
        if (elasticity < 0.0f || !float.IsFinite(elasticity))
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(elasticity));
        }

        var builder = new TweenBuilder().DurationSeconds(durationSeconds).Ease(t => PunchCurve(t, vibrato, elasticity));
        return lanes switch
        {
            2 => builder.From(Vector2.Zero).To(new Vector2(x, y)).Build(),
            3 => builder.From(Vector3.Zero).To(new Vector3(x, y, z)).Build(),
            _ => builder.From(Vector4.Zero).To(new Vector4(x, y, z, w)).Build(),
        };
    }

    private static TweenSpec ShakeDirectional(
        float durationSeconds, float x, float y, float z, float w, int lanes, int vibrato, uint seed)
    {
        Validate(durationSeconds, vibrato);
        ShakePhases(seed, out float p1, out float p2, out float p3);
        var builder = new TweenBuilder().DurationSeconds(durationSeconds).Ease(t => ShakeCurve(t, vibrato, p1, p2, p3));
        return lanes switch
        {
            2 => builder.From(Vector2.Zero).To(new Vector2(x, y)).Build(),
            3 => builder.From(Vector3.Zero).To(new Vector3(x, y, z)).Build(),
            _ => builder.From(Vector4.Zero).To(new Vector4(x, y, z, w)).Build(),
        };
    }

    private static void Validate(float durationSeconds, int vibrato)
    {
        if (durationSeconds <= 0.0f || !float.IsFinite(durationSeconds))
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(durationSeconds));
        }

        if (vibrato < 1)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(vibrato));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float PunchCurve(float t, int vibrato, float elasticity) =>
        MathF.Sin(TwoPi * vibrato * t) * MathF.Pow(1.0f - t, elasticity);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ShakeCurve(float t, int vibrato, float p1, float p2, float p3)
    {
        float envelope = 1.0f - t;
        float wave = MathF.Sin((TwoPi * vibrato * t) + p1)
            + MathF.Sin(((TwoPi * vibrato * GoldenRatio * t) + p2) * 0.5f) * 2.0f
            + MathF.Sin((TwoPi * vibrato * GoldenRatio * GoldenRatio * t) + p3);
        return (envelope * wave) / 4.0f;
    }

    private static void ShakePhases(uint seed, out float p1, out float p2, out float p3)
    {
        uint state = seed == 0 ? 0x9E3779B9u : seed;
        p1 = NextPhase(ref state);
        p2 = NextPhase(ref state);
        p3 = NextPhase(ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float NextPhase(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state / (float)uint.MaxValue) * TwoPi;
    }
}
