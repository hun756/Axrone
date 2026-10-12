namespace Axrone.Geometry;

using System.Globalization;

/// <summary>
/// Identity of a camera: a non-empty name. Empty and whitespace names are
/// rejected at construction, so a default-initialized id can never name a
/// live camera.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CameraId : IEquatable<CameraId>, IComparable<CameraId>
{
    private readonly string _value;

    /// <summary>Creates an id; empty or whitespace is rejected.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CameraId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ThrowHelper.ThrowArgumentException("CameraId value cannot be null or whitespace.");
        }

        _value = value;
    }

    /// <summary>The underlying name; empty when default-initialized.</summary>
    public string Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value ?? string.Empty;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(CameraId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(CameraId other) => string.CompareOrdinal(_value, other._value);

    /// <inheritdoc/>
    public override int GetHashCode() => _value is not null ? string.GetHashCode(_value, StringComparison.Ordinal) : 0;

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the underlying name.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(CameraId id) => id.Value;

    /// <summary>Wraps a name, applying the emptiness guard.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator CameraId(string value) => new(value);
}

/// <summary>Vertical field of view in radians; the open interval (0, PI).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct RadianFoV : IComparable<RadianFoV>
{
    /// <summary>Underlying radians.</summary>
    public readonly float Value;

    /// <summary>Creates a field of view, rejecting anything outside (0, PI).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RadianFoV(float value)
    {
        if (value <= 0.0f || value >= MathF.PI)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(value));
        }

        Value = value;
    }

    /// <summary>Converts degrees to a guarded radian field of view.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RadianFoV FromDegrees(float degrees) => new(degrees * (MathF.PI / 180.0f));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(RadianFoV other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Value.ToString("G", CultureInfo.InvariantCulture);

    /// <summary>Unwraps the underlying radians.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float(RadianFoV fov) => fov.Value;

    /// <summary>Determines whether the left field of view is narrower than the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(RadianFoV left, RadianFoV right) => left.Value < right.Value;

    /// <summary>Determines whether the left field of view exceeds the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(RadianFoV left, RadianFoV right) => left.Value > right.Value;

    /// <summary>Determines whether the left field of view is at most the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(RadianFoV left, RadianFoV right) => left.Value <= right.Value;

    /// <summary>Determines whether the left field of view is at least the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(RadianFoV left, RadianFoV right) => left.Value >= right.Value;
}

/// <summary>Width over height of the view; strictly positive and finite.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AspectRatio : IComparable<AspectRatio>
{
    /// <summary>Underlying ratio.</summary>
    public readonly float Value;

    /// <summary>Creates a ratio, rejecting non-positive or non-finite values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AspectRatio(float value)
    {
        if (value <= 0.0f || float.IsInfinity(value) || float.IsNaN(value))
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(value));
        }

        Value = value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(AspectRatio other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Value.ToString("G", CultureInfo.InvariantCulture);

    /// <summary>Unwraps the underlying ratio.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float(AspectRatio ratio) => ratio.Value;

    /// <summary>Determines whether the left ratio is below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AspectRatio left, AspectRatio right) => left.Value < right.Value;

    /// <summary>Determines whether the left ratio exceeds the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AspectRatio left, AspectRatio right) => left.Value > right.Value;

    /// <summary>Determines whether the left ratio is at or below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AspectRatio left, AspectRatio right) => left.Value <= right.Value;

    /// <summary>Determines whether the left ratio is at or above the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AspectRatio left, AspectRatio right) => left.Value >= right.Value;
}

/// <summary>Near/far plane distance; strictly positive and finite.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct DepthDistance : IComparable<DepthDistance>
{
    /// <summary>Underlying distance.</summary>
    public readonly float Value;

    /// <summary>Creates a distance, rejecting non-positive or non-finite values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DepthDistance(float value)
    {
        if (value <= 0.0f || float.IsInfinity(value) || float.IsNaN(value))
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(value));
        }

        Value = value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(DepthDistance other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Value.ToString("G", CultureInfo.InvariantCulture);

    /// <summary>Unwraps the underlying distance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float(DepthDistance distance) => distance.Value;

    /// <summary>Determines whether the left distance is below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(DepthDistance left, DepthDistance right) => left.Value < right.Value;

    /// <summary>Determines whether the left distance exceeds the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(DepthDistance left, DepthDistance right) => left.Value > right.Value;

    /// <summary>Determines whether the left distance is at or below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(DepthDistance left, DepthDistance right) => left.Value <= right.Value;

    /// <summary>Determines whether the left distance is at or above the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(DepthDistance left, DepthDistance right) => left.Value >= right.Value;
}
