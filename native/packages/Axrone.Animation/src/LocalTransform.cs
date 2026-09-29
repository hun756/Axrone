namespace Axrone.Animation;

/// <summary>
/// Padded local rigid transform: 16-byte aligned lanes keep vectorized readers
/// happy. Construction centralizes the sanitization rules (degenerate rotation
/// becomes identity, zero scale lanes become one) so no call site re-implements
/// them — the invariant lives in the type.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 16)]
public readonly struct LocalTransform : IEquatable<LocalTransform>
{
    /// <summary>Local translation.</summary>
    public readonly Vector3 Translation;

    private readonly float _pad0;

    /// <summary>Local rotation (unit).</summary>
    public readonly Quaternion Rotation;

    /// <summary>Local scale (no zero lanes).</summary>
    public readonly Vector3 Scale;

    private readonly float _pad1;

    /// <summary>Identity transform.</summary>
    public static LocalTransform Identity => new(Vector3.Zero, Quaternion.Identity, Vector3.One);

    /// <summary>Creates a sanitized transform.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LocalTransform(Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        Translation = translation;
        _pad0 = 0.0f;
        Rotation = rotation.X == 0.0f && rotation.Y == 0.0f && rotation.Z == 0.0f && rotation.W == 0.0f
            ? Quaternion.Identity
            : Quaternion.Normalize(rotation);
        Scale = new Vector3(
            scale.X == 0.0f ? 1.0f : scale.X,
            scale.Y == 0.0f ? 1.0f : scale.Y,
            scale.Z == 0.0f ? 1.0f : scale.Z);
        _pad1 = 0.0f;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(LocalTransform other) =>
        Translation.Equals(other.Translation) && Rotation.Equals(other.Rotation) && Scale.Equals(other.Scale);

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is LocalTransform other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Translation, Rotation, Scale);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(LocalTransform left, LocalTransform right) => left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(LocalTransform left, LocalTransform right) => !left.Equals(right);
}
