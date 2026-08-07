namespace Axrone.Utility.Comparer;

/// <summary>
/// Floating-point comparison utilities with configurable epsilon tolerance.
/// </summary>
/// <remarks>
/// All methods are static pure functions — zero allocation, safe for hot paths.
/// </remarks>
public static class FloatCompare
{
    /// <summary>Default epsilon for single-precision: ~1.19e-7 * 4 = ~4.76e-7.</summary>
    public const float DefaultFloatEpsilon = 1e-5f;

    /// <summary>Default epsilon for double-precision: ~1e-9.</summary>
    public const double DefaultDoubleEpsilon = 1e-9;

    /// <summary>Compare two floats with absolute epsilon tolerance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AlmostEqual(float a, float b, float epsilon = DefaultFloatEpsilon)
        => MathF.Abs(a - b) <= epsilon;

    /// <summary>Compare two doubles with absolute epsilon tolerance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AlmostEqual(double a, double b, double epsilon = DefaultDoubleEpsilon)
        => Math.Abs(a - b) <= epsilon;

    /// <summary>
    /// Compare two floats with relative + absolute epsilon (ULP-aware).
    /// Handles values near zero correctly.
    /// </summary>
    public static bool AlmostEqualRelative(float a, float b, float relEpsilon = 1e-4f, float absEpsilon = DefaultFloatEpsilon)
    {
        var diff = MathF.Abs(a - b);
        if (diff <= absEpsilon) return true;
        var largest = MathF.Max(MathF.Abs(a), MathF.Abs(b));
        return diff <= largest * relEpsilon;
    }

    /// <summary>
    /// Compare two doubles with relative + absolute epsilon.
    /// </summary>
    public static bool AlmostEqualRelative(double a, double b, double relEpsilon = 1e-6, double absEpsilon = DefaultDoubleEpsilon)
    {
        var diff = Math.Abs(a - b);
        if (diff <= absEpsilon) return true;
        var largest = Math.Max(Math.Abs(a), Math.Abs(b));
        return diff <= largest * relEpsilon;
    }

    /// <summary>Three-way comparison for floats with epsilon.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Compare(float a, float b, float epsilon = DefaultFloatEpsilon)
    {
        if (AlmostEqual(a, b, epsilon)) return 0;
        return a < b ? -1 : 1;
    }

    /// <summary>Three-way comparison for doubles with epsilon.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Compare(double a, double b, double epsilon = DefaultDoubleEpsilon)
    {
        if (AlmostEqual(a, b, epsilon)) return 0;
        return a < b ? -1 : 1;
    }

    /// <summary>Check if a float is approximately zero.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsZero(float value, float epsilon = DefaultFloatEpsilon)
        => MathF.Abs(value) <= epsilon;

    /// <summary>Check if a double is approximately zero.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsZero(double value, double epsilon = DefaultDoubleEpsilon)
        => Math.Abs(value) <= epsilon;
}

/// <summary>
/// Generic equality comparer that delegates to a <see cref="Func{T1, T2, TResult}"/>.
/// Useful for inline comparisons without allocating a full comparer class.
/// </summary>
public readonly struct FuncEqualityComparer<T> : IEqualityComparer<T>
{
    private readonly Func<T, T, bool> _equals;
    private readonly Func<T, int> _getHashCode;

    public FuncEqualityComparer(Func<T, T, bool> equals, Func<T, int> getHashCode)
    {
        ArgumentNullException.ThrowIfNull(equals);
        ArgumentNullException.ThrowIfNull(getHashCode);
        _equals = equals;
        _getHashCode = getHashCode;
    }

    public bool Equals(T? x, T? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        return _equals(x, y);
    }

    public int GetHashCode(T obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return _getHashCode(obj);
    }
}
