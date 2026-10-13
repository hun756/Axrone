namespace Axrone.Physics2D;

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

#region Domain Primitives

/// <summary>Stored joint kind; selects the active constraint archetype.</summary>
public enum JointKind : byte
{
    /// <summary>Rigid rod between two anchors.</summary>
    Distance = 0,

    /// <summary>Pinned hinge with optional limits and motor.</summary>
    Revolute = 1,

    /// <summary>Locked relative pose; bodies move as one.</summary>
    Weld = 2,

    /// <summary>Slider along an axis with optional stops and motor.</summary>
    Prismatic = 3,

    /// <summary>Servo driving a body toward a moving target frame with capped force.</summary>
    Motor = 4,

    /// <summary>Sprung suspension along an axis with an optional hub motor.</summary>
    Wheel = 5,

    /// <summary>Two anchors hung over ground pulleys with a length ratio.</summary>
    Pulley = 6,

    /// <summary>Ratio constraint between the coordinates of two revolute/prismatic joints.</summary>
    Gear = 7,
}

/// <summary>Angular or linear limit state resolved per step.</summary>
public enum LimitState : byte
{
    /// <summary>Limits disabled or value strictly inside.</summary>
    Inactive = 0,

    /// <summary>At the lower stop; only opening impulses apply.</summary>
    AtLower = 1,

    /// <summary>At the upper stop; only closing impulses apply.</summary>
    AtUpper = 2,

    /// <summary>Stops coincide; the coordinate is locked.</summary>
    Equal = 3,
}

#endregion

#region Nominal Identities

/// <summary>
/// Type-safe nominal identifier for constraints. Upper 4 bits encode the joint kind,
/// lower 28 bits encode the dense slot index. Register-passable (4 bytes).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct JointId(uint Value) :
    IComparable<JointId>,
    IEquatable<JointId>,
    IComparisonOperators<JointId, JointId, bool>,
    IEqualityOperators<JointId, JointId, bool>
{
    private const int KindShift = 28;
    private const uint IndexMask = 0x0FFF_FFFF;

    /// <summary>Invalid sentinel; never a registered joint.</summary>
    public static JointId Invalid => default;

    /// <summary>Wraps a kind and a dense slot index.</summary>
    /// <param name="kind">The constraint archetype.</param>
    /// <param name="index">The dense slot index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JointId(JointKind kind, uint index)
        : this((((uint)kind + 1u) << KindShift) | (index & IndexMask))
    {
    }

    /// <summary>Whether this identity names a registered joint.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Value >> KindShift) != 0;
    }

    /// <summary>The constraint archetype encoded in the identity.</summary>
    public JointKind Kind
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (JointKind)((Value >> KindShift) - 1u);
    }

    /// <summary>The dense slot index encoded in the identity.</summary>
    public uint SlotIndex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value & IndexMask;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(JointId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => IsValid ? $"Joint({Kind}:{SlotIndex})" : "Joint(Invalid)";

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(JointId left, JointId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(JointId left, JointId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(JointId left, JointId right) => left.Value <= right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(JointId left, JointId right) => left.Value >= right.Value;

    /// <summary>Unwraps the underlying identifier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(JointId id) => id.Value;
}

#endregion

#region Microarchitectural Helpers

/// <summary>
/// Shared joint math: frame rotations, angle wrapping, limit resolution and the
/// position-correction applicator. Single home for the expressions every policy
/// repeats, so fixes land once.
/// </summary>
internal static class MathPhysics
{
    /// <summary>Rotates a vector by a body frame.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Rotate(in BodyState state, Vec2 vector) =>
        new((state.Cos * vector.X) - (state.Sin * vector.Y), (state.Sin * vector.X) + (state.Cos * vector.Y));

    /// <summary>Returns the perpendicular follower of a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Perp(Vec2 vector) => new(-vector.Y, vector.X);

    /// <summary>Wraps an angle into [-pi, pi].</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float NormalizeAngle(float angle) =>
        angle - (MathF.Floor((angle + MathF.PI) / (2.0f * MathF.PI)) * 2.0f * MathF.PI);

    /// <summary>Resolves the limit state of a coordinate against its stops.</summary>
    /// <param name="enabled">Whether stops apply.</param>
    /// <param name="lower">The lower stop.</param>
    /// <param name="upper">The upper stop.</param>
    /// <param name="value">The current coordinate.</param>
    /// <param name="slop">The coincidence band fusing near-equal stops.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LimitState ResolveLimitState(bool enabled, float lower, float upper, float value, float slop)
    {
        if (!enabled)
        {
            return LimitState.Inactive;
        }

        if (upper - lower < 2.0f * slop)
        {
            return LimitState.Equal;
        }

        if (value <= lower)
        {
            return LimitState.AtLower;
        }

        if (value >= upper)
        {
            return LimitState.AtUpper;
        }

        return LimitState.Inactive;
    }

    /// <summary>Applies one position correction to a body pair with rotation coupling.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyPositionCorrection(
        ref BodyState stateA, ref BodyState stateB, Vec2 rA, Vec2 rB, Vec2 correction,
        float invMassA, float invMassB, float invIA, float invIB, bool awakeA, bool awakeB)
    {
        if (awakeA)
        {
            stateA.Position -= correction * invMassA;
            float angleA = MathF.Atan2(stateA.Sin, stateA.Cos) - (invIA * Vec2.Cross(rA, correction));
            stateA.Cos = MathF.Cos(angleA);
            stateA.Sin = MathF.Sin(angleA);
        }

        if (awakeB)
        {
            stateB.Position += correction * invMassB;
            float angleB = MathF.Atan2(stateB.Sin, stateB.Cos) + (invIB * Vec2.Cross(rB, correction));
            stateB.Cos = MathF.Cos(angleB);
            stateB.Sin = MathF.Sin(angleB);
        }
    }
}

/// <summary>Compile-time construction phases; invalid orders do not compile.</summary>
#pragma warning disable CA1034 // Typestate phantoms are intentionally nested: the phase only means something qualified by its builder protocol, and unqualified top-level names would pollute the package surface.
public static class Typestate
{
    /// <summary>No configuration yet.</summary>
    public readonly struct Unconfigured;

    /// <summary>Bodies bound; geometry still open.</summary>
    public readonly struct BodiesConfigured;

    /// <summary>Bodies and geometry bound; ready to build.</summary>
    public readonly struct GeometryConfigured;

    /// <summary>Geared joints bound; ready to build.</summary>
    public readonly struct JointsConfigured;
}
#pragma warning restore CA1034

#endregion

#region Validated Parameter Definitions

/// <summary>
/// Construction parameters of a revolute joint: two bodies pinned through a
/// shared world anchor, with optional angular stops and a speed motor.
/// Local anchors and the reference angle derive from the world anchor at
/// creation, so the caller never converts frames by hand.
/// </summary>
public readonly struct RevoluteDef
{
    /// <summary>Creates default parameters.</summary>
    public RevoluteDef()
    {
    }

    /// <summary>First body.</summary>
    public BodyId BodyA { get; init; } = BodyId.Invalid;

    /// <summary>Second body.</summary>
    public BodyId BodyB { get; init; } = BodyId.Invalid;

    /// <summary>Shared world anchor.</summary>
    public Vec2 WorldAnchor { get; init; } = default;

    /// <summary>Lower angular stop in radians.</summary>
    public float LowerAngle
    {
        get => field;
        init
        {
            if (float.IsNaN(value))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(LowerAngle));
            }

            field = value;
        }
    } = 0.0f;

    /// <summary>Upper angular stop in radians.</summary>
    public float UpperAngle
    {
        get => field;
        init
        {
            if (float.IsNaN(value))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(UpperAngle));
            }

            field = value;
        }
    } = 0.0f;

    /// <summary>Whether angular stops apply.</summary>
    public bool EnableLimit { get; init; } = false;

    /// <summary>Target relative angular velocity.</summary>
    public float MotorSpeed
    {
        get => field;
        init
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(MotorSpeed));
            }

            field = value;
        }
    } = 0.0f;

    /// <summary>Peak motor torque; never negative.</summary>
    public float MaxMotorTorque
    {
        get => field;
        init
        {
            if (value < 0.0f || float.IsNaN(value))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(MaxMotorTorque));
            }

            field = value;
        }
    } = 0.0f;

    /// <summary>Whether the motor drives.</summary>
    public bool EnableMotor { get; init; } = false;
}

/// <summary>
/// Construction parameters of a weld joint: two bodies locked through a
/// shared world anchor. Local anchors and the reference angle derive from
/// the world anchor at creation.
/// </summary>
public readonly struct WeldDef
{
    /// <summary>Creates default parameters.</summary>
    public WeldDef()
    {
    }

    /// <summary>First body.</summary>
    public BodyId BodyA { get; init; } = BodyId.Invalid;

    /// <summary>Second body.</summary>
    public BodyId BodyB { get; init; } = BodyId.Invalid;

    /// <summary>Shared world anchor.</summary>
    public Vec2 WorldAnchor { get; init; } = default;
}

/// <summary>
/// Construction parameters of a prismatic joint: two bodies sliding along a
/// shared world axis through a world anchor, with optional slide stops and
/// a slide motor. Local anchors, axis and reference translation derive from
/// the world frame at creation.
/// </summary>
public readonly struct PrismaticDef
{
    /// <summary>Creates default parameters.</summary>
    public PrismaticDef()
    {
    }

    /// <summary>First body.</summary>
    public BodyId BodyA { get; init; } = BodyId.Invalid;

    /// <summary>Second body.</summary>
    public BodyId BodyB { get; init; } = BodyId.Invalid;

    /// <summary>Shared world anchor.</summary>
    public Vec2 WorldAnchor { get; init; } = default;

    /// <summary>Slide axis in world coordinates; normalized at creation.</summary>
    public Vec2 WorldAxis { get; init; } = new Vec2(1, 0);

    /// <summary>Lower slide stop along the axis.</summary>
    public float LowerTranslation
    {
        get => field;
        init
        {
            if (float.IsNaN(value))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(LowerTranslation));
            }

            field = value;
        }
    } = 0.0f;

    /// <summary>Upper slide stop along the axis.</summary>
    public float UpperTranslation
    {
        get => field;
        init
        {
            if (float.IsNaN(value))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(UpperTranslation));
            }

            field = value;
        }
    } = 0.0f;

    /// <summary>Whether slide stops apply.</summary>
    public bool EnableLimit { get; init; } = false;

    /// <summary>Target slide speed along the axis.</summary>
    public float MotorSpeed
    {
        get => field;
        init
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(MotorSpeed));
            }

            field = value;
        }
    } = 0.0f;

    /// <summary>Peak slide force; never negative.</summary>
    public float MaxMotorForce
    {
        get => field;
        init
        {
            if (value < 0.0f || float.IsNaN(value))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(MaxMotorForce));
            }

            field = value;
        }
    } = 0.0f;

    /// <summary>Whether the slide motor drives.</summary>
    public bool EnableMotor { get; init; } = false;
}

/// <summary>
/// Construction parameters of a motor joint: the second body is servoed
/// toward a target pose rigidly attached to the first body. Force and torque
/// caps bound the drive; the correction factor sets the response speed.
/// </summary>
public readonly struct MotorDef
{
    /// <summary>Creates default parameters.</summary>
    public MotorDef()
    {
    }

    /// <summary>First body; carries the target frame.</summary>
    public BodyId BodyA { get; init; } = BodyId.Invalid;

    /// <summary>Second body; servoed toward the target.</summary>
    public BodyId BodyB { get; init; } = BodyId.Invalid;

    /// <summary>Target offset of the second body origin in the first body frame.</summary>
    public Vec2 LinearOffset { get; init; } = default;

    /// <summary>Target relative angle in radians.</summary>
    public float AngularOffset { get; init; } = 0.0f;

    /// <summary>Peak servo force; never negative.</summary>
    public float MaxForce
    {
        get => field;
        init
        {
            if (value < 0.0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(MaxForce));
            }

            field = value;
        }
    } = 0.0f;

    /// <summary>Peak servo torque; never negative.</summary>
    public float MaxTorque
    {
        get => field;
        init
        {
            if (value < 0.0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(MaxTorque));
            }

            field = value;
        }
    } = 0.0f;

    /// <summary>Fraction of the pose error corrected per second; in (0, 1].</summary>
    public float CorrectionFactor
    {
        get => field;
        init
        {
            if (value <= 0.0f || value > 1.0f || float.IsNaN(value))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(CorrectionFactor));
            }

            field = value;
        }
    } = 0.3f;
}

/// <summary>
/// Construction parameters of a wheel joint: the second body hangs off the
/// first along a suspension axis with a spring, slides freely otherwise, and
/// spins under an optional hub motor. The rest position freezes at creation.
/// </summary>
public readonly struct WheelDef
{
    /// <summary>Creates default parameters.</summary>
    public WheelDef()
    {
    }

    /// <summary>First body; carries the suspension.</summary>
    public BodyId BodyA { get; init; } = BodyId.Invalid;

    /// <summary>Second body; the wheel.</summary>
    public BodyId BodyB { get; init; } = BodyId.Invalid;

    /// <summary>Shared world anchor.</summary>
    public Vec2 WorldAnchor { get; init; } = default;

    /// <summary>Suspension axis in world coordinates; normalized at creation.</summary>
    public Vec2 WorldAxis { get; init; } = new Vec2(0, 1);

    /// <summary>Target hub speed.</summary>
    public float MotorSpeed { get; init; } = 0.0f;

    /// <summary>Peak hub torque; never negative.</summary>
    public float MaxMotorTorque { get; init; } = 0.0f;

    /// <summary>Whether the hub motor drives.</summary>
    public bool EnableMotor { get; init; } = false;

    /// <summary>Suspension frequency in hertz; strictly positive, stable below 1 / (pi * dt).</summary>
    public float SuspensionFrequency
    {
        get => field;
        init
        {
            if (value <= 0.0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(SuspensionFrequency));
            }

            field = value;
        }
    } = 2.0f;

    /// <summary>Suspension damping ratio; never negative.</summary>
    public float SuspensionDamping
    {
        get => field;
        init
        {
            if (value < 0.0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(SuspensionDamping));
            }

            field = value;
        }
    } = 0.7f;
}

/// <summary>
/// Construction parameters of a pulley joint: two body anchors hung over two
/// fixed ground pulleys with a rope ratio. Rope lengths freeze at creation;
/// the weighted total stays fixed.
/// </summary>
public readonly struct PulleyDef
{
    /// <summary>Creates default parameters.</summary>
    public PulleyDef()
    {
    }

    /// <summary>First body.</summary>
    public BodyId BodyA { get; init; } = BodyId.Invalid;

    /// <summary>Second body.</summary>
    public BodyId BodyB { get; init; } = BodyId.Invalid;

    /// <summary>Fixed world pulley of the first rope.</summary>
    public Vec2 GroundAnchorA { get; init; } = default;

    /// <summary>Fixed world pulley of the second rope.</summary>
    public Vec2 GroundAnchorB { get; init; } = default;

    /// <summary>World anchor on the first body.</summary>
    public Vec2 WorldAnchorA { get; init; } = default;

    /// <summary>World anchor on the second body.</summary>
    public Vec2 WorldAnchorB { get; init; } = default;

    /// <summary>Rope length ratio; strictly positive.</summary>
    public float Ratio
    {
        get => field;
        init
        {
            if (value <= 0.0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(Ratio));
            }

            field = value;
        }
    } = 1.0f;
}

/// <summary>
/// Construction parameters of a gear joint: the coordinates of two live
/// revolute or prismatic joints (never gears) are locked in a ratio. The
/// coordinate sum freezes at creation.
/// </summary>
public readonly struct GearDef
{
    /// <summary>Creates default parameters.</summary>
    public GearDef()
    {
    }

    /// <summary>First geared joint; must be live, revolute or prismatic.</summary>
    public JointId JointA { get; init; } = JointId.Invalid;

    /// <summary>Second geared joint; must be live, revolute or prismatic.</summary>
    public JointId JointB { get; init; } = JointId.Invalid;

    /// <summary>Coordinate ratio of the second joint; finite and non-zero.</summary>
    public float Ratio
    {
        get => field;
        init
        {
            if (value == 0.0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(Ratio));
            }

            field = value;
        }
    } = 1.0f;
}

#endregion

#region Compile-Time Typestate Builders

#pragma warning disable CA1000 // Typestate factories live on the phased builder itself: the phase type parameter is the whole point, and a parallel factory class would split the construction protocol from its states.

/// <summary>Distance joint builder; each step returns the next phase.</summary>
/// <typeparam name="TPhase">The current configuration phase.</typeparam>
public readonly struct DistanceJointBuilder<TPhase>
{
    private readonly BodyId _bodyA;
    private readonly BodyId _bodyB;
    private readonly Vec2 _localAnchorA;
    private readonly Vec2 _localAnchorB;
    private readonly float _length;

    internal DistanceJointBuilder(BodyId bodyA, BodyId bodyB, Vec2 anchorA, Vec2 anchorB, float length)
    {
        _bodyA = bodyA;
        _bodyB = bodyB;
        _localAnchorA = anchorA;
        _localAnchorB = anchorB;
        _length = length;
    }

    /// <summary>Starts an unconfigured builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DistanceJointBuilder<Typestate.Unconfigured> Create() =>
        new(BodyId.Invalid, BodyId.Invalid, default, default, 0.0f);

    internal BodyId BodyA => _bodyA;
    internal BodyId BodyB => _bodyB;
    internal Vec2 LocalAnchorA => _localAnchorA;
    internal Vec2 LocalAnchorB => _localAnchorB;
    internal float Length => _length;
}

