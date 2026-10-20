namespace Axrone.Asset2D;

/// <summary>
/// Nominal strongly-typed pixel coordinate or dimension (4 bytes, register promoted).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Pixels(float Value) :
    IEquatable<Pixels>,
    IComparable<Pixels>,
    IComparisonOperators<Pixels, Pixels, bool>,
    IAdditionOperators<Pixels, Pixels, Pixels>,
    ISubtractionOperators<Pixels, Pixels, Pixels>,
    IMultiplyOperators<Pixels, float, Pixels>,
    IDivisionOperators<Pixels, float, Pixels>
{
    /// <summary>Zero pixels.</summary>
    public static Pixels Zero => default;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(Pixels other) => Value.Equals(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(Pixels other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(Pixels left, Pixels right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(Pixels left, Pixels right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(Pixels left, Pixels right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(Pixels left, Pixels right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Pixels operator +(Pixels left, Pixels right) => new(left.Value + right.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Pixels operator -(Pixels left, Pixels right) => new(left.Value - right.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Pixels operator *(Pixels left, float scalar) => new(left.Value * scalar);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Pixels operator /(Pixels left, float scalar) => new(left.Value / scalar);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Pixels(float value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator float(Pixels pixels) => pixels.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override string ToString() => $"{Value:F2}px";
}

/// <summary>
/// Nominal strongly-typed temporal duration in milliseconds (4 bytes, register promoted).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Milliseconds(float Value) :
    IEquatable<Milliseconds>,
    IComparable<Milliseconds>,
    IComparisonOperators<Milliseconds, Milliseconds, bool>,
    IAdditionOperators<Milliseconds, Milliseconds, Milliseconds>,
    ISubtractionOperators<Milliseconds, Milliseconds, Milliseconds>,
    IMultiplyOperators<Milliseconds, float, Milliseconds>,
    IDivisionOperators<Milliseconds, float, Milliseconds>
{
    /// <summary>Zero duration.</summary>
    public static Milliseconds Zero => default;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(Milliseconds other) => Value.Equals(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(Milliseconds other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(Milliseconds left, Milliseconds right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(Milliseconds left, Milliseconds right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(Milliseconds left, Milliseconds right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(Milliseconds left, Milliseconds right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Milliseconds operator +(Milliseconds left, Milliseconds right) => new(left.Value + right.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Milliseconds operator -(Milliseconds left, Milliseconds right) => new(left.Value - right.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Milliseconds operator *(Milliseconds left, float scalar) => new(left.Value * scalar);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Milliseconds operator /(Milliseconds left, float scalar) => new(left.Value / scalar);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Milliseconds(float value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator float(Milliseconds ms) => ms.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override string ToString() => $"{Value:F1}ms";
}

/// <summary>
/// Nominal strongly-typed normalized texture coordinate [0.0 .. 1.0] (4 bytes, register promoted).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct NormalizedUv(float Value) :
    IEquatable<NormalizedUv>,
    IComparable<NormalizedUv>,
    IComparisonOperators<NormalizedUv, NormalizedUv, bool>
{
    /// <summary>Zero coordinate.</summary>
    public static NormalizedUv Zero => default;

    /// <summary>Unit coordinate.</summary>
    public static NormalizedUv One => new(1.0f);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(NormalizedUv other) => Value.Equals(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(NormalizedUv other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(NormalizedUv left, NormalizedUv right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(NormalizedUv left, NormalizedUv right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(NormalizedUv left, NormalizedUv right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(NormalizedUv left, NormalizedUv right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator NormalizedUv(float value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator float(NormalizedUv uv) => uv.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override string ToString() => $"{Value:F4}";
}

/// <summary>
/// Nominal strongly-typed Atlas identifier (8 bytes, pointer/register passable).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct AtlasId : IEquatable<AtlasId>, IComparable<AtlasId>
{
    /// <summary>Identifier text.</summary>
    public string Value { get; }

    /// <summary>Creates an identifier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AtlasId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ThrowHelper.ThrowInvalidIdentifier(nameof(AtlasId));
        }

        Value = value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AtlasId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AtlasId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>Determines whether the left nominal sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AtlasId left, AtlasId right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left nominal sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AtlasId left, AtlasId right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left nominal sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AtlasId left, AtlasId right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left nominal sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AtlasId left, AtlasId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override int GetHashCode() => Value is not null ? string.GetHashCode(Value.AsSpan(), StringComparison.Ordinal) : 0;

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;

    /// <inheritdoc/>
    public static implicit operator AtlasId(string value) => new(value);
}

/// <summary>
/// Nominal strongly-typed Texture identifier (8 bytes, pointer/register passable).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct TextureId : IEquatable<TextureId>, IComparable<TextureId>
{
    /// <summary>Identifier text.</summary>
    public string Value { get; }

    /// <summary>Creates an identifier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextureId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ThrowHelper.ThrowInvalidIdentifier(nameof(TextureId));
        }

        Value = value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TextureId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(TextureId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>Determines whether the left nominal sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(TextureId left, TextureId right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left nominal sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(TextureId left, TextureId right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left nominal sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(TextureId left, TextureId right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left nominal sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(TextureId left, TextureId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override int GetHashCode() => Value is not null ? string.GetHashCode(Value.AsSpan(), StringComparison.Ordinal) : 0;

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;

    /// <inheritdoc/>
    public static implicit operator TextureId(string value) => new(value);
}

/// <summary>
/// Nominal strongly-typed Frame identifier with allocation-free span comparison (8 bytes, pointer/register passable).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct FrameId : IEquatable<FrameId>, IComparable<FrameId>
{
    /// <summary>Identifier text.</summary>
    public string Value { get; }

    /// <summary>Creates an identifier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FrameId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ThrowHelper.ThrowInvalidIdentifier(nameof(FrameId));
        }

        Value = value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(FrameId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <summary>Compares against a span without allocating.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ReadOnlySpan<char> span) => Value is not null && Value.AsSpan().SequenceEqual(span);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(FrameId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>Determines whether the left nominal sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(FrameId left, FrameId right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left nominal sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(FrameId left, FrameId right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left nominal sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(FrameId left, FrameId right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left nominal sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(FrameId left, FrameId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override int GetHashCode() => Value is not null ? string.GetHashCode(Value.AsSpan(), StringComparison.Ordinal) : 0;

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;

    /// <inheritdoc/>
    public static implicit operator FrameId(string value) => new(value);
}

/// <summary>
/// Nominal strongly-typed Animation Clip identifier (8 bytes, pointer/register passable).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ClipId : IEquatable<ClipId>, IComparable<ClipId>
{
    /// <summary>Identifier text.</summary>
    public string Value { get; }

    /// <summary>Creates an identifier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ClipId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ThrowHelper.ThrowInvalidIdentifier(nameof(ClipId));
        }

        Value = value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ClipId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <summary>Compares against a span without allocating.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ReadOnlySpan<char> span) => Value is not null && Value.AsSpan().SequenceEqual(span);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(ClipId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>Determines whether the left nominal sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(ClipId left, ClipId right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left nominal sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(ClipId left, ClipId right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left nominal sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(ClipId left, ClipId right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left nominal sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(ClipId left, ClipId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override int GetHashCode() => Value is not null ? string.GetHashCode(Value.AsSpan(), StringComparison.Ordinal) : 0;

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;

    /// <inheritdoc/>
    public static implicit operator ClipId(string value) => new(value);
}
