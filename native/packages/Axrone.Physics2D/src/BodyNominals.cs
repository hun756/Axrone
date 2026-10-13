namespace Axrone.Physics2D;

using System.Globalization;

/// <summary>
/// 4-byte identity of a simulated body. The zero value is the invalid
/// sentinel, so a default-initialized slot can never name a live body.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct BodyId(uint Value) : IComparable<BodyId>
{
    /// <summary>Invalid sentinel; never a registered body.</summary>
    public static BodyId Invalid => default;

    /// <summary>Whether this identity names a registered body.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value != 0;
    }

    /// <summary>Converts a 1-based identity to its 0-based slot index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SlotIndex ToSlotIndex() => new((int)Value - 1);

    /// <summary>Wraps a 0-based slot index as a 1-based identity.</summary>
    /// <param name="slot">The slot index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BodyId FromSlotIndex(SlotIndex slot) => new((uint)slot.Value + 1u);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(BodyId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => IsValid ? string.Create(CultureInfo.InvariantCulture, $"Body({Value})") : "Body(Invalid)";

    /// <summary>Unwraps the underlying identifier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(BodyId id) => id.Value;

    /// <summary>Wraps a raw identifier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator BodyId(uint value) => new(value);

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(BodyId left, BodyId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(BodyId left, BodyId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(BodyId left, BodyId right) => left.Value <= right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(BodyId left, BodyId right) => left.Value >= right.Value;
}

/// <summary>
/// 4-byte identity of a collider attached to a body. The zero value is the
/// invalid sentinel, so a default-initialized slot can never name a live shape.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ShapeId(uint Value) : IComparable<ShapeId>
{
    /// <summary>Invalid sentinel; never a registered shape.</summary>
    public static ShapeId Invalid => default;

    /// <summary>Whether this identity names a registered shape.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value != 0;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(ShapeId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => IsValid ? string.Create(CultureInfo.InvariantCulture, $"Shape({Value})") : "Shape(Invalid)";

    /// <summary>Unwraps the underlying identifier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(ShapeId id) => id.Value;

    /// <summary>Wraps a raw identifier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ShapeId(uint value) => new(value);

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(ShapeId left, ShapeId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(ShapeId left, ShapeId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(ShapeId left, ShapeId right) => left.Value <= right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(ShapeId left, ShapeId right) => left.Value >= right.Value;
}

/// <summary>How a body moves: fixed scenery, script-driven kinematics, or simulated dynamics.</summary>
public enum BodyType : byte
{
    /// <summary>Immovable scenery; infinite mass, never integrated.</summary>
    Static = 0,

    /// <summary>Script-driven motion; infinite mass, pushes dynamics but never reacts.</summary>
    Kinematic = 1,

    /// <summary>Fully simulated; finite mass, integrated and resolved.</summary>
    Dynamic = 2,
}

/// <summary>
/// Collision filtering triple: shared group overrides, category/mask pairs
/// otherwise. Two shapes interact exactly when <see cref="ShouldCollide"/>
/// says so, so the broadphase can reject pairs before any geometry runs.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CollisionFilter(uint Category, uint Mask, int Group)
{
    /// <summary>Default filter: category one, collides with everything, no group.</summary>
    public static CollisionFilter Default => new(0x0001u, 0xFFFFu, 0);

    /// <summary>Determines whether two filters interact.</summary>
    /// <param name="a">The first filter.</param>
    /// <param name="b">The second filter.</param>
    /// <returns>
    /// <see langword="true"/> when a positive shared group unites them, or when no
    /// negative group separates them and the category/mask pairs cross-match.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool ShouldCollide(in CollisionFilter a, in CollisionFilter b)
    {
        if (a.Group != 0 && a.Group == b.Group)
        {
            return a.Group > 0;
        }

        return (a.Mask & b.Category) != 0 && (b.Mask & a.Category) != 0;
    }
}

/// <summary>
/// Contact response of a shape pair: friction resists sliding, restitution
/// returns bounce. Both are guarded at construction so an invalid material
/// can never enter the solver.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct PhysicsMaterial
{
    /// <summary>Coulomb friction; never negative.</summary>
    public readonly float Friction;

    /// <summary>Restitution; the closed interval [0, 1].</summary>
    public readonly float Restitution;

    /// <summary>Balanced default: slight friction, no bounce.</summary>
    public static PhysicsMaterial Default => new(0.2f, 0.0f);

    /// <summary>Creates a material, rejecting negative friction and out-of-range restitution.</summary>
    /// <param name="friction">Coulomb friction; must not be negative.</param>
    /// <param name="restitution">Restitution; must lie in [0, 1].</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysicsMaterial(float friction, float restitution)
    {
        if (friction < 0.0f || float.IsNaN(friction))
        {
            ThrowHelper.ThrowInvalidOperation("Friction must not be negative.");
        }

        if (restitution < 0.0f || restitution > 1.0f || float.IsNaN(restitution))
        {
            ThrowHelper.ThrowInvalidOperation("Restitution must lie in [0, 1].");
        }

        Friction = friction;
        Restitution = restitution;
    }
}
