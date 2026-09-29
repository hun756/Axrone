namespace Axrone.Animation;

/// <summary>
/// Zero-copy pose view over caller-owned lanes: arena scratch, stack spans, or
/// foreign buffers can participate in retargeting without an
/// <see cref="AnimationFrame"/> allocation. A ref struct by necessity (spans);
/// consumed by overloads, never generics — the language forbids interface
/// implementation on ref structs, so a generic pose-source contract could never
/// accept this type. Overloads are the honest shape.
/// </summary>
public readonly ref struct AnimationFrameView
{
    /// <summary>Translation lane.</summary>
    public Span<Vector3> Translations { get; }

    /// <summary>Rotation lane.</summary>
    public Span<Quaternion> Rotations { get; }

    /// <summary>Scale lane.</summary>
    public Span<Vector3> Scales { get; }

    /// <summary>Curve lane.</summary>
    public Span<float> Curves { get; }

    /// <summary>Creates a view; lanes must agree in length.</summary>
    public AnimationFrameView(Span<Vector3> translations, Span<Quaternion> rotations, Span<Vector3> scales, Span<float> curves = default)
    {
        if (rotations.Length != translations.Length || scales.Length != translations.Length)
        {
            AnimationThrowHelper.ThrowValidation(AnimationErrorCode.ValidationInvalidArgument, "Pose view lanes disagree in length.");
        }

        Translations = translations;
        Rotations = rotations;
        Scales = scales;
        Curves = curves;
    }

    /// <summary>Views a frame's lanes without copying.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AnimationFrameView Over(AnimationFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return new AnimationFrameView(frame.GetTranslations(), frame.GetRotations(), frame.GetScales(), frame.Curves.AsSpan());
    }
}