/// <summary>Phased extensions of the distance joint builder.</summary>
public static class DistanceJointBuilderExtensions
{
    /// <summary>Binds the two bodies; rejects shared or invalid identities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DistanceJointBuilder<Typestate.BodiesConfigured> WithBodies(
        this DistanceJointBuilder<Typestate.Unconfigured> _, BodyId bodyA, BodyId bodyB)
    {
        if (!bodyA.IsValid || !bodyB.IsValid || bodyA == bodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Distance joint requires two distinct valid bodies.");
        }

        return new DistanceJointBuilder<Typestate.BodiesConfigured>(bodyA, bodyB, default, default, 0.0f);
    }

    /// <summary>Binds the anchors and the rest length.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DistanceJointBuilder<Typestate.GeometryConfigured> WithAnchors(
        this DistanceJointBuilder<Typestate.BodiesConfigured> builder,
        Vec2 localAnchorA, Vec2 localAnchorB, float length)
    {
        if (length < 0.0f || float.IsNaN(length) || float.IsInfinity(length))
        {
            ThrowHelper.ThrowInvalidOperation("Rest length must be finite and non-negative.");
        }

        return new DistanceJointBuilder<Typestate.GeometryConfigured>(
            builder.BodyA, builder.BodyB, localAnchorA, localAnchorB, length);
    }

    /// <summary>Allocates the joint in the store.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JointId Build(
        this DistanceJointBuilder<Typestate.GeometryConfigured> builder,
        BodyStore bodies, JointStore store)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(store);
        return store.CreateDistance(bodies, builder.BodyA, builder.BodyB,
            builder.LocalAnchorA, builder.LocalAnchorB, builder.Length);
    }
}

/// <summary>Revolute joint builder; each step returns the next phase.</summary>
/// <typeparam name="TPhase">The current configuration phase.</typeparam>
public readonly struct RevoluteJointBuilder<TPhase>
{
    private readonly RevoluteDef _definition;

    internal RevoluteJointBuilder(in RevoluteDef definition)
    {
        _definition = definition;
    }

    /// <summary>Starts an unconfigured builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RevoluteJointBuilder<Typestate.Unconfigured> Create() => new(new RevoluteDef());

    internal RevoluteDef Definition => _definition;
}

/// <summary>Phased extensions of the revolute joint builder.</summary>
public static class RevoluteJointBuilderExtensions
{
    /// <summary>Binds the two bodies; rejects shared or invalid identities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RevoluteJointBuilder<Typestate.BodiesConfigured> WithBodies(
        this RevoluteJointBuilder<Typestate.Unconfigured> builder, BodyId bodyA, BodyId bodyB)
    {
        if (!bodyA.IsValid || !bodyB.IsValid || bodyA == bodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Revolute joint requires two distinct valid bodies.");
        }

        RevoluteDef definition = builder.Definition with { BodyA = bodyA, BodyB = bodyB };
        return new RevoluteJointBuilder<Typestate.BodiesConfigured>(in definition);
    }

    /// <summary>Binds the shared world anchor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RevoluteJointBuilder<Typestate.GeometryConfigured> WithAnchor(
        this RevoluteJointBuilder<Typestate.BodiesConfigured> builder, Vec2 worldAnchor)
    {
        RevoluteDef definition = builder.Definition with { WorldAnchor = worldAnchor };
        return new RevoluteJointBuilder<Typestate.GeometryConfigured>(in definition);
    }

    /// <summary>Binds the angular stops.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RevoluteJointBuilder<Typestate.GeometryConfigured> WithLimits(
        this RevoluteJointBuilder<Typestate.GeometryConfigured> builder, float lowerAngle, float upperAngle)
    {
        RevoluteDef definition = builder.Definition with
        {
            LowerAngle = lowerAngle,
            UpperAngle = upperAngle,
            EnableLimit = true,
        };
        return new RevoluteJointBuilder<Typestate.GeometryConfigured>(in definition);
    }

    /// <summary>Binds the speed motor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RevoluteJointBuilder<Typestate.GeometryConfigured> WithMotor(
        this RevoluteJointBuilder<Typestate.GeometryConfigured> builder, float speed, float maxTorque)
    {
        RevoluteDef definition = builder.Definition with
        {
            MotorSpeed = speed,
            MaxMotorTorque = maxTorque,
            EnableMotor = true,
        };
        return new RevoluteJointBuilder<Typestate.GeometryConfigured>(in definition);
    }

    /// <summary>Allocates the joint in the store.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JointId Build(
        this RevoluteJointBuilder<Typestate.GeometryConfigured> builder,
        BodyStore bodies, JointStore store)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(store);
        RevoluteDef definition = builder.Definition;
        return store.CreateRevolute(bodies, in definition);
    }
}

/// <summary>Weld joint builder; each step returns the next phase.</summary>
/// <typeparam name="TPhase">The current configuration phase.</typeparam>
public readonly struct WeldJointBuilder<TPhase>
{
    private readonly WeldDef _definition;

    internal WeldJointBuilder(in WeldDef definition)
    {
        _definition = definition;
    }

    /// <summary>Starts an unconfigured builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WeldJointBuilder<Typestate.Unconfigured> Create() => new(new WeldDef());

    internal WeldDef Definition => _definition;
}

/// <summary>Phased extensions of the weld joint builder.</summary>
public static class WeldJointBuilderExtensions
{
    /// <summary>Binds the two bodies; rejects shared or invalid identities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WeldJointBuilder<Typestate.BodiesConfigured> WithBodies(
        this WeldJointBuilder<Typestate.Unconfigured> builder, BodyId bodyA, BodyId bodyB)
    {
        if (!bodyA.IsValid || !bodyB.IsValid || bodyA == bodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Weld joint requires two distinct valid bodies.");
        }

        WeldDef definition = builder.Definition with { BodyA = bodyA, BodyB = bodyB };
        return new WeldJointBuilder<Typestate.BodiesConfigured>(in definition);
    }

    /// <summary>Binds the shared world anchor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WeldJointBuilder<Typestate.GeometryConfigured> WithAnchor(
        this WeldJointBuilder<Typestate.BodiesConfigured> builder, Vec2 worldAnchor)
    {
        WeldDef definition = builder.Definition with { WorldAnchor = worldAnchor };
        return new WeldJointBuilder<Typestate.GeometryConfigured>(in definition);
    }

    /// <summary>Allocates the joint in the store.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JointId Build(
        this WeldJointBuilder<Typestate.GeometryConfigured> builder,
        BodyStore bodies, JointStore store)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(store);
        WeldDef definition = builder.Definition;
        return store.CreateWeld(bodies, in definition);
    }
}

/// <summary>Prismatic joint builder; each step returns the next phase.</summary>
/// <typeparam name="TPhase">The current configuration phase.</typeparam>
public readonly struct PrismaticJointBuilder<TPhase>
{
    private readonly PrismaticDef _definition;

    internal PrismaticJointBuilder(in PrismaticDef definition)
    {
        _definition = definition;
    }

    /// <summary>Starts an unconfigured builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PrismaticJointBuilder<Typestate.Unconfigured> Create() => new(new PrismaticDef());

    internal PrismaticDef Definition => _definition;
}

/// <summary>Phased extensions of the prismatic joint builder.</summary>
public static class PrismaticJointBuilderExtensions
{
    /// <summary>Binds the two bodies; rejects shared or invalid identities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PrismaticJointBuilder<Typestate.BodiesConfigured> WithBodies(
        this PrismaticJointBuilder<Typestate.Unconfigured> builder, BodyId bodyA, BodyId bodyB)
    {
        if (!bodyA.IsValid || !bodyB.IsValid || bodyA == bodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Prismatic joint requires two distinct valid bodies.");
        }

        PrismaticDef definition = builder.Definition with { BodyA = bodyA, BodyB = bodyB };
        return new PrismaticJointBuilder<Typestate.BodiesConfigured>(in definition);
    }

    /// <summary>Binds the shared world anchor and the slide axis.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PrismaticJointBuilder<Typestate.GeometryConfigured> WithAxis(
        this PrismaticJointBuilder<Typestate.BodiesConfigured> builder, Vec2 worldAnchor, Vec2 worldAxis)
    {
        PrismaticDef definition = builder.Definition with { WorldAnchor = worldAnchor, WorldAxis = worldAxis };
        return new PrismaticJointBuilder<Typestate.GeometryConfigured>(in definition);
    }

    /// <summary>Binds the slide stops.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PrismaticJointBuilder<Typestate.GeometryConfigured> WithLimits(
        this PrismaticJointBuilder<Typestate.GeometryConfigured> builder, float lower, float upper)
    {
        PrismaticDef definition = builder.Definition with
        {
            LowerTranslation = lower,
            UpperTranslation = upper,
            EnableLimit = true,
        };
        return new PrismaticJointBuilder<Typestate.GeometryConfigured>(in definition);
    }

    /// <summary>Binds the slide motor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PrismaticJointBuilder<Typestate.GeometryConfigured> WithMotor(
        this PrismaticJointBuilder<Typestate.GeometryConfigured> builder, float speed, float maxForce)
    {
        PrismaticDef definition = builder.Definition with
        {
            MotorSpeed = speed,
            MaxMotorForce = maxForce,
            EnableMotor = true,
        };
        return new PrismaticJointBuilder<Typestate.GeometryConfigured>(in definition);
    }

    /// <summary>Allocates the joint in the store.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JointId Build(
        this PrismaticJointBuilder<Typestate.GeometryConfigured> builder,
        BodyStore bodies, JointStore store)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(store);
        PrismaticDef definition = builder.Definition;
        return store.CreatePrismatic(bodies, in definition);
    }
}

/// <summary>Motor joint builder; each step returns the next phase.</summary>
/// <typeparam name="TPhase">The current configuration phase.</typeparam>
public readonly struct MotorJointBuilder<TPhase>
{
    private readonly MotorDef _definition;

    internal MotorJointBuilder(in MotorDef definition)
    {
        _definition = definition;
    }

    /// <summary>Starts an unconfigured builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotorJointBuilder<Typestate.Unconfigured> Create() => new(new MotorDef());

    internal MotorDef Definition => _definition;
}

/// <summary>Phased extensions of the motor joint builder.</summary>
public static class MotorJointBuilderExtensions
{
    /// <summary>Binds the two bodies; rejects shared or invalid identities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotorJointBuilder<Typestate.BodiesConfigured> WithBodies(
        this MotorJointBuilder<Typestate.Unconfigured> builder, BodyId bodyA, BodyId bodyB)
    {
        if (!bodyA.IsValid || !bodyB.IsValid || bodyA == bodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Motor joint requires two distinct valid bodies.");
        }

        MotorDef definition = builder.Definition with { BodyA = bodyA, BodyB = bodyB };
        return new MotorJointBuilder<Typestate.BodiesConfigured>(in definition);
    }

    /// <summary>Binds the servo target, caps and response speed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotorJointBuilder<Typestate.GeometryConfigured> WithServo(
        this MotorJointBuilder<Typestate.BodiesConfigured> builder,
        Vec2 linearOffset, float angularOffset, float maxForce, float maxTorque, float correctionFactor)
    {
        MotorDef definition = builder.Definition with
        {
            LinearOffset = linearOffset,
            AngularOffset = angularOffset,
            MaxForce = maxForce,
            MaxTorque = maxTorque,
            CorrectionFactor = correctionFactor,
        };
        return new MotorJointBuilder<Typestate.GeometryConfigured>(in definition);
    }

    /// <summary>Allocates the joint in the store.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JointId Build(
        this MotorJointBuilder<Typestate.GeometryConfigured> builder,
        BodyStore bodies, JointStore store)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(store);
        MotorDef definition = builder.Definition;
        return store.CreateMotor(bodies, in definition);
    }
}

/// <summary>Wheel joint builder; each step returns the next phase.</summary>
/// <typeparam name="TPhase">The current configuration phase.</typeparam>
public readonly struct WheelJointBuilder<TPhase>
{
    private readonly WheelDef _definition;

    internal WheelJointBuilder(in WheelDef definition)
    {
        _definition = definition;
    }

    /// <summary>Starts an unconfigured builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WheelJointBuilder<Typestate.Unconfigured> Create() => new(new WheelDef());

    internal WheelDef Definition => _definition;
}

/// <summary>Phased extensions of the wheel joint builder.</summary>
public static class WheelJointBuilderExtensions
{
    /// <summary>Binds the two bodies; rejects shared or invalid identities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WheelJointBuilder<Typestate.BodiesConfigured> WithBodies(
        this WheelJointBuilder<Typestate.Unconfigured> builder, BodyId bodyA, BodyId bodyB)
    {
        if (!bodyA.IsValid || !bodyB.IsValid || bodyA == bodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Wheel joint requires two distinct valid bodies.");
        }

        WheelDef definition = builder.Definition with { BodyA = bodyA, BodyB = bodyB };
        return new WheelJointBuilder<Typestate.BodiesConfigured>(in definition);
    }

    /// <summary>Binds the suspension anchor, axis and spring tuning.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WheelJointBuilder<Typestate.GeometryConfigured> WithSuspension(
        this WheelJointBuilder<Typestate.BodiesConfigured> builder,
        Vec2 worldAnchor, Vec2 worldAxis, float frequency, float dampingRatio)
    {
        WheelDef definition = builder.Definition with
        {
            WorldAnchor = worldAnchor,
            WorldAxis = worldAxis,
            SuspensionFrequency = frequency,
            SuspensionDamping = dampingRatio,
        };
        return new WheelJointBuilder<Typestate.GeometryConfigured>(in definition);
    }

    /// <summary>Binds the hub motor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WheelJointBuilder<Typestate.GeometryConfigured> WithMotor(
        this WheelJointBuilder<Typestate.GeometryConfigured> builder, float speed, float maxTorque)
    {
        WheelDef definition = builder.Definition with
        {
            MotorSpeed = speed,
            MaxMotorTorque = maxTorque,
            EnableMotor = true,
        };
        return new WheelJointBuilder<Typestate.GeometryConfigured>(in definition);
    }

    /// <summary>Allocates the joint in the store.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JointId Build(
        this WheelJointBuilder<Typestate.GeometryConfigured> builder,
        BodyStore bodies, JointStore store)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(store);
        WheelDef definition = builder.Definition;
        return store.CreateWheel(bodies, in definition);
    }
}

/// <summary>Pulley joint builder; each step returns the next phase.</summary>
/// <typeparam name="TPhase">The current configuration phase.</typeparam>
public readonly struct PulleyJointBuilder<TPhase>
{
    private readonly PulleyDef _definition;

    internal PulleyJointBuilder(in PulleyDef definition)
    {
        _definition = definition;
    }

    /// <summary>Starts an unconfigured builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PulleyJointBuilder<Typestate.Unconfigured> Create() => new(new PulleyDef());

    internal PulleyDef Definition => _definition;
}

/// <summary>Phased extensions of the pulley joint builder.</summary>
public static class PulleyJointBuilderExtensions
{
    /// <summary>Binds the two bodies; rejects shared or invalid identities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PulleyJointBuilder<Typestate.BodiesConfigured> WithBodies(
        this PulleyJointBuilder<Typestate.Unconfigured> builder, BodyId bodyA, BodyId bodyB)
    {
        if (!bodyA.IsValid || !bodyB.IsValid || bodyA == bodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Pulley joint requires two distinct valid bodies.");
        }

        PulleyDef definition = builder.Definition with { BodyA = bodyA, BodyB = bodyB };
        return new PulleyJointBuilder<Typestate.BodiesConfigured>(in definition);
    }

    /// <summary>Binds the ground pulleys, the body anchors and the rope ratio.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PulleyJointBuilder<Typestate.GeometryConfigured> WithRopes(
        this PulleyJointBuilder<Typestate.BodiesConfigured> builder,
        Vec2 groundA, Vec2 groundB, Vec2 anchorA, Vec2 anchorB, float ratio)
    {
        PulleyDef definition = builder.Definition with
        {
            GroundAnchorA = groundA,
            GroundAnchorB = groundB,
            WorldAnchorA = anchorA,
            WorldAnchorB = anchorB,
            Ratio = ratio,
        };
        return new PulleyJointBuilder<Typestate.GeometryConfigured>(in definition);
    }

    /// <summary>Allocates the joint in the store.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JointId Build(
        this PulleyJointBuilder<Typestate.GeometryConfigured> builder,
        BodyStore bodies, JointStore store)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(store);
        PulleyDef definition = builder.Definition;
        return store.CreatePulley(bodies, in definition);
    }
}

/// <summary>Gear joint builder; each step returns the next phase.</summary>
/// <typeparam name="TPhase">The current configuration phase.</typeparam>
public readonly struct GearJointBuilder<TPhase>
{
    private readonly GearDef _definition;

    internal GearJointBuilder(in GearDef definition)
    {
        _definition = definition;
    }

    /// <summary>Starts an unconfigured builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GearJointBuilder<Typestate.Unconfigured> Create() => new(new GearDef());

    internal GearDef Definition => _definition;
}

/// <summary>Phased extensions of the gear joint builder.</summary>
public static class GearJointBuilderExtensions
{
    /// <summary>Binds the two geared joints; rejects shared, invalid or geared identities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GearJointBuilder<Typestate.JointsConfigured> WithJoints(
        this GearJointBuilder<Typestate.Unconfigured> builder, JointId jointA, JointId jointB)
    {
        if (!jointA.IsValid || !jointB.IsValid || jointA == jointB)
        {
            ThrowHelper.ThrowInvalidOperation("Gear joint requires two distinct live joints.");
        }

        GearDef definition = builder.Definition with { JointA = jointA, JointB = jointB };
        return new GearJointBuilder<Typestate.JointsConfigured>(in definition);
    }

    /// <summary>Binds the coordinate ratio.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GearJointBuilder<Typestate.GeometryConfigured> WithRatio(
        this GearJointBuilder<Typestate.JointsConfigured> builder, float ratio)
    {
        GearDef definition = builder.Definition with { Ratio = ratio };
        return new GearJointBuilder<Typestate.GeometryConfigured>(in definition);
    }

    /// <summary>Allocates the joint in the store.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JointId Build(
        this GearJointBuilder<Typestate.GeometryConfigured> builder,
        BodyStore bodies, JointStore store)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(store);
        GearDef definition = builder.Definition;
        return store.CreateGear(bodies, in definition);
    }
}

