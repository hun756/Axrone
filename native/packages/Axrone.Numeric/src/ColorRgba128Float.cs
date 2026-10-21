namespace Axrone.Numeric;

/// <summary>
/// Linear RGBA high-dynamic-range vector (16 bytes): maps directly to
/// 128-bit hardware SIMD registers.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
public readonly struct Rgba128Float :
    IColorVector<Rgba128Float>,
    ISpanFormattable,
    IUtf8SpanFormattable
{
    /// <summary>Red channel.</summary>
    public readonly float R;

    /// <summary>Green channel.</summary>
    public readonly float G;

    /// <summary>Blue channel.</summary>
    public readonly float B;

    /// <summary>Alpha channel.</summary>
    public readonly float A;

    /// <inheritdoc/>
    public float Component0 => R;

    /// <inheritdoc/>
    public float Component1 => G;

    /// <inheritdoc/>
    public float Component2 => B;

    /// <inheritdoc/>
    public float Alpha => A;

    /// <summary>Creates a linear color.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Rgba128Float(float r, float g, float b, float a = 1.0f)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>Reinterprets a SIMD register.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Rgba128Float(Vector128<float> vector) => this = Unsafe.BitCast<Vector128<float>, Rgba128Float>(vector);

    /// <summary>Reinterprets as a SIMD register.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Vector128<float> AsVector128() => Unsafe.BitCast<Rgba128Float, Vector128<float>>(this);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Rgba128Float Clamp()
    {
        Vector128<float> clamped = Vector128.Min(Vector128.Max(AsVector128(), Vector128<float>.Zero), Vector128.Create(1.0f));
        return new Rgba128Float(clamped);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(Rgba128Float other)
    {
        Vector128<uint> v1 = Unsafe.BitCast<Rgba128Float, Vector128<uint>>(this);
        Vector128<uint> v2 = Unsafe.BitCast<Rgba128Float, Vector128<uint>>(other);
        return v1 == v2;
    }

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Rgba128Float other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override int GetHashCode()
    {
        Vector128<float> v = AsVector128();
        return HashCode.Combine(v.GetElement(0), v.GetElement(1), v.GetElement(2), v.GetElement(3));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(Rgba128Float left, Rgba128Float right) => left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(Rgba128Float left, Rgba128Float right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => ToString(null, null);

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        $"Rgba128Float {{ R = {R:F4}, G = {G:F4}, B = {B:F4}, A = {A:F4} }}";

    /// <inheritdoc/>
    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = default)
    {
        Rgba32 srgb = ColorSpaceConverter.LinearRgbToSrgb(this);
        return srgb.TryFormat(destination, out charsWritten, format, provider);
    }

    /// <inheritdoc/>
    public bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = default)
    {
        Rgba32 srgb = ColorSpaceConverter.LinearRgbToSrgb(this);
        return srgb.TryFormat(utf8Destination, out bytesWritten, format, provider);
    }
}
