namespace Axrone.Tween;

/// <summary>Scalar and vector linear interpolators.</summary>
public readonly struct FloatInterpolator : IInterpolator<float>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Interpolate(in float start, in float finish, float factor) => start + ((finish - start) * factor);
}

/// <inheritdoc cref="FloatInterpolator"/>
public readonly struct Vector2Interpolator : IInterpolator<Vector2>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Interpolate(in Vector2 start, in Vector2 finish, float factor) => start + ((finish - start) * factor);
}

/// <inheritdoc cref="FloatInterpolator"/>
public readonly struct Vector3Interpolator : IInterpolator<Vector3>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Interpolate(in Vector3 start, in Vector3 finish, float factor) => start + ((finish - start) * factor);
}

/// <inheritdoc cref="FloatInterpolator"/>
public readonly struct Vector4Interpolator : IInterpolator<Vector4>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Interpolate(in Vector4 start, in Vector4 finish, float factor) => start + ((finish - start) * factor);
}