#pragma warning restore CA1000

#endregion

#region Monomorphic Joint Traits

#pragma warning disable CA1062 // Policy entry points run behind JointStorage sweeps owned by JointStore; JointSolver validates both stores once per pass, so per-sweep revalidation would pessimise the hot loop without covering new ground.

/// <summary>
/// Monomorphic constraint archetype: per-type solver entry points resolved at
/// compile time, so the sweep loop carries no virtual dispatch.
/// </summary>
/// <typeparam name="TSelf">The policy type itself.</typeparam>
/// <typeparam name="TData">The constraint storage layout.</typeparam>
public interface IJointPolicy<TSelf, TData>
    where TSelf : struct, IJointPolicy<TSelf, TData>
    where TData : struct
{
    /// <summary>The constraint archetype.</summary>
    static abstract JointKind Kind { get; }

    /// <summary>Reads the coordinated body pair for island union.</summary>
    static abstract void GetBodies(ref TData data, out BodyId bodyA, out BodyId bodyB);

    /// <summary>
    /// Per-step init: wakes sleeping dynamics on genuine relative motion and
    /// applies the stored impulse to velocities. Runs once per step before
    /// the iteration loop.
    /// </summary>
    static abstract void InitVelocity(
        ref TData data,
        BodyStore bodies,
        JointStore store,
        float wakeTolerance,
        float dt);

    /// <summary>Runs one sweep of velocity impulses.</summary>
    static abstract void SolveVelocity(
        ref TData data,
        BodyStore bodies,
        JointStore store);

    /// <summary>Runs one sweep of position correction.</summary>
    static abstract void SolvePosition(
        ref TData data,
        BodyStore bodies,
        JointStore store);
}

/// <summary>One distance constraint: two bodies pinned through body-frame anchors at a fixed rest length.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct DistanceJointData
{
    /// <summary>First body.</summary>
    public BodyId BodyA;

    /// <summary>Second body.</summary>
    public BodyId BodyB;

    /// <summary>Anchor in the first body frame.</summary>
    public Vec2 LocalAnchorA;

    /// <summary>Anchor in the second body frame.</summary>
    public Vec2 LocalAnchorB;

    /// <summary>Rest separation of the anchors; never negative.</summary>
    public float Length;

    /// <summary>Accumulated impulse; warm-started every step.</summary>
    public float Impulse;
}

/// <summary>Sequential-impulse distance constraint: bilateral impulses hold the rest length.</summary>
public readonly struct DistancePolicy : IJointPolicy<DistancePolicy, DistanceJointData>
{
    /// <inheritdoc/>
    public static JointKind Kind => JointKind.Distance;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBodies(ref DistanceJointData data, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = data.BodyA;
        bodyB = data.BodyB;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(
        ref DistanceJointData data, BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB);

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        if ((vB - vA).LengthSquared() > wakeTolerance * wakeTolerance)
        {
            if (stateA.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyA, true);
            }

            if (stateB.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyB, true);
            }
        }

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        Vec2 pA = stateA.Position + rA;
        Vec2 pB = stateB.Position + rB;
        Vec2 axis = pB - pA;
        Vec2 direction = axis.LengthSquared() > 1e-12f ? axis * (1.0f / MathF.Sqrt(axis.LengthSquared())) : Vec2.Zero;

        Vec2 impulse = direction * data.Impulse;
        if (awakeA)
        {
            stateA.LinearVelocity -= impulse * stateA.InverseMass;
            stateA.AngularVelocity -= stateA.InverseInertia * Vec2.Cross(rA, impulse);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += impulse * stateB.InverseMass;
            stateB.AngularVelocity += stateB.InverseInertia * Vec2.Cross(rB, impulse);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocity(ref DistanceJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, data.LocalAnchorA);
        Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
        Vec2 axis = pB - pA;
        float lengthSq = axis.LengthSquared();
        if (lengthSq < 1e-12f)
        {
            return;
        }

        Vec2 direction = axis * (1.0f / MathF.Sqrt(lengthSq));
        float rnA = Vec2.Cross(rA, direction);
        float rnB = Vec2.Cross(rB, direction);
        float k = invMassA + invMassB + (invIA * rnA * rnA) + (invIB * rnB * rnB);
        if (k <= 0.0f)
        {
            return;
        }

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        float velocityError = Vec2.Dot(direction, vB - vA);

        float impulse = -velocityError / k;
        data.Impulse += impulse;
        Vec2 applied = direction * impulse;

        if (awakeA)
        {
            stateA.LinearVelocity -= applied * invMassA;
            stateA.AngularVelocity -= invIA * Vec2.Cross(rA, applied);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += applied * invMassB;
            stateB.AngularVelocity += invIB * Vec2.Cross(rB, applied);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolvePosition(ref DistanceJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, data.LocalAnchorA);
        Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
        Vec2 axis = pB - pA;
        float length = MathF.Sqrt(axis.LengthSquared());
        if (length < 1e-9f)
        {
            return;
        }

        Vec2 direction = axis * (1.0f / length);
        float rnA = Vec2.Cross(rA, direction);
        float rnB = Vec2.Cross(rB, direction);
        float k = invMassA + invMassB + (invIA * rnA * rnA) + (invIB * rnB * rnB);
        if (k <= 0.0f)
        {
            return;
        }

        float error = Math.Clamp(length - data.Length, -SolverTuning.MaxCorrection, SolverTuning.MaxCorrection);
        Vec2 applied = direction * (-error / k);

        MathPhysics.ApplyPositionCorrection(ref stateA, ref stateB, rA, rB, applied, invMassA, invMassB, invIA, invIB, awakeA, awakeB);
    }
}

/// <summary>One pinned hinge with optional angular stops and a speed motor.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct RevoluteJointData
{
    /// <summary>First body.</summary>
    public BodyId BodyA;

    /// <summary>Second body.</summary>
    public BodyId BodyB;

    /// <summary>Anchor in the first body frame.</summary>
    public Vec2 LocalAnchorA;

    /// <summary>Anchor in the second body frame.</summary>
    public Vec2 LocalAnchorB;

    /// <summary>Reference angle (second minus first) at creation.</summary>
    public float ReferenceAngle;

    /// <summary>Lower angular stop in radians.</summary>
    public float LowerAngle;

    /// <summary>Upper angular stop in radians.</summary>
    public float UpperAngle;

    /// <summary>Target relative angular velocity.</summary>
    public float MotorSpeed;

    /// <summary>Peak motor torque; never negative.</summary>
    public float MaxMotorTorque;

    /// <summary>Accumulated point impulse; warm-started every step.</summary>
    public Vec2 PointImpulse;

    /// <summary>Accumulated motor impulse; warm-started every step.</summary>
    public float MotorImpulse;

    /// <summary>Accumulated limit impulse; warm-started every step.</summary>
    public float LimitImpulse;

    /// <summary>Motor impulse cap of the current step (torque times dt).</summary>
    public float MotorMaxImpulse;

    /// <summary>Resolved limit state of the current step.</summary>
    public LimitState LimitState;

    /// <summary>Whether angular stops apply.</summary>
    public bool EnableLimit;

    /// <summary>Whether the motor drives.</summary>
    public bool EnableMotor;
}

