namespace Axrone.Tween;

/// <summary>Dispatches normalized time to the selected curve.</summary>
public static class EasingEvaluator
{
    /// <summary>Evaluates the curve, clamping time to the unit interval.</summary>
    /// <remarks>Unknown kinds (including bare <see cref="EasingKind.Custom"/>) fall back to linear.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Evaluate(EasingKind kind, float t)
    {
        t = Math.Clamp(t, 0.0f, 1.0f);

        return kind switch
        {
            EasingKind.Linear => LinearCurve.Evaluate(t),
            EasingKind.EaseInQuad => EaseInQuadCurve.Evaluate(t),
            EasingKind.EaseOutQuad => EaseOutQuadCurve.Evaluate(t),
            EasingKind.EaseInOutQuad => EaseInOutQuadCurve.Evaluate(t),
            EasingKind.EaseInCubic => EaseInCubicCurve.Evaluate(t),
            EasingKind.EaseOutCubic => EaseOutCubicCurve.Evaluate(t),
            EasingKind.EaseInOutCubic => EaseInOutCubicCurve.Evaluate(t),
            EasingKind.EaseInQuart => EaseInQuartCurve.Evaluate(t),
            EasingKind.EaseOutQuart => EaseOutQuartCurve.Evaluate(t),
            EasingKind.EaseInOutQuart => EaseInOutQuartCurve.Evaluate(t),
            EasingKind.EaseInQuint => EaseInQuintCurve.Evaluate(t),
            EasingKind.EaseOutQuint => EaseOutQuintCurve.Evaluate(t),
            EasingKind.EaseInOutQuint => EaseInOutQuintCurve.Evaluate(t),
            _ => t,
        };
    }
}
