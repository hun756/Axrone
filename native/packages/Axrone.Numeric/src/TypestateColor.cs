namespace Axrone.Numeric;

/// <summary>Typestate marker for alpha handling.</summary>
public interface IAlphaTypestate
{
}

/// <summary>Straight (non-premultiplied) alpha.</summary>
public readonly struct StraightAlpha : IAlphaTypestate
{
}

/// <summary>Premultiplied alpha.</summary>
public readonly struct PremultipliedAlpha : IAlphaTypestate
{
}

/// <summary>Typestate marker for transfer function.</summary>
public interface IEncodingTypestate
{
}

/// <summary>Linear transfer.</summary>
public readonly struct LinearTransfer : IEncodingTypestate
{
}

/// <summary>Non-linear (gamma) transfer.</summary>
public readonly struct NonLinearTransfer : IEncodingTypestate
{
}

/// <summary>
/// Compile-time typestate color: the type system prevents blending
/// gamma-encoded colors or mixing straight with premultiplied alpha.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
public readonly struct TypestateColor<TAlpha, TEncoding> :
    IEquatable<TypestateColor<TAlpha, TEncoding>>
    where TAlpha : struct, IAlphaTypestate
    where TEncoding : struct, IEncodingTypestate
{
    private readonly Rgba128Float _color;

    /// <summary>Creates a typestate color.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal TypestateColor(Rgba128Float color) => _color = color;

    /// <summary>Payload.</summary>
    public Rgba128Float Value => _color;

    /// <summary>Begins a linear straight-alpha pipeline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TypestateColor<StraightAlpha, LinearTransfer> CreateLinearStraight(Rgba128Float color) => new(color);

    /// <summary>Begins a gamma straight-alpha pipeline from bytes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TypestateColor<StraightAlpha, NonLinearTransfer> CreateGammaStraight(Rgba32 srgb) =>
        new(new Rgba128Float(srgb.R * (1f / 255f), srgb.G * (1f / 255f), srgb.B * (1f / 255f), srgb.A * (1f / 255f)));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(TypestateColor<TAlpha, TEncoding> other) => _color.Equals(other._color);

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is TypestateColor<TAlpha, TEncoding> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _color.GetHashCode();

    /// <inheritdoc/>
    public static bool operator ==(TypestateColor<TAlpha, TEncoding> left, TypestateColor<TAlpha, TEncoding> right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(TypestateColor<TAlpha, TEncoding> left, TypestateColor<TAlpha, TEncoding> right) => !left.Equals(right);
}

/// <summary>Compile-time transitions enforcing a valid pipeline order.</summary>
public static class TypestateTransitions
{
    /// <summary>Premultiplies straight alpha.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TypestateColor<PremultipliedAlpha, LinearTransfer> Premultiply(
        this TypestateColor<StraightAlpha, LinearTransfer> source) =>
        new(ColorBlending.Premultiply(source.Value));

    /// <summary>Unpremultiplies.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TypestateColor<StraightAlpha, LinearTransfer> Unpremultiply(
        this TypestateColor<PremultipliedAlpha, LinearTransfer> source) =>
        new(ColorBlending.Unpremultiply(source.Value));

    /// <summary>Encodes linear to gamma.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TypestateColor<StraightAlpha, NonLinearTransfer> EncodeSrgb(
        this TypestateColor<StraightAlpha, LinearTransfer> source)
    {
        Rgba32 srgb = ColorSpaceConverter.LinearRgbToSrgb(source.Value);
        return new TypestateColor<StraightAlpha, NonLinearTransfer>(
            new Rgba128Float(srgb.R * (1f / 255f), srgb.G * (1f / 255f), srgb.B * (1f / 255f), srgb.A * (1f / 255f)));
    }

    /// <summary>Decodes gamma to linear.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TypestateColor<StraightAlpha, LinearTransfer> DecodeSrgb(
        this TypestateColor<StraightAlpha, NonLinearTransfer> source)
    {
        var srgb = new Rgba32(
            (byte)Math.Clamp((int)MathF.Round(source.Value.R * 255f), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(source.Value.G * 255f), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(source.Value.B * 255f), 0, 255),
            (byte)Math.Clamp((int)MathF.Round(source.Value.A * 255f), 0, 255));
        return new TypestateColor<StraightAlpha, LinearTransfer>(ColorSpaceConverter.SrgbToLinearRgb(srgb));
    }

    /// <summary>Blends premultiplied source over premultiplied destination.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TypestateColor<PremultipliedAlpha, LinearTransfer> BlendOver(
        this TypestateColor<PremultipliedAlpha, LinearTransfer> source,
        TypestateColor<PremultipliedAlpha, LinearTransfer> destination) =>
        new(ColorBlending.BlendPremultipliedOver(source.Value, destination.Value));
}
