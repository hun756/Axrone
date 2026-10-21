namespace Axrone.Numeric;

/// <summary>Discriminant tag for the active color representation.</summary>
public enum ColorSpaceKind : byte
{
    /// <summary>Packed sRGB.</summary>
    Rgba32 = 0,

    /// <summary>Linear HDR vector.</summary>
    Rgba128Float = 1,

    /// <summary>Perceptual Oklab.</summary>
    Oklab = 2,

    /// <summary>Cylindrical Oklch.</summary>
    Oklch = 3,
}

/// <summary>
/// Flat zero-allocation discriminated union over the canonical color
/// representations. Strictly 20 bytes: 4-byte tag plus 16-byte payload.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 20)]
public readonly struct UniversalColor :
    IEquatable<UniversalColor>,
    IEqualityOperators<UniversalColor, UniversalColor, bool>
{
    [FieldOffset(0)]
    private readonly ColorSpaceKind _kind;

    [FieldOffset(4)]
    private readonly Rgba32 _rgba32;

    [FieldOffset(4)]
    private readonly Rgba128Float _rgba128;

    [FieldOffset(4)]
    private readonly Oklab _oklab;

    [FieldOffset(4)]
    private readonly Oklch _oklch;

    /// <summary>Active representation.</summary>
    public ColorSpaceKind Kind => _kind;

    /// <summary>Wraps a packed color.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public UniversalColor(Rgba32 color)
    {
        Unsafe.SkipInit(out this);
        _kind = ColorSpaceKind.Rgba32;
        _rgba32 = color;
    }

    /// <summary>Wraps a linear color.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public UniversalColor(Rgba128Float color)
    {
        Unsafe.SkipInit(out this);
        _kind = ColorSpaceKind.Rgba128Float;
        _rgba128 = color;
    }

    /// <summary>Wraps an Oklab color.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public UniversalColor(Oklab color)
    {
        Unsafe.SkipInit(out this);
        _kind = ColorSpaceKind.Oklab;
        _oklab = color;
    }

    /// <summary>Wraps an Oklch color.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public UniversalColor(Oklch color)
    {
        Unsafe.SkipInit(out this);
        _kind = ColorSpaceKind.Oklch;
        _oklch = color;
    }

    /// <summary>Tries to read the packed representation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryGetRgba32(out Rgba32 color)
    {
        if (_kind == ColorSpaceKind.Rgba32)
        {
            color = _rgba32;
            return true;
        }

        color = default;
        return false;
    }

    /// <summary>Tries to read the linear representation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryGetRgba128Float(out Rgba128Float color)
    {
        if (_kind == ColorSpaceKind.Rgba128Float)
        {
            color = _rgba128;
            return true;
        }

        color = default;
        return false;
    }

    /// <summary>Tries to read the Oklab representation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryGetOklab(out Oklab color)
    {
        if (_kind == ColorSpaceKind.Oklab)
        {
            color = _oklab;
            return true;
        }

        color = default;
        return false;
    }

    /// <summary>Tries to read the Oklch representation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryGetOklch(out Oklch color)
    {
        if (_kind == ColorSpaceKind.Oklch)
        {
            color = _oklch;
            return true;
        }

        color = default;
        return false;
    }

    /// <summary>Converts to packed sRGB.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Rgba32 ToRgba32() => _kind switch
    {
        ColorSpaceKind.Rgba32 => _rgba32,
        ColorSpaceKind.Rgba128Float => ColorSpaceConverter.LinearRgbToSrgb(_rgba128),
        ColorSpaceKind.Oklab => ColorSpaceConverter.OklabToSrgb(_oklab),
        ColorSpaceKind.Oklch => ColorSpaceConverter.OklabToSrgb(ColorSpaceConverter.OklchToOklab(_oklch)),
        _ => ThrowHelper.ThrowInvalidOperationException<Rgba32>("Corrupt union discriminant tag."),
    };

    /// <summary>Converts to linear.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Rgba128Float ToLinearRgb() => _kind switch
    {
        ColorSpaceKind.Rgba32 => ColorSpaceConverter.SrgbToLinearRgb(_rgba32),
        ColorSpaceKind.Rgba128Float => _rgba128,
        ColorSpaceKind.Oklab => ColorSpaceConverter.OklabToLinearRgb(_oklab),
        ColorSpaceKind.Oklch => ColorSpaceConverter.OklabToLinearRgb(ColorSpaceConverter.OklchToOklab(_oklch)),
        _ => ThrowHelper.ThrowInvalidOperationException<Rgba128Float>("Corrupt union discriminant tag."),
    };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(UniversalColor other)
    {
        if (_kind != other._kind)
        {
            return false;
        }

        return _kind switch
        {
            ColorSpaceKind.Rgba32 => _rgba32 == other._rgba32,
            ColorSpaceKind.Rgba128Float => _rgba128 == other._rgba128,
            ColorSpaceKind.Oklab => _oklab == other._oklab,
            ColorSpaceKind.Oklch => _oklch == other._oklch,
            _ => false,
        };
    }

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is UniversalColor other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _kind switch
    {
        ColorSpaceKind.Rgba32 => HashCode.Combine(_kind, _rgba32),
        ColorSpaceKind.Rgba128Float => HashCode.Combine(_kind, _rgba128),
        ColorSpaceKind.Oklab => HashCode.Combine(_kind, _oklab),
        ColorSpaceKind.Oklch => HashCode.Combine(_kind, _oklch),
        _ => 0,
    };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(UniversalColor left, UniversalColor right) => left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(UniversalColor left, UniversalColor right) => !left.Equals(right);
}
