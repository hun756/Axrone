namespace Axrone.Asset2D;

/// <summary>
/// Represents 2D pixel dimensions with positive bounds (8 bytes, register promoted).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Size2D(Pixels Width, Pixels Height)
{
    /// <summary>Zero size.</summary>
    public static Size2D Zero => default;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override string ToString() => $"[{Width}x{Height}]";
}

/// <summary>
/// Represents an axis-aligned 2D pixel bounding box (16 bytes, dual-register promoted).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Rect2D(Pixels X, Pixels Y, Pixels Width, Pixels Height)
{
    /// <summary>Zero rectangle.</summary>
    public static Rect2D Zero => default;

    /// <summary>Dimensions.</summary>
    public Size2D Size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(Width, Height);
    }

    /// <summary>Top-left position as a vector.</summary>
    public Vec2 Position
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(X.Value, Y.Value);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override string ToString() => $"[X:{X}, Y:{Y}, W:{Width}, H:{Height}]";
}

/// <summary>
/// Represents normalized UV texture coordinates [0.0 .. 1.0] (16 bytes, dual-register promoted).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct UvRect(NormalizedUv U, NormalizedUv V, NormalizedUv Width, NormalizedUv Height)
{
    /// <summary>Zero rectangle.</summary>
    public static UvRect Zero => default;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override string ToString() => $"[U:{U}, V:{V}, W:{Width}, H:{Height}]";
}

/// <summary>
/// Represents 9-slice border insets in pixels (16 bytes, dual-register promoted).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Border2D(Pixels Left, Pixels Right, Pixels Top, Pixels Bottom)
{
    /// <summary>Zero border.</summary>
    public static Border2D Zero => default;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override string ToString() => $"[L:{Left}, R:{Right}, T:{Top}, B:{Bottom}]";
}