/// <summary>Pinned hinge: motor, then limit, then the 2x2 point block.</summary>
public readonly struct RevolutePolicy : IJointPolicy<RevolutePolicy, RevoluteJointData>
{
    /// <inheritdoc/>
    public static JointKind Kind => JointKind.Revolute;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBodies(ref RevoluteJointData data, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = data.BodyA;
        bodyB = data.BodyB;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(
        ref RevoluteJointData data, BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        float angleA = MathF.Atan2(stateA.Sin, stateA.Cos);
        float angleB = MathF.Atan2(stateB.Sin, stateB.Cos);
        float jointAngle = MathPhysics.NormalizeAngle((angleB - angleA) - data.ReferenceAngle);

        LimitState previous = data.LimitState;
        data.LimitState = MathPhysics.ResolveLimitState(data.EnableLimit, data.LowerAngle, data.UpperAngle, jointAngle, SolverTuning.AngularSlop);
        if (data.LimitState != previous)
        {
            data.LimitImpulse = 0.0f;
        }

        data.MotorMaxImpulse = data.EnableMotor ? data.MaxMotorTorque * dt : 0.0f;

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        if ((vB - vA).LengthSquared() > wakeTolerance * wakeTolerance ||
            MathF.Abs(stateB.AngularVelocity - stateA.AngularVelocity) > wakeTolerance)
        {
            if (stateA.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyA, true);
            }

            if (stateB.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyB, true);
            }
        }

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        Vec2 pointImpulse = data.PointImpulse;
        float totalTorque = data.MotorImpulse + data.LimitImpulse;
        if (awakeA)
        {
            stateA.LinearVelocity -= pointImpulse * stateA.InverseMass;
            stateA.AngularVelocity -= stateA.InverseInertia * (Vec2.Cross(rA, pointImpulse) + totalTorque);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += pointImpulse * stateB.InverseMass;
            stateB.AngularVelocity += stateB.InverseInertia * (Vec2.Cross(rB, pointImpulse) + totalTorque);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocity(ref RevoluteJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;
        float axialMass = 1.0f / (invIA + invIB);

        if (data.EnableMotor && (awakeA || awakeB))
        {
            float rate = (stateB.AngularVelocity - stateA.AngularVelocity) - data.MotorSpeed;
            float impulse = -axialMass * rate;
            float old = data.MotorImpulse;
            float next = Math.Clamp(old + impulse, -data.MotorMaxImpulse, data.MotorMaxImpulse);
            float delta = next - old;
            data.MotorImpulse = next;

            if (awakeA)
            {
                stateA.AngularVelocity -= invIA * delta;
            }

            if (awakeB)
            {
                stateB.AngularVelocity += invIB * delta;
            }
        }

        if (data.LimitState != LimitState.Inactive && (awakeA || awakeB))
        {
            float rate = stateB.AngularVelocity - stateA.AngularVelocity;
            float impulse = -axialMass * rate;
            float old = data.LimitImpulse;
            float next = data.LimitState switch
            {
                LimitState.AtLower => MathF.Max(old + impulse, 0.0f),
                LimitState.AtUpper => MathF.Min(old + impulse, 0.0f),
                _ => old + impulse,
            };
            float delta = next - old;
            data.LimitImpulse = next;

            if (awakeA)
            {
                stateA.AngularVelocity -= invIA * delta;
            }

            if (awakeB)
            {
                stateB.AngularVelocity += invIB * delta;
            }
        }

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        float k11 = invMassA + invMassB + (invIA * rA.Y * rA.Y) + (invIB * rB.Y * rB.Y);
        float k22 = invMassA + invMassB + (invIA * rA.X * rA.X) + (invIB * rB.X * rB.X);
        float k12 = (invIA * rA.X * rA.Y) - (invIB * rB.X * rB.Y);
        float det = (k11 * k22) - (k12 * k12);
        if (det <= 0.0f)
        {
            return;
        }

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        Vec2 accumulated = data.PointImpulse;
        Vec2 residual = (vB - vA) - new Vec2(
            (k11 * accumulated.X) + (k12 * accumulated.Y),
            (k12 * accumulated.X) + (k22 * accumulated.Y));

        float invDet = 1.0f / det;
        Vec2 solved = new(
            ((k12 * residual.Y) - (k22 * residual.X)) * invDet,
            ((k12 * residual.X) - (k11 * residual.Y)) * invDet);
        Vec2 deltaImpulse = solved - accumulated;
        data.PointImpulse = solved;

        if (awakeA)
        {
            stateA.LinearVelocity -= deltaImpulse * invMassA;
            stateA.AngularVelocity -= invIA * Vec2.Cross(rA, deltaImpulse);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += deltaImpulse * invMassB;
            stateB.AngularVelocity += invIB * Vec2.Cross(rB, deltaImpulse);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolvePosition(ref RevoluteJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        float k11 = invMassA + invMassB + (invIA * rA.Y * rA.Y) + (invIB * rB.Y * rB.Y);
        float k22 = invMassA + invMassB + (invIA * rA.X * rA.X) + (invIB * rB.X * rB.X);
        float k12 = (invIA * rA.X * rA.Y) - (invIB * rB.X * rB.Y);
        float det = (k11 * k22) - (k12 * k12);

        if (det > 0.0f)
        {
            Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, data.LocalAnchorA);
            Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
            Vec2 error = pB - pA;
            float invDet = 1.0f / det;
            Vec2 correction = new(
                ((k12 * error.Y) - (k22 * error.X)) * invDet,
                ((k12 * error.X) - (k11 * error.Y)) * invDet);
            MathPhysics.ApplyPositionCorrection(ref stateA, ref stateB, rA, rB, correction, invMassA, invMassB, invIA, invIB, awakeA, awakeB);
        }

        if (data.LimitState == LimitState.Inactive)
        {
            return;
        }

        float axialMass = 1.0f / (invIA + invIB);
        float angleA = MathF.Atan2(stateA.Sin, stateA.Cos);
        float angleB = MathF.Atan2(stateB.Sin, stateB.Cos);
        float jointAngle = MathPhysics.NormalizeAngle((angleB - angleA) - data.ReferenceAngle);

        float angularError = data.LimitState switch
        {
            LimitState.AtLower => jointAngle - data.LowerAngle + SolverTuning.AngularSlop,
            LimitState.AtUpper => jointAngle - data.UpperAngle - SolverTuning.AngularSlop,
            _ => jointAngle - data.LowerAngle,
        };

        bool violates = data.LimitState switch
        {
            LimitState.AtLower => angularError < 0.0f,
            LimitState.AtUpper => angularError > 0.0f,
            _ => angularError != 0.0f,
        };

        if (!violates)
        {
            return;
        }

        float limitCorrection = -axialMass * angularError;
        if (awakeA)
        {
            float correctedA = MathF.Atan2(stateA.Sin, stateA.Cos) - (invIA * limitCorrection);
            stateA.Cos = MathF.Cos(correctedA);
            stateA.Sin = MathF.Sin(correctedA);
        }

        if (awakeB)
        {
            float correctedB = MathF.Atan2(stateB.Sin, stateB.Cos) + (invIB * limitCorrection);
            stateB.Cos = MathF.Cos(correctedB);
            stateB.Sin = MathF.Sin(correctedB);
        }
    }
}

/// <summary>One locked relative pose: pin block plus angle lock.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct WeldJointData
{
    /// <summary>First body.</summary>
    public BodyId BodyA;

    /// <summary>Second body.</summary>
    public BodyId BodyB;

    /// <summary>Anchor in the first body frame.</summary>
    public Vec2 LocalAnchorA;

    /// <summary>Anchor in the second body frame.</summary>
    public Vec2 LocalAnchorB;

    /// <summary>Reference angle (second minus first) at creation.</summary>
    public float ReferenceAngle;

    /// <summary>Accumulated point impulse; warm-started every step.</summary>
    public Vec2 PointImpulse;

    /// <summary>Accumulated angle impulse; warm-started every step.</summary>
    public float WeldAngleImpulse;
}

/// <summary>Locked pose: angle lock, then the shared pin block.</summary>
public readonly struct WeldPolicy : IJointPolicy<WeldPolicy, WeldJointData>
{
    /// <inheritdoc/>
    public static JointKind Kind => JointKind.Weld;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBodies(ref WeldJointData data, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = data.BodyA;
        bodyB = data.BodyB;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(
        ref WeldJointData data, BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        if ((vB - vA).LengthSquared() > wakeTolerance * wakeTolerance ||
            MathF.Abs(stateB.AngularVelocity - stateA.AngularVelocity) > wakeTolerance)
        {
            if (stateA.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyA, true);
            }

            if (stateB.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyB, true);
            }
        }

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        Vec2 pointImpulse = data.PointImpulse;
        if (awakeA)
        {
            stateA.LinearVelocity -= pointImpulse * stateA.InverseMass;
            stateA.AngularVelocity -= stateA.InverseInertia * (Vec2.Cross(rA, pointImpulse) + data.WeldAngleImpulse);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += pointImpulse * stateB.InverseMass;
            stateB.AngularVelocity += stateB.InverseInertia * (Vec2.Cross(rB, pointImpulse) + data.WeldAngleImpulse);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocity(ref WeldJointData data, BodyStore bodies, JointStore store)
    {
        SolveAngleLock(bodies, ref data);
        SolvePinBlockVelocity(bodies, ref data);
    }

    /// <summary>Shared angle velocity lock used by weld joints.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void SolveAngleLock(BodyStore bodies, ref WeldJointData data)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;
        float axialMass = invIA + invIB;
        if (axialMass <= 0.0f || (!awakeA && !awakeB))
        {
            return;
        }

        float delta = -(stateB.AngularVelocity - stateA.AngularVelocity) / axialMass;
        data.WeldAngleImpulse += delta;

        if (awakeA)
        {
            stateA.AngularVelocity -= invIA * delta;
        }

        if (awakeB)
        {
            stateB.AngularVelocity += invIB * delta;
        }
    }

    /// <summary>Shared 2x2 pin velocity block used by weld joints.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void SolvePinBlockVelocity(BodyStore bodies, ref WeldJointData data)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        float k11 = invMassA + invMassB + (invIA * rA.Y * rA.Y) + (invIB * rB.Y * rB.Y);
        float k22 = invMassA + invMassB + (invIA * rA.X * rA.X) + (invIB * rB.X * rB.X);
        float k12 = (invIA * rA.X * rA.Y) - (invIB * rB.X * rB.Y);
        float det = (k11 * k22) - (k12 * k12);
        if (det <= 0.0f)
        {
            return;
        }

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        Vec2 accumulated = data.PointImpulse;
        Vec2 residual = (vB - vA) - new Vec2(
            (k11 * accumulated.X) + (k12 * accumulated.Y),
            (k12 * accumulated.X) + (k22 * accumulated.Y));

        float invDet = 1.0f / det;
        Vec2 solved = new(
            ((k12 * residual.Y) - (k22 * residual.X)) * invDet,
            ((k12 * residual.X) - (k11 * residual.Y)) * invDet);
        Vec2 deltaImpulse = solved - accumulated;
        data.PointImpulse = solved;

        if (awakeA)
        {
            stateA.LinearVelocity -= deltaImpulse * invMassA;
            stateA.AngularVelocity -= invIA * Vec2.Cross(rA, deltaImpulse);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += deltaImpulse * invMassB;
            stateB.AngularVelocity += invIB * Vec2.Cross(rB, deltaImpulse);
        }
    }

    /// <summary>Shared pin position correction used by weld joints.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void SolvePinCorrection(
        BodyStore bodies, BodyId bodyA, BodyId bodyB, Vec2 localAnchorA, Vec2 localAnchorB)
    {
        ref BodyState stateA = ref bodies.Slot(bodyA);
        ref BodyState stateB = ref bodies.Slot(bodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 rA = MathPhysics.Rotate(stateA, localAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, localAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        float k11 = invMassA + invMassB + (invIA * rA.Y * rA.Y) + (invIB * rB.Y * rB.Y);
        float k22 = invMassA + invMassB + (invIA * rA.X * rA.X) + (invIB * rB.X * rB.X);
        float k12 = (invIA * rA.X * rA.Y) - (invIB * rB.X * rB.Y);
        float det = (k11 * k22) - (k12 * k12);
        if (det <= 0.0f)
        {
            return;
        }

        Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, localAnchorA);
        Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, localAnchorB);
        Vec2 error = pB - pA;

        float invDet = 1.0f / det;
        Vec2 correction = new(
            ((k12 * error.Y) - (k22 * error.X)) * invDet,
            ((k12 * error.X) - (k11 * error.Y)) * invDet);

        MathPhysics.ApplyPositionCorrection(ref stateA, ref stateB, rA, rB, correction, invMassA, invMassB, invIA, invIB, awakeA, awakeB);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolvePosition(ref WeldJointData data, BodyStore bodies, JointStore store)
    {
        SolvePinCorrection(bodies, data.BodyA, data.BodyB, data.LocalAnchorA, data.LocalAnchorB);

        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;
        float axialMass = invIA + invIB;
        if (axialMass <= 0.0f)
        {
            return;
        }

        float angleA = MathF.Atan2(stateA.Sin, stateA.Cos);
        float angleB = MathF.Atan2(stateB.Sin, stateB.Cos);
        float angleError = MathPhysics.NormalizeAngle((angleB - angleA) - data.ReferenceAngle);
        if (MathF.Abs(angleError) < SolverTuning.AngularSlop)
        {
            return;
        }

        float correction = -angleError / axialMass;
        if (awakeA)
        {
            float correctedA = angleA - (invIA * correction);
            stateA.Cos = MathF.Cos(correctedA);
            stateA.Sin = MathF.Sin(correctedA);
        }

        if (awakeB)
        {
            float correctedB = angleB + (invIB * correction);
            stateB.Cos = MathF.Cos(correctedB);
            stateB.Sin = MathF.Sin(correctedB);
        }
    }
}

/// <summary>One slider along an axis with optional stops, motor and angle lock.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PrismaticJointData
{
    /// <summary>First body.</summary>
    public BodyId BodyA;

    /// <summary>Second body.</summary>
    public BodyId BodyB;

    /// <summary>Anchor in the first body frame.</summary>
    public Vec2 LocalAnchorA;

    /// <summary>Anchor in the second body frame.</summary>
    public Vec2 LocalAnchorB;

    /// <summary>Slide axis in the first body frame.</summary>
    public Vec2 LocalAxisA;

    /// <summary>Reference angle (second minus first) at creation.</summary>
    public float ReferenceAngle;

    /// <summary>Along-axis separation at creation; the slide zero.</summary>
    public float ReferenceTranslation;

    /// <summary>Lower slide stop along the axis.</summary>
    public float LowerTranslation;

    /// <summary>Upper slide stop along the axis.</summary>
    public float UpperTranslation;

    /// <summary>Target slide speed along the axis.</summary>
    public float MotorSpeed;

    /// <summary>Peak slide force; never negative.</summary>
    public float MaxMotorForce;

    /// <summary>Accumulated lateral impulse (.Y); the axis stays free except limit/motor.</summary>
    public Vec2 PrismaticImpulse;

    /// <summary>Accumulated slide motor impulse; warm-started every step.</summary>
    public float MotorImpulse;

    /// <summary>Accumulated slide limit impulse; warm-started every step.</summary>
    public float LimitImpulse;

    /// <summary>Slide motor impulse cap of the current step (force times dt).</summary>
    public float MotorMaxImpulse;

    /// <summary>Accumulated angle impulse; warm-started every step.</summary>
    public float WeldAngleImpulse;

    /// <summary>Resolved slide limit state of the current step.</summary>
    public LimitState LimitState;

    /// <summary>Whether slide stops apply.</summary>
    public bool EnableLimit;

    /// <summary>Whether the slide motor drives.</summary>
    public bool EnableMotor;
}

/// <summary>Slider: motor, then limit, then lateral lock, then angle lock.</summary>
public readonly struct PrismaticPolicy : IJointPolicy<PrismaticPolicy, PrismaticJointData>
{
    /// <inheritdoc/>
    public static JointKind Kind => JointKind.Prismatic;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBodies(ref PrismaticJointData data, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = data.BodyA;
        bodyB = data.BodyB;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(
        ref PrismaticJointData data, BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        Vec2 axis = MathPhysics.Rotate(stateA, data.LocalAxisA);
        float axisLenSq = axis.LengthSquared();
        if (axisLenSq > 1e-12f)
        {
            axis *= 1.0f / MathF.Sqrt(axisLenSq);
        }

        Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, data.LocalAnchorA);
        Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
        float translation = Vec2.Dot(pB - pA, axis) - data.ReferenceTranslation;

        LimitState previous = data.LimitState;
        data.LimitState = MathPhysics.ResolveLimitState(data.EnableLimit, data.LowerTranslation, data.UpperTranslation, translation, SolverTuning.AngularSlop);
        if (data.LimitState != previous)
        {
            data.LimitImpulse = 0.0f;
        }

        data.MotorMaxImpulse = data.EnableMotor ? data.MaxMotorForce * dt : 0.0f;

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        if ((vB - vA).LengthSquared() > wakeTolerance * wakeTolerance ||
            MathF.Abs(stateB.AngularVelocity - stateA.AngularVelocity) > wakeTolerance)
        {
            if (stateA.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyA, true);
            }

            if (stateB.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyB, true);
            }
        }

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        Vec2 perp = MathPhysics.Perp(axis);
        Vec2 impulse = (axis * (data.MotorImpulse + data.LimitImpulse)) + (perp * data.PrismaticImpulse.Y);
        if (awakeA)
        {
            stateA.LinearVelocity -= impulse * stateA.InverseMass;
            stateA.AngularVelocity -= stateA.InverseInertia * (Vec2.Cross(rA, impulse) + data.WeldAngleImpulse);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += impulse * stateB.InverseMass;
            stateB.AngularVelocity += stateB.InverseInertia * (Vec2.Cross(rB, impulse) + data.WeldAngleImpulse);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocity(ref PrismaticJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 axis = MathPhysics.Rotate(stateA, data.LocalAxisA);
        if (axis.LengthSquared() > 1e-12f)
        {
            axis *= 1.0f / MathF.Sqrt(axis.LengthSquared());
        }

        Vec2 perp = MathPhysics.Perp(axis);
        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        float axisA = Vec2.Cross(rA, axis);
        float axisB = Vec2.Cross(rB, axis);
        float axisMass = invMassA + invMassB + (invIA * axisA * axisA) + (invIB * axisB * axisB);

        if (data.EnableMotor && axisMass > 0.0f && (awakeA || awakeB))
        {
            Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
            Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
            float rate = Vec2.Dot(vB - vA, axis) - data.MotorSpeed;
            float impulse = -rate / axisMass;
            float old = data.MotorImpulse;
            float next = Math.Clamp(old + impulse, -data.MotorMaxImpulse, data.MotorMaxImpulse);
            Vec2 applied = axis * (next - old);
            data.MotorImpulse = next;

            if (awakeA)
            {
                stateA.LinearVelocity -= applied * invMassA;
                stateA.AngularVelocity -= invIA * Vec2.Cross(rA, applied);
            }

            if (awakeB)
            {
                stateB.LinearVelocity += applied * invMassB;
                stateB.AngularVelocity += invIB * Vec2.Cross(rB, applied);
            }
        }

        if (data.LimitState != LimitState.Inactive && axisMass > 0.0f && (awakeA || awakeB))
        {
            Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
            Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
            float rate = Vec2.Dot(vB - vA, axis);
            float impulse = -rate / axisMass;
            float old = data.LimitImpulse;
            float next = data.LimitState switch
            {
                LimitState.AtLower => MathF.Max(old + impulse, 0.0f),
                LimitState.AtUpper => MathF.Min(old + impulse, 0.0f),
                _ => old + impulse,
            };
            Vec2 applied = axis * (next - old);
            data.LimitImpulse = next;

            if (awakeA)
            {
                stateA.LinearVelocity -= applied * invMassA;
                stateA.AngularVelocity -= invIA * Vec2.Cross(rA, applied);
            }

            if (awakeB)
            {
                stateB.LinearVelocity += applied * invMassB;
                stateB.AngularVelocity += invIB * Vec2.Cross(rB, applied);
            }
        }

        float perpA = Vec2.Cross(rA, perp);
        float perpB = Vec2.Cross(rB, perp);
        float perpMass = invMassA + invMassB + (invIA * perpA * perpA) + (invIB * perpB * perpB);
        if (perpMass > 0.0f && (awakeA || awakeB))
        {
            Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
            Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
            float rate = Vec2.Dot(vB - vA, perp);
            float delta = -rate / perpMass;
            data.PrismaticImpulse = new Vec2(data.PrismaticImpulse.X, data.PrismaticImpulse.Y + delta);
            Vec2 applied = perp * delta;

            if (awakeA)
            {
                stateA.LinearVelocity -= applied * invMassA;
                stateA.AngularVelocity -= invIA * Vec2.Cross(rA, applied);
            }

            if (awakeB)
            {
                stateB.LinearVelocity += applied * invMassB;
                stateB.AngularVelocity += invIB * Vec2.Cross(rB, applied);
            }
        }

        float axialMass = invIA + invIB;
        if (axialMass > 0.0f && (awakeA || awakeB))
        {
            float rate = stateB.AngularVelocity - stateA.AngularVelocity;
            float delta = -rate / axialMass;
            data.WeldAngleImpulse += delta;

            if (awakeA)
            {
                stateA.AngularVelocity -= invIA * delta;
            }

            if (awakeB)
            {
                stateB.AngularVelocity += invIB * delta;
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolvePosition(ref PrismaticJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 axis = MathPhysics.Rotate(stateA, data.LocalAxisA);
        if (axis.LengthSquared() > 1e-12f)
        {
            axis *= 1.0f / MathF.Sqrt(axis.LengthSquared());
        }

        Vec2 perp = MathPhysics.Perp(axis);
        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);
        Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, data.LocalAnchorA);
        Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
        Vec2 error = pB - pA;

        float perpA = Vec2.Cross(rA, perp);
        float perpB = Vec2.Cross(rB, perp);
        float perpK = invMassA + invMassB + (invIA * perpA * perpA) + (invIB * perpB * perpB);
        if (perpK > 0.0f)
        {
            float perpError = Math.Clamp(Vec2.Dot(error, perp), -SolverTuning.MaxCorrection, SolverTuning.MaxCorrection);
            Vec2 applied = perp * (-perpError / perpK);
            MathPhysics.ApplyPositionCorrection(ref stateA, ref stateB, rA, rB, applied, invMassA, invMassB, invIA, invIB, awakeA, awakeB);
        }

        float axialMass = invIA + invIB;
        if (axialMass > 0.0f)
        {
            float angleA = MathF.Atan2(stateA.Sin, stateA.Cos);
            float angleB = MathF.Atan2(stateB.Sin, stateB.Cos);
            float angleError = MathPhysics.NormalizeAngle((angleB - angleA) - data.ReferenceAngle);
            if (MathF.Abs(angleError) >= SolverTuning.AngularSlop)
            {
                float correction = -angleError / axialMass;
                if (awakeA)
                {
                    float correctedA = angleA - (invIA * correction);
                    stateA.Cos = MathF.Cos(correctedA);
                    stateA.Sin = MathF.Sin(correctedA);
                }

                if (awakeB)
                {
                    float correctedB = angleB + (invIB * correction);
                    stateB.Cos = MathF.Cos(correctedB);
                    stateB.Sin = MathF.Sin(correctedB);
                }
            }
        }

        if (data.LimitState == LimitState.Inactive)
        {
            return;
        }

        float axisA = Vec2.Cross(rA, axis);
        float axisB = Vec2.Cross(rB, axis);
        float axisK = invMassA + invMassB + (invIA * axisA * axisA) + (invIB * axisB * axisB);
        if (axisK <= 0.0f)
        {
            return;
        }

        float translation = Vec2.Dot(error, axis) - data.ReferenceTranslation;
        float limitError = data.LimitState switch
        {
            LimitState.AtLower => translation - data.LowerTranslation + SolverTuning.LinearSlop,
            LimitState.AtUpper => translation - data.UpperTranslation - SolverTuning.LinearSlop,
            _ => translation - data.LowerTranslation,
        };

        bool violates = data.LimitState switch
        {
            LimitState.AtLower => limitError < 0.0f,
            LimitState.AtUpper => limitError > 0.0f,
            _ => limitError != 0.0f,
        };

        if (!violates)
        {
            return;
        }

        Vec2 limitApplied = axis * (-Math.Clamp(limitError, -SolverTuning.MaxCorrection, SolverTuning.MaxCorrection) / axisK);
        MathPhysics.ApplyPositionCorrection(ref stateA, ref stateB, rA, rB, limitApplied, invMassA, invMassB, invIA, invIB, awakeA, awakeB);
    }
}

/// <summary>One servo target: capped force and torque toward a moving frame.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct MotorJointData
{
    /// <summary>First body; carries the target frame.</summary>
    public BodyId BodyA;

    /// <summary>Second body; servoed toward the target.</summary>
    public BodyId BodyB;

    /// <summary>Target offset in the first body frame.</summary>
    public Vec2 ServoOffset;

    /// <summary>Target relative angle.</summary>
    public float ServoAngle;

    /// <summary>Peak servo force; never negative.</summary>
    public float ServoMaxForce;

    /// <summary>Peak servo torque; never negative.</summary>
    public float ServoMaxTorque;

    /// <summary>Fraction of the pose error corrected per second; in (0, 1].</summary>
    public float ServoFactor;

    /// <summary>Servo response rate of the current step (factor over dt).</summary>
    public float ServoRate;

    /// <summary>Accumulated servo force impulse; warm-started every step.</summary>
    public Vec2 ServoLinearImpulse;

    /// <summary>Accumulated servo torque impulse; warm-started every step.</summary>
    public float ServoAngleImpulse;

    /// <summary>Servo force impulse cap of the current step.</summary>
    public float ServoMaxLinearImpulse;

    /// <summary>Servo torque impulse cap of the current step.</summary>
    public float ServoMaxAngleImpulse;
}

/// <summary>Capped P-servo toward the target pose; the error is re-derived every step.</summary>
public readonly struct MotorPolicy : IJointPolicy<MotorPolicy, MotorJointData>
{
    /// <inheritdoc/>
    public static JointKind Kind => JointKind.Motor;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBodies(ref MotorJointData data, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = data.BodyA;
        bodyB = data.BodyB;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(
        ref MotorJointData data, BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        data.ServoMaxLinearImpulse = data.ServoMaxForce * dt;
        data.ServoMaxAngleImpulse = data.ServoMaxTorque * dt;
        data.ServoRate = data.ServoFactor / dt;

        Vec2 target = stateA.Position + MathPhysics.Rotate(stateA, data.ServoOffset);
        Vec2 error = target - stateB.Position;
        if (error.LengthSquared() > wakeTolerance * wakeTolerance ||
            MathF.Abs(stateB.AngularVelocity - stateA.AngularVelocity) > wakeTolerance)
        {
            if (stateA.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyA, true);
            }

            if (stateB.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyB, true);
            }
        }

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        if (awakeA)
        {
            stateA.LinearVelocity -= data.ServoLinearImpulse * stateA.InverseMass;
            stateA.AngularVelocity -= stateA.InverseInertia * data.ServoAngleImpulse;
        }

        if (awakeB)
        {
            stateB.LinearVelocity += data.ServoLinearImpulse * stateB.InverseMass;
            stateB.AngularVelocity += stateB.InverseInertia * data.ServoAngleImpulse;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocity(ref MotorJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        float linearMass = invMassA + invMassB;
        if (linearMass > 0.0f)
        {
            Vec2 target = stateA.Position + MathPhysics.Rotate(stateA, data.ServoOffset);
            Vec2 desired = (target - stateB.Position) * data.ServoRate;
            Vec2 relative = stateB.LinearVelocity - stateA.LinearVelocity;
            Vec2 impulse = (desired - relative) * (1.0f / linearMass);
            Vec2 next = data.ServoLinearImpulse + impulse;
            float cap = data.ServoMaxLinearImpulse;
            if (next.LengthSquared() > cap * cap)
            {
                next *= cap / MathF.Sqrt(next.LengthSquared());
            }

            Vec2 delta = next - data.ServoLinearImpulse;
            data.ServoLinearImpulse = next;

            if (awakeA)
            {
                stateA.LinearVelocity -= delta * invMassA;
            }

            if (awakeB)
            {
                stateB.LinearVelocity += delta * invMassB;
            }
        }

        float axialMass = invIA + invIB;
        if (axialMass > 0.0f)
        {
            float angleA = MathF.Atan2(stateA.Sin, stateA.Cos);
            float angleB = MathF.Atan2(stateB.Sin, stateB.Cos);
            float error = MathPhysics.NormalizeAngle((angleA + data.ServoAngle) - angleB);
            float desiredRate = (error * data.ServoRate) - (stateB.AngularVelocity - stateA.AngularVelocity);
            float impulse = desiredRate / axialMass;
            float next = Math.Clamp(
                data.ServoAngleImpulse + impulse,
                -data.ServoMaxAngleImpulse, data.ServoMaxAngleImpulse);
            float delta = next - data.ServoAngleImpulse;
            data.ServoAngleImpulse = next;

            if (awakeA)
            {
                stateA.AngularVelocity -= invIA * delta;
            }

            if (awakeB)
            {
                stateB.AngularVelocity += invIB * delta;
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SolvePosition(ref MotorJointData data, BodyStore bodies, JointStore store)
    {
        // Servo recomputes from pose each step; position correction is unconstrained.
    }
}

/// <summary>One sprung suspension with a free-spinning hub and a lateral lock.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct WheelJointData
{
    /// <summary>First body; carries the suspension.</summary>
    public BodyId BodyA;

    /// <summary>Second body; the wheel.</summary>
    public BodyId BodyB;

    /// <summary>Anchor in the first body frame.</summary>
    public Vec2 LocalAnchorA;

    /// <summary>Anchor in the second body frame.</summary>
    public Vec2 LocalAnchorB;

    /// <summary>Suspension axis in the first body frame.</summary>
    public Vec2 LocalAxisA;

    /// <summary>Along-axis separation at creation; the spring rest length.</summary>
    public float ReferenceTranslation;

    /// <summary>Suspension frequency in hertz.</summary>
    public float SuspensionFrequency;

    /// <summary>Suspension damping ratio.</summary>
    public float SuspensionDamping;

    /// <summary>Target hub speed.</summary>
    public float MotorSpeed;

    /// <summary>Peak hub torque; never negative.</summary>
    public float MaxMotorTorque;

    /// <summary>Accumulated hub motor impulse; warm-started every step.</summary>
    public float MotorImpulse;

    /// <summary>Hub motor impulse cap of the current step.</summary>
    public float MotorMaxImpulse;

    /// <summary>Accumulated lateral impulse; warm-started every step.</summary>
    public float PerpImpulse;

    /// <summary>Whether the hub motor drives.</summary>
    public bool EnableMotor;
}

/// <summary>Suspension: explicit spring force in init, hub motor plus lateral lock in the sweep.</summary>
public readonly struct WheelPolicy : IJointPolicy<WheelPolicy, WheelJointData>
{
    /// <inheritdoc/>
    public static JointKind Kind => JointKind.Wheel;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBodies(ref WheelJointData data, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = data.BodyA;
        bodyB = data.BodyB;
    }

    /// <summary>Shared suspension frame: axis, lateral, COM-relative arms and anchor points.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Frame(
        in BodyState stateA, in BodyState stateB,
        Vec2 localAnchorA, Vec2 localAnchorB, Vec2 localAxisA,
        out Vec2 axis, out Vec2 perp, out Vec2 rA, out Vec2 rB, out Vec2 pA, out Vec2 pB)
    {
        axis = MathPhysics.Rotate(stateA, localAxisA);
        if (axis.LengthSquared() > 1e-12f)
        {
            axis *= 1.0f / MathF.Sqrt(axis.LengthSquared());
        }

        perp = MathPhysics.Perp(axis);
        rA = MathPhysics.Rotate(stateA, localAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        rB = MathPhysics.Rotate(stateB, localAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);
        pA = stateA.Position + MathPhysics.Rotate(stateA, localAnchorA);
        pB = stateB.Position + MathPhysics.Rotate(stateB, localAnchorB);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(
        ref WheelJointData data, BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        Frame(in stateA, in stateB, data.LocalAnchorA, data.LocalAnchorB, data.LocalAxisA,
            out Vec2 axis, out Vec2 perp, out Vec2 rA, out Vec2 rB, out Vec2 pA, out Vec2 pB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        float axisA = Vec2.Cross(rA, axis);
        float axisB = Vec2.Cross(rB, axis);
        float springInverseMass = invMassA + invMassB + (invIA * axisA * axisA) + (invIB * axisB * axisB);
        if (springInverseMass > 0.0f)
        {
            float mass = 1.0f / springInverseMass;
            float omega = 2.0f * MathF.PI * data.SuspensionFrequency;
            float stiffness = mass * omega * omega;
            float damping = 2.0f * mass * data.SuspensionDamping * omega;

            float stretch = Vec2.Dot(pB - pA, axis) - data.ReferenceTranslation;
            Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
            Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
            float rate = Vec2.Dot(vB - vA, axis);

            Vec2 spring = axis * ((-(stiffness * stretch) - (damping * rate)) * dt);
            if (awakeA)
            {
                stateA.LinearVelocity -= spring * invMassA;
                stateA.AngularVelocity -= invIA * Vec2.Cross(rA, spring);
            }

            if (awakeB)
            {
                stateB.LinearVelocity += spring * invMassB;
                stateB.AngularVelocity += invIB * Vec2.Cross(rB, spring);
            }
        }

        data.MotorMaxImpulse = data.EnableMotor ? data.MaxMotorTorque * dt : 0.0f;

        Vec2 anchorA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
        Vec2 anchorB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        if ((anchorB - anchorA).LengthSquared() > wakeTolerance * wakeTolerance ||
            MathF.Abs(stateB.AngularVelocity - stateA.AngularVelocity) > wakeTolerance)
        {
            if (stateA.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyA, true);
            }

            if (stateB.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyB, true);
            }
        }

        awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        Vec2 impulse = perp * data.PerpImpulse;
        if (awakeA)
        {
            stateA.LinearVelocity -= impulse * stateA.InverseMass;
            stateA.AngularVelocity -= stateA.InverseInertia * (Vec2.Cross(rA, impulse) + data.MotorImpulse);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += impulse * stateB.InverseMass;
            stateB.AngularVelocity += stateB.InverseInertia * (Vec2.Cross(rB, impulse) + data.MotorImpulse);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocity(ref WheelJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Frame(in stateA, in stateB, data.LocalAnchorA, data.LocalAnchorB, data.LocalAxisA,
            out _, out Vec2 perp, out Vec2 rA, out Vec2 rB, out _, out _);

        float axialMass = invIA + invIB;
        if (data.EnableMotor && axialMass > 0.0f)
        {
            float rate = (stateB.AngularVelocity - stateA.AngularVelocity) - data.MotorSpeed;
            float impulse = -rate / axialMass;
            float old = data.MotorImpulse;
            float next = Math.Clamp(old + impulse, -data.MotorMaxImpulse, data.MotorMaxImpulse);
            float delta = next - old;
            data.MotorImpulse = next;

            if (awakeA)
            {
                stateA.AngularVelocity -= invIA * delta;
            }

            if (awakeB)
            {
                stateB.AngularVelocity += invIB * delta;
            }
        }

        float perpA = Vec2.Cross(rA, perp);
        float perpB = Vec2.Cross(rB, perp);
        float perpMass = invMassA + invMassB + (invIA * perpA * perpA) + (invIB * perpB * perpB);
        if (perpMass > 0.0f)
        {
            Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
            Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
            float rate = Vec2.Dot(vB - vA, perp);
            float delta = -rate / perpMass;
            data.PerpImpulse += delta;
            Vec2 applied = perp * delta;

            if (awakeA)
            {
                stateA.LinearVelocity -= applied * invMassA;
                stateA.AngularVelocity -= invIA * Vec2.Cross(rA, applied);
            }

            if (awakeB)
            {
                stateB.LinearVelocity += applied * invMassB;
                stateB.AngularVelocity += invIB * Vec2.Cross(rB, applied);
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolvePosition(ref WheelJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Frame(in stateA, in stateB, data.LocalAnchorA, data.LocalAnchorB, data.LocalAxisA,
            out _, out Vec2 perp, out Vec2 rA, out Vec2 rB, out Vec2 pA, out Vec2 pB);

        float perpA = Vec2.Cross(rA, perp);
        float perpB = Vec2.Cross(rB, perp);
        float perpK = invMassA + invMassB + (invIA * perpA * perpA) + (invIB * perpB * perpB);
        if (perpK <= 0.0f)
        {
            return;
        }

        float error = Math.Clamp(Vec2.Dot(pB - pA, perp), -SolverTuning.MaxCorrection, SolverTuning.MaxCorrection);
        Vec2 applied = perp * (-error / perpK);
        MathPhysics.ApplyPositionCorrection(ref stateA, ref stateB, rA, rB, applied, invMassA, invMassB, invIA, invIB, awakeA, awakeB);
    }
}

/// <summary>One rope over two ground pulleys with a length ratio.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PulleyJointData
{
    /// <summary>First body.</summary>
    public BodyId BodyA;

    /// <summary>Second body.</summary>
    public BodyId BodyB;

    /// <summary>Anchor in the first body frame.</summary>
    public Vec2 LocalAnchorA;

    /// <summary>Anchor in the second body frame.</summary>
    public Vec2 LocalAnchorB;

    /// <summary>Fixed world pulley of the first rope.</summary>
    public Vec2 PulleyGroundA;

    /// <summary>Fixed world pulley of the second rope.</summary>
    public Vec2 PulleyGroundB;

    /// <summary>Rope length ratio; strictly positive.</summary>
    public float PulleyRatio;

    /// <summary>Fixed total rope length captured at creation.</summary>
    public float PulleyConstant;

    /// <summary>Accumulated rope impulse; warm-started every step.</summary>
    public float PulleyImpulse;
}

/// <summary>Rope: one bilateral impulse holding the weighted total length.</summary>
public readonly struct PulleyPolicy : IJointPolicy<PulleyPolicy, PulleyJointData>
{
    /// <inheritdoc/>
    public static JointKind Kind => JointKind.Pulley;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBodies(ref PulleyJointData data, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = data.BodyA;
        bodyB = data.BodyB;
    }

    /// <summary>Rope directions from ground pulleys to anchors; false while either rope is slack.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static bool GetDirections(in PulleyJointData data, Vec2 pA, Vec2 pB, out Vec2 uA, out Vec2 uB)
    {
        Vec2 dA = pA - data.PulleyGroundA;
        Vec2 dB = pB - data.PulleyGroundB;
        float lenASq = dA.LengthSquared();
        float lenBSq = dB.LengthSquared();

        if (lenASq <= SolverTuning.LinearSlop * SolverTuning.LinearSlop ||
            lenBSq <= SolverTuning.LinearSlop * SolverTuning.LinearSlop)
        {
            uA = default;
            uB = default;
            return false;
        }

        uA = dA * (1.0f / MathF.Sqrt(lenASq));
        uB = dB * (1.0f / MathF.Sqrt(lenBSq));
        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(
        ref PulleyJointData data, BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, data.LocalAnchorA);
        Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        if ((vB - vA).LengthSquared() > wakeTolerance * wakeTolerance)
        {
            if (stateA.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyA, true);
            }

            if (stateB.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyB, true);
            }
        }

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        if (GetDirections(in data, pA, pB, out Vec2 uA, out Vec2 uB))
        {
            Vec2 impulseA = uA * data.PulleyImpulse;
            Vec2 impulseB = uB * (data.PulleyImpulse * data.PulleyRatio);
            if (awakeA)
            {
                stateA.LinearVelocity += impulseA * stateA.InverseMass;
                stateA.AngularVelocity += stateA.InverseInertia * Vec2.Cross(rA, impulseA);
            }

            if (awakeB)
            {
                stateB.LinearVelocity += impulseB * stateB.InverseMass;
                stateB.AngularVelocity += stateB.InverseInertia * Vec2.Cross(rB, impulseB);
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocity(ref PulleyJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, data.LocalAnchorA);
        Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
        if (!GetDirections(in data, pA, pB, out Vec2 uA, out Vec2 uB))
        {
            return;
        }

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        float ratio = data.PulleyRatio;
        float rnA = Vec2.Cross(rA, uA);
        float rnB = Vec2.Cross(rB, uB);
        float k = invMassA + (ratio * ratio * invMassB) + (invIA * rnA * rnA) + (ratio * ratio * invIB * rnB * rnB);
        if (k <= 0.0f)
        {
            return;
        }

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        float rate = Vec2.Dot(uA, vA) + (ratio * Vec2.Dot(uB, vB));
        float delta = -rate / k;
        data.PulleyImpulse += delta;

        Vec2 appliedA = uA * delta;
        Vec2 appliedB = uB * (delta * ratio);
        if (awakeA)
        {
            stateA.LinearVelocity += appliedA * invMassA;
            stateA.AngularVelocity += invIA * Vec2.Cross(rA, appliedA);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += appliedB * invMassB;
            stateB.AngularVelocity += invIB * Vec2.Cross(rB, appliedB);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolvePosition(ref PulleyJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, data.LocalAnchorA);
        Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
        if (!GetDirections(in data, pA, pB, out Vec2 uA, out Vec2 uB))
        {
            return;
        }

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        float ratio = data.PulleyRatio;
        float rnA = Vec2.Cross(rA, uA);
        float rnB = Vec2.Cross(rB, uB);
        float k = invMassA + (ratio * ratio * invMassB) + (invIA * rnA * rnA) + (ratio * ratio * invIB * rnB * rnB);
        if (k <= 0.0f)
        {
            return;
        }

        float lengthA = (pA - data.PulleyGroundA).Length();
        float lengthB = (pB - data.PulleyGroundB).Length();
        float error = Math.Clamp(
            (lengthA + (ratio * lengthB)) - data.PulleyConstant,
            -SolverTuning.MaxCorrection, SolverTuning.MaxCorrection);

        Vec2 appliedA = uA * (-error / k);
        Vec2 appliedB = uB * (-error * ratio / k);
        if (awakeA)
        {
            stateA.Position += appliedA * invMassA;
            float angleA = MathF.Atan2(stateA.Sin, stateA.Cos) + (invIA * Vec2.Cross(rA, appliedA));
            stateA.Cos = MathF.Cos(angleA);
            stateA.Sin = MathF.Sin(angleA);
        }

        if (awakeB)
        {
            stateB.Position += appliedB * invMassB;
            float angleB = MathF.Atan2(stateB.Sin, stateB.Cos) + (invIB * Vec2.Cross(rB, appliedB));
            stateB.Cos = MathF.Cos(angleB);
            stateB.Sin = MathF.Sin(angleB);
        }
    }
}

/// <summary>One ratio lock between two geared coordinates; bridges two islands.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct GearJointData
{
    /// <summary>First body of the first geared joint; island bridge endpoint.</summary>
    public BodyId BodyA;

    /// <summary>First body of the second geared joint; island bridge endpoint.</summary>
    public BodyId BodyB;

    /// <summary>First geared joint; never a gear itself.</summary>
    public JointId GearJointA;

    /// <summary>Second geared joint; never a gear itself.</summary>
    public JointId GearJointB;

    /// <summary>Coordinate ratio of the second joint; finite and non-zero.</summary>
    public float GearRatio;

    /// <summary>Fixed coordinate sum captured at creation.</summary>
    public float GearConstant;

    /// <summary>Accumulated gear impulse; warm-started every step.</summary>
    public float GearImpulse;
}

/// <summary>Ratio lock: one bilateral impulse over the summed coordinates.</summary>
public readonly struct GearPolicy : IJointPolicy<GearPolicy, GearJointData>
{
    /// <inheritdoc/>
    public static JointKind Kind => JointKind.Gear;

    /// <inheritdoc/>
    public static void GetBodies(ref GearJointData data, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = data.BodyA;
        bodyB = data.BodyB;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(
        ref GearJointData data, BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        if (!store.IsValid(data.GearJointA) || !store.IsValid(data.GearJointB))
        {
            return;
        }

        float rateA = store.GetCoordinateRate(bodies, data.GearJointA);
        float rateB = store.GetCoordinateRate(bodies, data.GearJointB);
        if (MathF.Abs(rateA + (data.GearRatio * rateB)) > wakeTolerance)
        {
            store.WakeGearedBodies(bodies, data.GearJointA);
            store.WakeGearedBodies(bodies, data.GearJointB);
        }

        store.ApplyGearedVelocity(bodies, data.GearJointA, data.GearImpulse);
        store.ApplyGearedVelocity(bodies, data.GearJointB, data.GearImpulse * data.GearRatio);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocity(ref GearJointData data, BodyStore bodies, JointStore store)
    {
        if (!store.IsValid(data.GearJointA) || !store.IsValid(data.GearJointB))
        {
            return;
        }

        float ratio = data.GearRatio;
        float massA = store.GetEffectiveCoordinateMass(bodies, data.GearJointA);
        float massB = store.GetEffectiveCoordinateMass(bodies, data.GearJointB);
        float k = massA + (ratio * ratio * massB);
        if (k <= 0.0f)
        {
            return;
        }

        float rate = store.GetCoordinateRate(bodies, data.GearJointA) + (ratio * store.GetCoordinateRate(bodies, data.GearJointB));
        float delta = -rate / k;
        data.GearImpulse += delta;

        store.ApplyGearedVelocity(bodies, data.GearJointA, delta);
        store.ApplyGearedVelocity(bodies, data.GearJointB, delta * ratio);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolvePosition(ref GearJointData data, BodyStore bodies, JointStore store)
    {
        if (!store.IsValid(data.GearJointA) || !store.IsValid(data.GearJointB))
        {
            return;
        }

        float ratio = data.GearRatio;
        float massA = store.GetEffectiveCoordinateMass(bodies, data.GearJointA);
        float massB = store.GetEffectiveCoordinateMass(bodies, data.GearJointB);
        float k = massA + (ratio * ratio * massB);
        if (k <= 0.0f)
        {
            return;
        }

        float error = (store.GetCoordinate(bodies, data.GearJointA) + (ratio * store.GetCoordinate(bodies, data.GearJointB))) - data.GearConstant;
        if (MathF.Abs(error) < SolverTuning.LinearSlop)
        {
            return;
        }

        float correction = -error / k;
        store.ApplyGearedPosition(bodies, data.GearJointA, correction);
        store.ApplyGearedPosition(bodies, data.GearJointB, correction * ratio);
    }
}

#pragma warning restore CA1062

#endregion

#region Zero-Allocation Duck-Typed Dense Storage

/// <summary>Minimal storage role: live count plus slot teardown.</summary>
public interface IJointStorage
{
    /// <summary>Live constraints.</summary>
    int Count { get; }

    /// <summary>Tears down one slot by sparse index; unknown slots are ignored.</summary>
    /// <param name="sparseIndex">The sparse index to release.</param>
    void DestroySlot(uint sparseIndex);
}

/// <summary>
/// Dense slot storage for one constraint archetype: swap-remove keeps the
/// live set packed, sparse indices recycle, and sweeps walk raw refs with no
/// per-item dispatch.
/// </summary>
/// <typeparam name="TPolicy">The monomorphic solver archetype.</typeparam>
/// <typeparam name="TData">The constraint storage layout.</typeparam>
[StructLayout(LayoutKind.Sequential)]
public sealed class JointStorage<TPolicy, TData> : IJointStorage
    where TPolicy : struct, IJointPolicy<TPolicy, TData>
    where TData : struct
{
    private TData[] _dense;
    private uint[] _denseToSparse;
    private uint[] _sparse;
    private uint[] _freeStack;
    private int _count;
    private int _freeCount;
    private int _sparseCapacity;

    /// <summary>Creates storage with room for sixteen constraints.</summary>
    /// <param name="capacity">The initial capacity.</param>
    public JointStorage(int capacity = 16)
    {
        _sparseCapacity = Math.Max(16, capacity);
        _dense = new TData[_sparseCapacity];
        _denseToSparse = new uint[_sparseCapacity];
        _sparse = new uint[_sparseCapacity];
        _freeStack = new uint[_sparseCapacity];

        Array.Fill(_sparse, uint.MaxValue);
        _count = 0;
        _freeCount = 0;
    }

    /// <inheritdoc/>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    /// <summary>Live data packed front to back.</summary>
    public ReadOnlySpan<TData> DataSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _dense.AsSpan(0, _count);
    }

    /// <summary>Maps a dense position back to its sparse index.</summary>
    /// <param name="denseIndex">The dense position.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint SparseOfDense(int denseIndex) => _denseToSparse[denseIndex];

    /// <summary>Reads live data by sparse index; dead slots throw.</summary>
    /// <param name="sparseIndex">The sparse index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TData GetBySparseIndex(uint sparseIndex)
    {
        if (sparseIndex >= (uint)_sparseCapacity)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(sparseIndex));
        }

        uint denseIdx = _sparse[sparseIndex];
        if (denseIdx >= (uint)_count)
        {
            ThrowHelper.ThrowInvalidOperation("Accessing dead dense joint slot.");
        }

        return ref _dense[denseIdx];
    }

    /// <summary>Probes liveness by sparse index without throwing.</summary>
    /// <param name="sparseIndex">The sparse index.</param>
    /// <param name="denseIndex">The dense position on success.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetBySparseIndex(uint sparseIndex, out uint denseIndex)
    {
        if (sparseIndex < (uint)_sparseCapacity)
        {
            denseIndex = _sparse[sparseIndex];
            return denseIndex < (uint)_count;
        }

        denseIndex = uint.MaxValue;
        return false;
    }

    /// <summary>Appends an item and returns its sparse index.</summary>
    /// <param name="item">The constraint data.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Allocate(in TData item)
    {
        if (_count >= _dense.Length)
        {
            int newCap = _dense.Length * 2;
            Array.Resize(ref _dense, newCap);
            Array.Resize(ref _denseToSparse, newCap);
        }

        uint sparseIndex;
        if (_freeCount > 0)
        {
            sparseIndex = _freeStack[--_freeCount];
        }
        else
        {
            sparseIndex = (uint)_count;
            if (sparseIndex >= (uint)_sparseCapacity)
            {
                int newSparseCap = _sparseCapacity * 2;
                Array.Resize(ref _sparse, newSparseCap);
                Array.Resize(ref _freeStack, newSparseCap);
                for (int i = _sparseCapacity; i < newSparseCap; i++)
                {
                    _sparse[i] = uint.MaxValue;
                }

                _sparseCapacity = newSparseCap;
            }
        }

        uint denseIndex = (uint)_count++;
        _dense[denseIndex] = item;
        _denseToSparse[denseIndex] = sparseIndex;
        _sparse[sparseIndex] = denseIndex;

        return sparseIndex;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DestroySlot(uint sparseIndex)
    {
        if (sparseIndex >= (uint)_sparseCapacity)
        {
            return;
        }

        uint denseIndex = _sparse[sparseIndex];
        if (denseIndex >= (uint)_count)
        {
            return;
        }

        int lastDenseIndex = _count - 1;
        if ((int)denseIndex != lastDenseIndex)
        {
            TData lastItem = _dense[lastDenseIndex];
            uint lastSparseIndex = _denseToSparse[lastDenseIndex];

            _dense[denseIndex] = lastItem;
            _denseToSparse[denseIndex] = lastSparseIndex;
            _sparse[lastSparseIndex] = denseIndex;
        }

        _sparse[sparseIndex] = uint.MaxValue;
        _count--;

        if (_freeCount >= _freeStack.Length)
        {
            Array.Resize(ref _freeStack, _freeStack.Length * 2);
        }

        _freeStack[_freeCount++] = sparseIndex;
    }

    /// <summary>Runs the policy init over the packed set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void InitVelocity(BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        ref TData start = ref MemoryMarshal.GetArrayDataReference(_dense);
        uint count = (uint)_count;
        for (uint i = 0; i < count; i++)
        {
            TPolicy.InitVelocity(ref Unsafe.Add(ref start, i), bodies, store, wakeTolerance, dt);
        }
    }

    /// <summary>Runs the policy velocity sweep over the packed set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void SolveVelocity(BodyStore bodies, JointStore store)
    {
        ref TData start = ref MemoryMarshal.GetArrayDataReference(_dense);
        uint count = (uint)_count;
        for (uint i = 0; i < count; i++)
        {
            TPolicy.SolveVelocity(ref Unsafe.Add(ref start, i), bodies, store);
        }
    }

    /// <summary>Runs the policy position sweep over the packed set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void SolvePosition(BodyStore bodies, JointStore store)
    {
        ref TData start = ref MemoryMarshal.GetArrayDataReference(_dense);
        uint count = (uint)_count;
        for (uint i = 0; i < count; i++)
        {
            TPolicy.SolvePosition(ref Unsafe.Add(ref start, i), bodies, store);
        }
    }

    /// <summary>Enumerates the packed set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DenseEnumerator GetEnumerator() => new(this);

    /// <summary>Zero-allocation enumerator over the packed set.</summary>
#pragma warning disable CA1034 // The enumerator rides its storage: it borrows the dense array and the live count, so it cannot meaningfully live outside the generic storage type.
    public ref struct DenseEnumerator
    {
        private readonly TData[] _array;
        private readonly int _length;
        private int _index;

        /// <summary>Binds the enumerator to storage.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal DenseEnumerator(JointStorage<TPolicy, TData> storage)
        {
            _array = storage._dense;
            _length = storage._count;
            _index = -1;
        }

        /// <summary>Advances to the next live item.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int next = _index + 1;
            if (next < _length)
            {
                _index = next;
                return true;
            }

            return false;
        }

        /// <summary>The current live item.</summary>
        public readonly ref readonly TData Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _array[_index];
        }
    }
#pragma warning restore CA1034
}

#endregion

#region Segregated Role Contracts

/// <summary>Public joint registry role: count, liveness and teardown.</summary>
public interface IJointRegistry
{
    /// <summary>Live joints across all archetypes.</summary>
    int Count { get; }

    /// <summary>Whether an identity names a live joint.</summary>
    /// <param name="id">The identity.</param>
    bool IsValid(JointId id);

    /// <summary>Drops a joint; unknown identities are ignored.</summary>
    /// <param name="id">The identity.</param>
    void Destroy(JointId id);
}

/// <summary>Zero-virtual visitor over every live joint.</summary>
/// <typeparam name="TContext">The visitor context.</typeparam>
public interface IJointVisitor<TContext> where TContext : allows ref struct
{
    /// <summary>Visits one live joint.</summary>
    /// <typeparam name="TPolicy">The constraint archetype.</typeparam>
    /// <typeparam name="TData">The constraint storage layout.</typeparam>
    /// <param name="id">The joint identity.</param>
    /// <param name="data">The live constraint data.</param>
    /// <param name="context">The visitor context.</param>
    void Visit<TPolicy, TData>(JointId id, ref TData data, ref TContext context)
        where TPolicy : struct, IJointPolicy<TPolicy, TData>
        where TData : struct;
}

#endregion

#region Coordination & Arbitration Engine

/// <summary>
/// Dense segregated store of joints: one packed table per archetype, kind-tagged
/// identities, geometrically growing buffers. Creation validates bodies and wakes
/// sleeping dynamics; destroying a joint also drops the gears locked to it, and
/// destroying a body drops every joint touching it.
/// </summary>
public sealed class JointStore : IJointRegistry
{
    private readonly Lock _lock = new();

    private readonly JointStorage<DistancePolicy, DistanceJointData> _distance = new();
    private readonly JointStorage<RevolutePolicy, RevoluteJointData> _revolute = new();
    private readonly JointStorage<WeldPolicy, WeldJointData> _weld = new();
    private readonly JointStorage<PrismaticPolicy, PrismaticJointData> _prismatic = new();
    private readonly JointStorage<MotorPolicy, MotorJointData> _motor = new();
    private readonly JointStorage<WheelPolicy, WheelJointData> _wheel = new();
    private readonly JointStorage<PulleyPolicy, PulleyJointData> _pulley = new();
    private readonly JointStorage<GearPolicy, GearJointData> _gear = new();

    /// <inheritdoc/>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _distance.Count + _revolute.Count + _weld.Count + _prismatic.Count +
               _motor.Count + _wheel.Count + _pulley.Count + _gear.Count;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid(JointId id)
    {
        if (!id.IsValid)
        {
            return false;
        }

        uint slot = id.SlotIndex;
        return id.Kind switch
        {
            JointKind.Distance => _distance.TryGetBySparseIndex(slot, out _),
            JointKind.Revolute => _revolute.TryGetBySparseIndex(slot, out _),
            JointKind.Weld => _weld.TryGetBySparseIndex(slot, out _),
            JointKind.Prismatic => _prismatic.TryGetBySparseIndex(slot, out _),
            JointKind.Motor => _motor.TryGetBySparseIndex(slot, out _),
            JointKind.Wheel => _wheel.TryGetBySparseIndex(slot, out _),
            JointKind.Pulley => _pulley.TryGetBySparseIndex(slot, out _),
            JointKind.Gear => _gear.TryGetBySparseIndex(slot, out _),
            _ => false,
        };
    }

    /// <summary>
    /// Creates a rigid distance joint between two anchor points and wakes
    /// both bodies. The rest length is fixed at creation.
    /// </summary>
    /// <param name="bodies">Body registry; both bodies must be live.</param>
    /// <param name="bodyA">First body.</param>
    /// <param name="bodyB">Second body.</param>
    /// <param name="localAnchorA">Anchor in the first body frame.</param>
    /// <param name="localAnchorB">Anchor in the second body frame.</param>
    /// <param name="length">Rest separation; finite and never negative.</param>
    public JointId CreateDistance(
        BodyStore bodies,
        BodyId bodyA,
        BodyId bodyB,
        Vec2 localAnchorA,
        Vec2 localAnchorB,
        float length)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        bodies.Slot(bodyA);
        bodies.Slot(bodyB);

        if (!(length >= 0.0f) || float.IsNaN(length) || float.IsInfinity(length))
        {
            ThrowHelper.ThrowInvalidOperation("Joint rest length must be finite and non-negative.");
        }

        lock (_lock)
        {
            DistanceJointData data = new()
            {
                BodyA = bodyA,
                BodyB = bodyB,
                LocalAnchorA = localAnchorA,
                LocalAnchorB = localAnchorB,
                Length = length,
                Impulse = 0.0f,
            };
            uint sparse = _distance.Allocate(in data);
            bodies.SetAwake(bodyA, true);
            bodies.SetAwake(bodyB, true);
            return new JointId(JointKind.Distance, sparse);
        }
    }

    /// <summary>
    /// Creates a pinned hinge between two bodies through a shared world
    /// anchor and wakes both bodies. Angular stops and the speed motor are
    /// configured by <paramref name="definition"/>.
    /// </summary>
    /// <param name="bodies">Body registry; both bodies must be live and distinct.</param>
    /// <param name="definition">Pin anchor, stops and motor tuning.</param>
    public JointId CreateRevolute(BodyStore bodies, in RevoluteDef definition)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        if (!definition.BodyA.IsValid || !definition.BodyB.IsValid || definition.BodyA == definition.BodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Revolute joints need two distinct live bodies.");
        }

        if (float.IsNaN(definition.WorldAnchor.X) || float.IsNaN(definition.WorldAnchor.Y) ||
            float.IsInfinity(definition.WorldAnchor.X) || float.IsInfinity(definition.WorldAnchor.Y))
        {
            ThrowHelper.ThrowInvalidOperation("Joint anchor must be finite.");
        }

        if (definition.EnableLimit && definition.LowerAngle > definition.UpperAngle)
        {
            ThrowHelper.ThrowInvalidOperation("Lower angular stop must not exceed the upper stop.");
        }

        ref BodyState stateA = ref bodies.Slot(definition.BodyA);
        ref BodyState stateB = ref bodies.Slot(definition.BodyB);

        Transform2D transformA = Transform2D.FromRotation(stateA.Position, stateA.Cos, stateA.Sin);
        Transform2D transformB = Transform2D.FromRotation(stateB.Position, stateB.Cos, stateB.Sin);

        lock (_lock)
        {
            RevoluteJointData data = new()
            {
                BodyA = definition.BodyA,
                BodyB = definition.BodyB,
                LocalAnchorA = transformA.InverseTransformPoint(definition.WorldAnchor),
                LocalAnchorB = transformB.InverseTransformPoint(definition.WorldAnchor),
                ReferenceAngle = transformB.Angle - transformA.Angle,
                LowerAngle = definition.LowerAngle,
                UpperAngle = definition.UpperAngle,
                EnableLimit = definition.EnableLimit,
                MotorSpeed = definition.MotorSpeed,
                MaxMotorTorque = definition.MaxMotorTorque,
                EnableMotor = definition.EnableMotor,
            };
            uint sparse = _revolute.Allocate(in data);
            bodies.SetAwake(definition.BodyA, true);
            bodies.SetAwake(definition.BodyB, true);
            return new JointId(JointKind.Revolute, sparse);
        }
    }

    /// <summary>
    /// Creates a weld locking two bodies through a shared world anchor and
    /// wakes both bodies. The relative angle and anchor separation freeze at
    /// creation.
    /// </summary>
    /// <param name="bodies">Body registry; both bodies must be live and distinct.</param>
    /// <param name="definition">Pin anchor.</param>
    public JointId CreateWeld(BodyStore bodies, in WeldDef definition)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        if (!definition.BodyA.IsValid || !definition.BodyB.IsValid || definition.BodyA == definition.BodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Weld joints need two distinct live bodies.");
        }

        if (float.IsNaN(definition.WorldAnchor.X) || float.IsNaN(definition.WorldAnchor.Y) ||
            float.IsInfinity(definition.WorldAnchor.X) || float.IsInfinity(definition.WorldAnchor.Y))
        {
            ThrowHelper.ThrowInvalidOperation("Joint anchor must be finite.");
        }

        ref BodyState stateA = ref bodies.Slot(definition.BodyA);
        ref BodyState stateB = ref bodies.Slot(definition.BodyB);

        Transform2D transformA = Transform2D.FromRotation(stateA.Position, stateA.Cos, stateA.Sin);
        Transform2D transformB = Transform2D.FromRotation(stateB.Position, stateB.Cos, stateB.Sin);

        lock (_lock)
        {
            WeldJointData data = new()
            {
                BodyA = definition.BodyA,
                BodyB = definition.BodyB,
                LocalAnchorA = transformA.InverseTransformPoint(definition.WorldAnchor),
                LocalAnchorB = transformB.InverseTransformPoint(definition.WorldAnchor),
                ReferenceAngle = transformB.Angle - transformA.Angle,
            };
            uint sparse = _weld.Allocate(in data);
            bodies.SetAwake(definition.BodyA, true);
            bodies.SetAwake(definition.BodyB, true);
            return new JointId(JointKind.Weld, sparse);
        }
    }

    /// <summary>
    /// Creates a slider along a world axis through a world anchor and wakes
    /// both bodies. Slide stops and the slide motor are configured by
    /// <paramref name="definition"/>.
    /// </summary>
    /// <param name="bodies">Body registry; both bodies must be live and distinct.</param>
    /// <param name="definition">Slide anchor, axis, stops and motor tuning.</param>
    public JointId CreatePrismatic(BodyStore bodies, in PrismaticDef definition)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        if (!definition.BodyA.IsValid || !definition.BodyB.IsValid || definition.BodyA == definition.BodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Prismatic joints need two distinct live bodies.");
        }

        if (float.IsNaN(definition.WorldAnchor.X) || float.IsNaN(definition.WorldAnchor.Y) ||
            float.IsInfinity(definition.WorldAnchor.X) || float.IsInfinity(definition.WorldAnchor.Y))
        {
            ThrowHelper.ThrowInvalidOperation("Joint anchor must be finite.");
        }

        if (definition.WorldAxis.LengthSquared() < 1e-12f ||
            float.IsNaN(definition.WorldAxis.X) || float.IsNaN(definition.WorldAxis.Y))
        {
            ThrowHelper.ThrowInvalidOperation("Slide axis must be a finite non-zero vector.");
        }

        if (definition.EnableLimit && definition.LowerTranslation > definition.UpperTranslation)
        {
            ThrowHelper.ThrowInvalidOperation("Lower slide stop must not exceed the upper stop.");
        }

        ref BodyState stateA = ref bodies.Slot(definition.BodyA);
        ref BodyState stateB = ref bodies.Slot(definition.BodyB);

        Transform2D transformA = Transform2D.FromRotation(stateA.Position, stateA.Cos, stateA.Sin);
        Transform2D transformB = Transform2D.FromRotation(stateB.Position, stateB.Cos, stateB.Sin);

        Vec2 axis = Vec2.Normalize(definition.WorldAxis);

        lock (_lock)
        {
            Vec2 localAnchorA = transformA.InverseTransformPoint(definition.WorldAnchor);
            Vec2 localAnchorB = transformB.InverseTransformPoint(definition.WorldAnchor);
            Vec2 localAxisA = new(
                (transformA.Cos * axis.X) + (transformA.Sin * axis.Y),
                (-transformA.Sin * axis.X) + (transformA.Cos * axis.Y));

            PrismaticJointData data = new()
            {
                BodyA = definition.BodyA,
                BodyB = definition.BodyB,
                LocalAnchorA = localAnchorA,
                LocalAnchorB = localAnchorB,
                LocalAxisA = localAxisA,
                ReferenceAngle = transformB.Angle - transformA.Angle,
                ReferenceTranslation = Vec2.Dot(
                    transformB.TransformPoint(localAnchorB) - transformA.TransformPoint(localAnchorA), axis),
                LowerTranslation = definition.LowerTranslation,
                UpperTranslation = definition.UpperTranslation,
                EnableLimit = definition.EnableLimit,
                MotorSpeed = definition.MotorSpeed,
                MaxMotorForce = definition.MaxMotorForce,
                EnableMotor = definition.EnableMotor,
            };
            uint sparse = _prismatic.Allocate(in data);
            bodies.SetAwake(definition.BodyA, true);
            bodies.SetAwake(definition.BodyB, true);
            return new JointId(JointKind.Prismatic, sparse);
        }
    }

    /// <summary>
    /// Creates a servo driving the second body toward a target pose attached
    /// to the first body and wakes both bodies. Force and torque caps bound
    /// the drive; the correction factor sets the response speed.
    /// </summary>
    /// <param name="bodies">Body registry; both bodies must be live and distinct.</param>
    /// <param name="definition">Target offset, caps and response speed.</param>
    public JointId CreateMotor(BodyStore bodies, in MotorDef definition)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        if (!definition.BodyA.IsValid || !definition.BodyB.IsValid || definition.BodyA == definition.BodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Motor joints need two distinct live bodies.");
        }

        if (float.IsNaN(definition.LinearOffset.X) || float.IsNaN(definition.LinearOffset.Y) ||
            float.IsInfinity(definition.LinearOffset.X) || float.IsInfinity(definition.LinearOffset.Y))
        {
            ThrowHelper.ThrowInvalidOperation("Servo offset must be finite.");
        }

        if (float.IsNaN(definition.AngularOffset) || float.IsInfinity(definition.AngularOffset))
        {
            ThrowHelper.ThrowInvalidOperation("Servo angle must be finite.");
        }

        bodies.Slot(definition.BodyA);
        bodies.Slot(definition.BodyB);

        lock (_lock)
        {
            MotorJointData data = new()
            {
                BodyA = definition.BodyA,
                BodyB = definition.BodyB,
                ServoOffset = definition.LinearOffset,
                ServoAngle = definition.AngularOffset,
                ServoMaxForce = definition.MaxForce,
                ServoMaxTorque = definition.MaxTorque,
                ServoFactor = definition.CorrectionFactor,
            };
            uint sparse = _motor.Allocate(in data);
            bodies.SetAwake(definition.BodyA, true);
            bodies.SetAwake(definition.BodyB, true);
            return new JointId(JointKind.Motor, sparse);
        }
    }

    /// <summary>
    /// Creates a sprung suspension along a world axis through a world anchor
    /// and wakes both bodies. The rest position freezes at creation; the hub
    /// motor optionally spins the wheel.
    /// </summary>
    /// <param name="bodies">Body registry; both bodies must be live and distinct.</param>
    /// <param name="definition">Suspension anchor, axis, spring and hub tuning.</param>
    public JointId CreateWheel(BodyStore bodies, in WheelDef definition)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        if (!definition.BodyA.IsValid || !definition.BodyB.IsValid || definition.BodyA == definition.BodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Wheel joints need two distinct live bodies.");
        }

        if (float.IsNaN(definition.WorldAnchor.X) || float.IsNaN(definition.WorldAnchor.Y) ||
            float.IsInfinity(definition.WorldAnchor.X) || float.IsInfinity(definition.WorldAnchor.Y))
        {
            ThrowHelper.ThrowInvalidOperation("Joint anchor must be finite.");
        }

        if (definition.WorldAxis.LengthSquared() < 1e-12f ||
            float.IsNaN(definition.WorldAxis.X) || float.IsNaN(definition.WorldAxis.Y))
        {
            ThrowHelper.ThrowInvalidOperation("Suspension axis must be a finite non-zero vector.");
        }

        ref BodyState stateA = ref bodies.Slot(definition.BodyA);
        ref BodyState stateB = ref bodies.Slot(definition.BodyB);

        Transform2D transformA = Transform2D.FromRotation(stateA.Position, stateA.Cos, stateA.Sin);
        Transform2D transformB = Transform2D.FromRotation(stateB.Position, stateB.Cos, stateB.Sin);

        Vec2 axis = Vec2.Normalize(definition.WorldAxis);

        lock (_lock)
        {
            Vec2 localAnchorA = transformA.InverseTransformPoint(definition.WorldAnchor);
            Vec2 localAnchorB = transformB.InverseTransformPoint(definition.WorldAnchor);
            Vec2 localAxisA = new(
                (transformA.Cos * axis.X) + (transformA.Sin * axis.Y),
                (-transformA.Sin * axis.X) + (transformA.Cos * axis.Y));

            WheelJointData data = new()
            {
                BodyA = definition.BodyA,
                BodyB = definition.BodyB,
                LocalAnchorA = localAnchorA,
                LocalAnchorB = localAnchorB,
                LocalAxisA = localAxisA,
                ReferenceTranslation = Vec2.Dot(
                    transformB.TransformPoint(localAnchorB) - transformA.TransformPoint(localAnchorA), axis),
                MotorSpeed = definition.MotorSpeed,
                MaxMotorTorque = definition.MaxMotorTorque,
                EnableMotor = definition.EnableMotor,
                SuspensionFrequency = definition.SuspensionFrequency,
                SuspensionDamping = definition.SuspensionDamping,
            };
            uint sparse = _wheel.Allocate(in data);
            bodies.SetAwake(definition.BodyA, true);
            bodies.SetAwake(definition.BodyB, true);
            return new JointId(JointKind.Wheel, sparse);
        }
    }

    /// <summary>
    /// Creates a rope over two fixed ground pulleys between two body anchors
    /// and wakes both bodies. Rope lengths freeze at creation; the weighted
    /// total stays fixed.
    /// </summary>
    /// <param name="bodies">Body registry; both bodies must be live and distinct.</param>
    /// <param name="definition">Ground pulleys, body anchors and rope ratio.</param>
    public JointId CreatePulley(BodyStore bodies, in PulleyDef definition)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        if (!definition.BodyA.IsValid || !definition.BodyB.IsValid || definition.BodyA == definition.BodyB)
        {
            ThrowHelper.ThrowInvalidOperation("Pulley joints need two distinct live bodies.");
        }

        if (float.IsNaN(definition.GroundAnchorA.X) || float.IsNaN(definition.GroundAnchorA.Y) ||
            float.IsInfinity(definition.GroundAnchorA.X) || float.IsInfinity(definition.GroundAnchorA.Y) ||
            float.IsNaN(definition.GroundAnchorB.X) || float.IsNaN(definition.GroundAnchorB.Y) ||
            float.IsInfinity(definition.GroundAnchorB.X) || float.IsInfinity(definition.GroundAnchorB.Y))
        {
            ThrowHelper.ThrowInvalidOperation("Ground anchors must be finite.");
        }

        if (float.IsNaN(definition.WorldAnchorA.X) || float.IsNaN(definition.WorldAnchorA.Y) ||
            float.IsInfinity(definition.WorldAnchorA.X) || float.IsInfinity(definition.WorldAnchorA.Y) ||
            float.IsNaN(definition.WorldAnchorB.X) || float.IsNaN(definition.WorldAnchorB.Y) ||
            float.IsInfinity(definition.WorldAnchorB.X) || float.IsInfinity(definition.WorldAnchorB.Y))
        {
            ThrowHelper.ThrowInvalidOperation("Body anchors must be finite.");
        }

        ref BodyState stateA = ref bodies.Slot(definition.BodyA);
        ref BodyState stateB = ref bodies.Slot(definition.BodyB);

        Transform2D transformA = Transform2D.FromRotation(stateA.Position, stateA.Cos, stateA.Sin);
        Transform2D transformB = Transform2D.FromRotation(stateB.Position, stateB.Cos, stateB.Sin);

        float lengthA = (definition.WorldAnchorA - definition.GroundAnchorA).Length();
        float lengthB = (definition.WorldAnchorB - definition.GroundAnchorB).Length();
        if (lengthA <= SolverTuning.LinearSlop || lengthB <= SolverTuning.LinearSlop)
        {
            ThrowHelper.ThrowInvalidOperation("Body anchors must start clear of their ground pulleys.");
        }

        lock (_lock)
        {
            PulleyJointData data = new()
            {
                BodyA = definition.BodyA,
                BodyB = definition.BodyB,
                LocalAnchorA = transformA.InverseTransformPoint(definition.WorldAnchorA),
                LocalAnchorB = transformB.InverseTransformPoint(definition.WorldAnchorB),
                PulleyGroundA = definition.GroundAnchorA,
                PulleyGroundB = definition.GroundAnchorB,
                PulleyRatio = definition.Ratio,
                PulleyConstant = lengthA + (definition.Ratio * lengthB),
            };
            uint sparse = _pulley.Allocate(in data);
            bodies.SetAwake(definition.BodyA, true);
            bodies.SetAwake(definition.BodyB, true);
            return new JointId(JointKind.Pulley, sparse);
        }
    }

    /// <summary>
    /// Locks the coordinates of two live revolute or prismatic joints in a
    /// ratio and wakes every connected body. Gears of gears are rejected; the
    /// coordinate sum freezes at creation.
    /// </summary>
    /// <param name="bodies">Body registry for coordinate capture and waking.</param>
    /// <param name="definition">Geared joints and ratio.</param>
    public JointId CreateGear(BodyStore bodies, in GearDef definition)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        if (!definition.JointA.IsValid || !definition.JointB.IsValid || definition.JointA == definition.JointB)
        {
            ThrowHelper.ThrowInvalidOperation("Gear joints need two distinct live joints.");
        }

        if (definition.JointA.Kind == JointKind.Gear || definition.JointB.Kind == JointKind.Gear)
        {
            ThrowHelper.ThrowInvalidOperation("Gear joints cannot reference other gears.");
        }

        if (!IsGearedKind(definition.JointA.Kind) || !IsGearedKind(definition.JointB.Kind))
        {
            ThrowHelper.ThrowInvalidOperation("Gear joints need revolute or prismatic joints.");
        }

        if (!IsValid(definition.JointA) || !IsValid(definition.JointB))
        {
            ThrowHelper.ThrowInvalidOperation("Gear joints need live joints.");
        }

        float coordinateA = GetCoordinate(bodies, definition.JointA);
        float coordinateB = GetCoordinate(bodies, definition.JointB);

        lock (_lock)
        {
            GetGearedBodies(definition.JointA, out BodyId bridgeA, out _);
            GetGearedBodies(definition.JointB, out BodyId bridgeB, out _);

            GearJointData data = new()
            {
                BodyA = bridgeA,
                BodyB = bridgeB,
                GearJointA = definition.JointA,
                GearJointB = definition.JointB,
                GearRatio = definition.Ratio,
                GearConstant = coordinateA + (definition.Ratio * coordinateB),
            };
            uint sparse = _gear.Allocate(in data);
            WakeGearedBodies(bodies, definition.JointA);
            WakeGearedBodies(bodies, definition.JointB);
            return new JointId(JointKind.Gear, sparse);
        }
    }

    /// <inheritdoc/>
    public void Destroy(JointId id)
    {
        if (!id.IsValid)
        {
            return;
        }

        lock (_lock)
        {
            switch (id.Kind)
            {
                case JointKind.Distance:
                    _distance.DestroySlot(id.SlotIndex);
                    break;
                case JointKind.Revolute:
                    _revolute.DestroySlot(id.SlotIndex);
                    break;
                case JointKind.Weld:
                    _weld.DestroySlot(id.SlotIndex);
                    break;
                case JointKind.Prismatic:
                    _prismatic.DestroySlot(id.SlotIndex);
                    break;
                case JointKind.Motor:
                    _motor.DestroySlot(id.SlotIndex);
                    break;
                case JointKind.Wheel:
                    _wheel.DestroySlot(id.SlotIndex);
                    break;
                case JointKind.Pulley:
                    _pulley.DestroySlot(id.SlotIndex);
                    break;
                case JointKind.Gear:
                    _gear.DestroySlot(id.SlotIndex);
                    break;
                default:
                    return;
            }

            CascadeGearDestruction(id);
        }
    }

    /// <summary>Drops every gear locked to a destroyed joint.</summary>
    /// <param name="droppedId">The destroyed joint.</param>
    private void CascadeGearDestruction(JointId droppedId)
    {
        ReadOnlySpan<GearJointData> gears = _gear.DataSpan;
        for (int i = gears.Length - 1; i >= 0; i--)
        {
            ref readonly GearJointData gear = ref gears[i];
            if (gear.GearJointA == droppedId || gear.GearJointB == droppedId)
            {
                _gear.DestroySlot(_gear.SparseOfDense(i));
            }
        }
    }

    /// <summary>Drops every joint naming a destroyed body, gears included through their geared joints.</summary>
    /// <param name="body">The destroyed body.</param>
    internal void DestroyJointsOfBody(BodyId body)
    {
        int budget = Count;
        if (budget <= 0)
        {
            return;
        }

        JointId[] rented = ArrayPool<JointId>.Shared.Rent(budget);
        try
        {
            int found = CollectTouching(body, rented.AsSpan(0, budget));
            for (int i = 0; i < found; i++)
            {
                Destroy(rented[i]);
            }
        }
        finally
        {
            ArrayPool<JointId>.Shared.Return(rented);
        }
    }

    /// <summary>Collects the identities touching a body, gears resolved through their geared joints.</summary>
    /// <param name="body">The body to match.</param>
    /// <param name="destination">The destination span.</param>
    /// <returns>The number of identities written.</returns>
    internal int CollectTouching(BodyId body, Span<JointId> destination)
    {
        int count = 0;
        count += CollectFrom(_distance, JointKind.Distance, body, destination[count..]);
        count += CollectFrom(_revolute, JointKind.Revolute, body, destination[count..]);
        count += CollectFrom(_weld, JointKind.Weld, body, destination[count..]);
        count += CollectFrom(_prismatic, JointKind.Prismatic, body, destination[count..]);
        count += CollectFrom(_motor, JointKind.Motor, body, destination[count..]);
        count += CollectFrom(_wheel, JointKind.Wheel, body, destination[count..]);
        count += CollectFrom(_pulley, JointKind.Pulley, body, destination[count..]);

        ReadOnlySpan<GearJointData> gears = _gear.DataSpan;
        for (int i = 0; i < gears.Length && count < destination.Length; i++)
        {
            ref readonly GearJointData gear = ref gears[i];
            if (GearTouchesBody(in gear, body))
            {
                destination[count++] = new JointId(JointKind.Gear, _gear.SparseOfDense(i));
            }
        }

        return count;
    }

    private static int CollectFrom<TPolicy, TData>(
        JointStorage<TPolicy, TData> storage, JointKind kind, BodyId body, Span<JointId> destination)
        where TPolicy : struct, IJointPolicy<TPolicy, TData>
        where TData : struct
    {
        ReadOnlySpan<TData> span = storage.DataSpan;
        int count = 0;
        for (int i = 0; i < span.Length && count < destination.Length; i++)
        {
            TPolicy.GetBodies(ref Unsafe.AsRef(in span[i]), out BodyId bodyA, out BodyId bodyB);
            if (bodyA == body || bodyB == body)
            {
                destination[count++] = new JointId(kind, storage.SparseOfDense(i));
            }
        }

        return count;
    }

    /// <summary>Whether a gear names a body directly or through either geared joint.</summary>
    private bool GearTouchesBody(in GearJointData gear, BodyId body)
    {
        if (gear.BodyA == body || gear.BodyB == body)
        {
            return true;
        }

        if (GearedBodies(gear.GearJointA, out BodyId aA, out BodyId aB) &&
            (aA == body || aB == body))
        {
            return true;
        }

        return GearedBodies(gear.GearJointB, out BodyId bA, out BodyId bB) &&
            (bA == body || bB == body);
    }

    /// <summary>Reads the four bodies coupled by a gear reference; false while either side is dead.</summary>
    private bool GearedBodies(JointId id, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = BodyId.Invalid;
        bodyB = BodyId.Invalid;

        if (id.Kind == JointKind.Revolute)
        {
            if (!_revolute.TryGetBySparseIndex(id.SlotIndex, out uint dense))
            {
                return false;
            }

            RevoluteJointData data = _revolute.DataSpan[(int)dense];
            bodyA = data.BodyA;
            bodyB = data.BodyB;
            return true;
        }

        if (id.Kind == JointKind.Prismatic)
        {
            if (!_prismatic.TryGetBySparseIndex(id.SlotIndex, out uint dense))
            {
                return false;
            }

            PrismaticJointData data = _prismatic.DataSpan[(int)dense];
            bodyA = data.BodyA;
            bodyB = data.BodyB;
            return true;
        }

        return false;
    }

    /// <summary>Reads the two bodies of a geared joint reference.</summary>
    private void GetGearedBodies(JointId id, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = BodyId.Invalid;
        bodyB = BodyId.Invalid;

        if (id.Kind == JointKind.Revolute)
        {
            ref readonly RevoluteJointData data = ref _revolute.GetBySparseIndex(id.SlotIndex);
            bodyA = data.BodyA;
            bodyB = data.BodyB;
        }
        else if (id.Kind == JointKind.Prismatic)
        {
            ref readonly PrismaticJointData data = ref _prismatic.GetBySparseIndex(id.SlotIndex);
            bodyA = data.BodyA;
            bodyB = data.BodyB;
        }
    }

    /// <summary>Whether a joint kind carries a scalar coordinate gearable by ratio.</summary>
    /// <param name="kind">The kind to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsGearedKind(JointKind kind) =>
        kind == JointKind.Revolute || kind == JointKind.Prismatic;

    /// <summary>Reads the scalar coordinate of a geared joint: angle or translation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal float GetCoordinate(BodyStore bodies, JointId id)
    {
        if (id.Kind == JointKind.Prismatic)
        {
            ref readonly PrismaticJointData data = ref _prismatic.GetBySparseIndex(id.SlotIndex);
            ref readonly BodyState stateA = ref bodies.Slot(data.BodyA);
            ref readonly BodyState stateB = ref bodies.Slot(data.BodyB);

            Vec2 axis = MathPhysics.Rotate(stateA, data.LocalAxisA);
            if (axis.LengthSquared() > 1e-12f)
            {
                axis *= 1.0f / MathF.Sqrt(axis.LengthSquared());
            }

            Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, data.LocalAnchorA);
            Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
            return Vec2.Dot(pB - pA, axis) - data.ReferenceTranslation;
        }

        ref readonly RevoluteJointData revolute = ref _revolute.GetBySparseIndex(id.SlotIndex);
        ref readonly BodyState a = ref bodies.Slot(revolute.BodyA);
        ref readonly BodyState b = ref bodies.Slot(revolute.BodyB);

        float angleA = MathF.Atan2(a.Sin, a.Cos);
        float angleB = MathF.Atan2(b.Sin, b.Cos);
        return MathPhysics.NormalizeAngle((angleB - angleA) - revolute.ReferenceAngle);
    }

    /// <summary>Reads the coordinate rate of a geared joint: relative spin or slide speed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal float GetCoordinateRate(BodyStore bodies, JointId id)
    {
        if (id.Kind == JointKind.Prismatic)
        {
            ref readonly PrismaticJointData data = ref _prismatic.GetBySparseIndex(id.SlotIndex);
            ref readonly BodyState stateA = ref bodies.Slot(data.BodyA);
            ref readonly BodyState stateB = ref bodies.Slot(data.BodyB);

            Vec2 axis = MathPhysics.Rotate(stateA, data.LocalAxisA);
            if (axis.LengthSquared() > 1e-12f)
            {
                axis *= 1.0f / MathF.Sqrt(axis.LengthSquared());
            }

            Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
            Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);
            Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
            Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
            return Vec2.Dot(vB - vA, axis);
        }

        ref readonly RevoluteJointData revolute = ref _revolute.GetBySparseIndex(id.SlotIndex);
        return bodies.Slot(revolute.BodyB).AngularVelocity - bodies.Slot(revolute.BodyA).AngularVelocity;
    }

    /// <summary>Reads the effective mass of a geared coordinate with sleep-gated inverses.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal float GetEffectiveCoordinateMass(BodyStore bodies, JointId id)
    {
        if (id.Kind == JointKind.Prismatic)
        {
            ref readonly PrismaticJointData data = ref _prismatic.GetBySparseIndex(id.SlotIndex);
            ref readonly BodyState stateA = ref bodies.Slot(data.BodyA);
            ref readonly BodyState stateB = ref bodies.Slot(data.BodyB);

            bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
            bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
            float invMassA = awakeA ? stateA.InverseMass : 0.0f;
            float invMassB = awakeB ? stateB.InverseMass : 0.0f;
            float invIA = awakeA ? stateA.InverseInertia : 0.0f;
            float invIB = awakeB ? stateB.InverseInertia : 0.0f;

            Vec2 axis = MathPhysics.Rotate(stateA, data.LocalAxisA);
            if (axis.LengthSquared() > 1e-12f)
            {
                axis *= 1.0f / MathF.Sqrt(axis.LengthSquared());
            }

            Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
            Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);
            float rnA = Vec2.Cross(rA, axis);
            float rnB = Vec2.Cross(rB, axis);
            return invMassA + invMassB + (invIA * rnA * rnA) + (invIB * rnB * rnB);
        }

        ref readonly RevoluteJointData revolute = ref _revolute.GetBySparseIndex(id.SlotIndex);
        ref readonly BodyState a = ref bodies.Slot(revolute.BodyA);
        ref readonly BodyState b = ref bodies.Slot(revolute.BodyB);
        float iA = (a.Flags & BodyFlags.Awake) != 0 ? a.InverseInertia : 0.0f;
        float iB = (b.Flags & BodyFlags.Awake) != 0 ? b.InverseInertia : 0.0f;
        return iA + iB;
    }

    /// <summary>Applies a generalized velocity impulse to a geared coordinate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void ApplyGearedVelocity(BodyStore bodies, JointId id, float impulse)
    {
        if (id.Kind == JointKind.Prismatic)
        {
            ref readonly PrismaticJointData data = ref _prismatic.GetBySparseIndex(id.SlotIndex);
            ref BodyState stateA = ref bodies.Slot(data.BodyA);
            ref BodyState stateB = ref bodies.Slot(data.BodyB);

            bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
            bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
            float invMassA = awakeA ? stateA.InverseMass : 0.0f;
            float invMassB = awakeB ? stateB.InverseMass : 0.0f;
            float invIA = awakeA ? stateA.InverseInertia : 0.0f;
            float invIB = awakeB ? stateB.InverseInertia : 0.0f;

            Vec2 axis = MathPhysics.Rotate(stateA, data.LocalAxisA);
            if (axis.LengthSquared() > 1e-12f)
            {
                axis *= 1.0f / MathF.Sqrt(axis.LengthSquared());
            }

            Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
            Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);
            Vec2 applied = axis * impulse;

            if (awakeA)
            {
                stateA.LinearVelocity -= applied * invMassA;
                stateA.AngularVelocity -= invIA * Vec2.Cross(rA, applied);
            }

            if (awakeB)
            {
                stateB.LinearVelocity += applied * invMassB;
                stateB.AngularVelocity += invIB * Vec2.Cross(rB, applied);
            }

            return;
        }

        ref readonly RevoluteJointData revolute = ref _revolute.GetBySparseIndex(id.SlotIndex);
        ref BodyState revoluteA = ref bodies.Slot(revolute.BodyA);
        ref BodyState revoluteB = ref bodies.Slot(revolute.BodyB);
        bool revoluteAwakeA = (revoluteA.Flags & BodyFlags.Awake) != 0;
        bool revoluteAwakeB = (revoluteB.Flags & BodyFlags.Awake) != 0;
        float revoluteInvIA = revoluteAwakeA ? revoluteA.InverseInertia : 0.0f;
        float revoluteInvIB = revoluteAwakeB ? revoluteB.InverseInertia : 0.0f;

        if (revoluteAwakeA)
        {
            revoluteA.AngularVelocity -= revoluteInvIA * impulse;
        }

        if (revoluteAwakeB)
        {
            revoluteB.AngularVelocity += revoluteInvIB * impulse;
        }
    }

    /// <summary>Applies a generalized position correction to a geared coordinate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void ApplyGearedPosition(BodyStore bodies, JointId id, float correction)
    {
        if (id.Kind == JointKind.Prismatic)
        {
            ref readonly PrismaticJointData data = ref _prismatic.GetBySparseIndex(id.SlotIndex);
            ref BodyState stateA = ref bodies.Slot(data.BodyA);
            ref BodyState stateB = ref bodies.Slot(data.BodyB);

            bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
            bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
            if (!awakeA && !awakeB)
            {
                return;
            }

            float invMassA = awakeA ? stateA.InverseMass : 0.0f;
            float invMassB = awakeB ? stateB.InverseMass : 0.0f;
            float invIA = awakeA ? stateA.InverseInertia : 0.0f;
            float invIB = awakeB ? stateB.InverseInertia : 0.0f;

            Vec2 axis = MathPhysics.Rotate(stateA, data.LocalAxisA);
            if (axis.LengthSquared() > 1e-12f)
            {
                axis *= 1.0f / MathF.Sqrt(axis.LengthSquared());
            }

            Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
            Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);
            MathPhysics.ApplyPositionCorrection(ref stateA, ref stateB, rA, rB, axis * correction,
                invMassA, invMassB, invIA, invIB, awakeA, awakeB);
            return;
        }

        ref readonly RevoluteJointData revolute = ref _revolute.GetBySparseIndex(id.SlotIndex);
        ref BodyState revoluteA = ref bodies.Slot(revolute.BodyA);
        ref BodyState revoluteB = ref bodies.Slot(revolute.BodyB);

        bool revoluteAwakeA = (revoluteA.Flags & BodyFlags.Awake) != 0;
        bool revoluteAwakeB = (revoluteB.Flags & BodyFlags.Awake) != 0;
        if (!revoluteAwakeA && !revoluteAwakeB)
        {
            return;
        }

        float revoluteInvIA = revoluteAwakeA ? revoluteA.InverseInertia : 0.0f;
        float revoluteInvIB = revoluteAwakeB ? revoluteB.InverseInertia : 0.0f;
        float axialMass = revoluteInvIA + revoluteInvIB;
        if (axialMass <= 0.0f)
        {
            return;
        }

        if (revoluteAwakeA)
        {
            float angleA = MathF.Atan2(revoluteA.Sin, revoluteA.Cos) - (revoluteInvIA * correction);
            revoluteA.Cos = MathF.Cos(angleA);
            revoluteA.Sin = MathF.Sin(angleA);
        }

        if (revoluteAwakeB)
        {
            float angleB = MathF.Atan2(revoluteB.Sin, revoluteB.Cos) + (revoluteInvIB * correction);
            revoluteB.Cos = MathF.Cos(angleB);
            revoluteB.Sin = MathF.Sin(angleB);
        }
    }

    /// <summary>Wakes every body coupled by a geared joint reference.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WakeGearedBodies(BodyStore bodies, JointId id)
    {
        if (id.Kind == JointKind.Prismatic)
        {
            ref readonly PrismaticJointData prismatic = ref _prismatic.GetBySparseIndex(id.SlotIndex);
            bodies.SetAwake(prismatic.BodyA, true);
            bodies.SetAwake(prismatic.BodyB, true);
        }
        else if (id.Kind == JointKind.Revolute)
        {
            ref readonly RevoluteJointData revolute = ref _revolute.GetBySparseIndex(id.SlotIndex);
            bodies.SetAwake(revolute.BodyA, true);
            bodies.SetAwake(revolute.BodyB, true);
        }
    }

    /// <summary>Runs the policy init over every packed table.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void InitVelocityPass(BodyStore bodies, float wakeTolerance, float dt)
    {
        _distance.InitVelocity(bodies, this, wakeTolerance, dt);
        _revolute.InitVelocity(bodies, this, wakeTolerance, dt);
        _weld.InitVelocity(bodies, this, wakeTolerance, dt);
        _prismatic.InitVelocity(bodies, this, wakeTolerance, dt);
        _motor.InitVelocity(bodies, this, wakeTolerance, dt);
        _wheel.InitVelocity(bodies, this, wakeTolerance, dt);
        _pulley.InitVelocity(bodies, this, wakeTolerance, dt);
        _gear.InitVelocity(bodies, this, wakeTolerance, dt);
    }

    /// <summary>Runs one velocity sweep over every packed table.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void SolveVelocitySweep(BodyStore bodies)
    {
        _distance.SolveVelocity(bodies, this);
        _revolute.SolveVelocity(bodies, this);
        _weld.SolveVelocity(bodies, this);
        _prismatic.SolveVelocity(bodies, this);
        _motor.SolveVelocity(bodies, this);
        _wheel.SolveVelocity(bodies, this);
        _pulley.SolveVelocity(bodies, this);
        _gear.SolveVelocity(bodies, this);
    }

    /// <summary>Runs one position sweep over every packed table.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void SolvePositionSweep(BodyStore bodies)
    {
        _distance.SolvePosition(bodies, this);
        _revolute.SolvePosition(bodies, this);
        _weld.SolvePosition(bodies, this);
        _prismatic.SolvePosition(bodies, this);
        _wheel.SolvePosition(bodies, this);
        _pulley.SolvePosition(bodies, this);
        _gear.SolvePosition(bodies, this);
    }

    /// <summary>Visits every live joint across all packed tables.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Accept<TVisitor, TContext>(ref TVisitor visitor, ref TContext context)
        where TVisitor : struct, IJointVisitor<TContext>
        where TContext : allows ref struct
    {
        AcceptFrom(_distance, JointKind.Distance, ref visitor, ref context);
        AcceptFrom(_revolute, JointKind.Revolute, ref visitor, ref context);
        AcceptFrom(_weld, JointKind.Weld, ref visitor, ref context);
        AcceptFrom(_prismatic, JointKind.Prismatic, ref visitor, ref context);
        AcceptFrom(_motor, JointKind.Motor, ref visitor, ref context);
        AcceptFrom(_wheel, JointKind.Wheel, ref visitor, ref context);
        AcceptFrom(_pulley, JointKind.Pulley, ref visitor, ref context);
        AcceptFrom(_gear, JointKind.Gear, ref visitor, ref context);
    }

    private static void AcceptFrom<TPolicy, TData, TVisitor, TContext>(
        JointStorage<TPolicy, TData> storage, JointKind kind, ref TVisitor visitor, ref TContext context)
        where TPolicy : struct, IJointPolicy<TPolicy, TData>
        where TData : struct
        where TVisitor : struct, IJointVisitor<TContext>
        where TContext : allows ref struct
    {
        ReadOnlySpan<TData> span = storage.DataSpan;
        for (int i = 0; i < span.Length; i++)
        {
            visitor.Visit<TPolicy, TData>(
                new JointId(kind, storage.SparseOfDense(i)), ref Unsafe.AsRef(in span[i]), ref context);
        }
    }
}

