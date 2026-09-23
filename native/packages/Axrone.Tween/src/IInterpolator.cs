namespace Axrone.Tween;

/// <summary>Linear blend between two values.</summary>
/// <typeparam name="T">Value type.</typeparam>
public interface IInterpolator<T>
{
    /// <summary>Blends from start to end at the given factor.</summary>
    static abstract T Interpolate(in T start, in T finish, float factor);
}
