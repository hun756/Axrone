namespace Axrone.Numeric;

/// <summary>
/// Perceptually uniform Oklab color (L in [0, 1], a/b roughly in [-0.4, 0.4]).
/// Exactly 16 bytes, promoted directly to CPU SIMD registers.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
public readonly struct Oklab : IColorVector<Oklab>
{
    /// <summary>Lightness.</summary>
    public readonly float L;

    /// <summary>Green-red.</summary>
    public readonly float A;

    /// <summary>Blue-yellow.</summary>
    public readonly float B;

    /// <summary>Alpha.</summary>
    public readonly float Alpha;

    /// <inheritdoc/>
    public float Component0 => L;

    /// <inheritdoc/>
    public float Component1 => A;

    /// <inheritdoc/>
    public float Component2 => B;

    /// <summary>Creates an Oklab color.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Oklab(float l, float a, float b, float alpha = 1.0f)
    {
        L = l;
        A = a;
        B = b;
        Alpha = alpha;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Oklab Clamp() => new(
        Math.Clamp(L, 0.0f, 1.0f),
        Math.Clamp(A, -0.4f, 0.4f),
        Math.Clamp(B, -0.4f, 0.4f),
        Math.Clamp(Alpha, 0.0f, 1.0f));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(Oklab other)
    {
        Vector128<uint> v1 = Unsafe.BitCast<Oklab, Vector128<uint>>(this);
        Vector128<uint> v2 = Unsafe.BitCast<Oklab, Vector128<uint>>(other);
        return v1 == v2;
    }

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Oklab other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override int GetHashCode() => HashCode.Combine(L, A, B, Alpha);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(Oklab left, Oklab right) => left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(Oklab left, Oklab right) => !left.Equals(right);
}

/// <summary>
/// Cylindrical Oklab representation (lightness, chroma, hue degrees [0, 360)).
/// Strictly 16 bytes.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
public readonly struct Oklch : IColorVector<Oklch>
{
    /// <summary>Lightness.</summary>
    public readonly float L;

    /// <summary>Chroma.</summary>
    public readonly float C;

    /// <summary>Alpha.</summary>
    public readonly float Alpha;

    /// <summary>Hue in degrees, normalized to [0, 360).</summary>
    public float H
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => field;
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        init
        {
            float mod = value % 360.0f;
            field = mod < 0.0f ? mod + 360.0f : mod;
        }
    }

    /// <inheritdoc/>
    public float Component0 => L;

    /// <inheritdoc/>
    public float Component1 => C;

    /// <inheritdoc/>
    public float Component2 => H;

    /// <summary>Creates an Oklch color.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Oklch(float l, float c, float h, float alpha = 1.0f)
    {
        L = l;
        C = c;
        Alpha = alpha;
        H = h;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Oklch Clamp() => new(
        Math.Clamp(L, 0.0f, 1.0f),
        MathF.Max(0.0f, C),
        H,
        Math.Clamp(Alpha, 0.0f, 1.0f));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(Oklch other)
    {
        Vector128<uint> v1 = Unsafe.BitCast<Oklch, Vector128<uint>>(this);
        Vector128<uint> v2 = Unsafe.BitCast<Oklch, Vector128<uint>>(other);
        return v1 == v2;
    }

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Oklch other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override int GetHashCode() => HashCode.Combine(L, C, H, Alpha);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(Oklch left, Oklch right) => left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(Oklch left, Oklch right) => !left.Equals(right);
}