#endregion

#region Zero-Vtable Sequential-Impulse Solver

/// <summary>
/// Sequential-impulse joint solver: init freezes the effective masses and
/// warm-starts the stored impulses (waking sleeping dynamics on real relative
/// motion), then sweeps hold every constraint in velocity and position.
/// Sleeping endpoints read as infinite mass.
/// </summary>
public static class JointSolver
{
    /// <summary>
    /// Per-step init: wakes sleeping dynamics on genuine relative motion and
    /// applies the stored impulses to velocities. Call once per step before
    /// the iteration loop.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(BodyStore bodies, JointStore joints, float wakeTolerance, float dt)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        ArgumentNullException.ThrowIfNull(joints);

        joints.InitVelocityPass(bodies, wakeTolerance, dt);
    }

    /// <summary>Runs <paramref name="iterations"/> sweeps of velocity impulses.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocityPass(BodyStore bodies, JointStore joints, int iterations)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        ArgumentNullException.ThrowIfNull(joints);

        for (int i = 0; i < iterations; i++)
        {
            joints.SolveVelocitySweep(bodies);
        }
    }

    /// <summary>Runs <paramref name="iterations"/> sweeps of position correction.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void SolvePositionPass(BodyStore bodies, JointStore joints, int iterations)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        ArgumentNullException.ThrowIfNull(joints);

        for (int i = 0; i < iterations; i++)
        {
            joints.SolvePositionSweep(bodies);
        }
    }
}

#endregion
