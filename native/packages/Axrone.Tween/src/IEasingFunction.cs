namespace Axrone.Tween;

/// <summary>Closed-form easing curve over the unit interval.</summary>
public interface IEasingFunction
{
    /// <summary>Evaluates the curve at normalized time.</summary>
    static abstract float Evaluate(float t);
}
