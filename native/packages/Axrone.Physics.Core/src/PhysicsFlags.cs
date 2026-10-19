namespace Axrone.Physics.Core;

/// <summary>
/// Solver capability toggles carried by the world definition.
/// </summary>
[Flags]
public enum SolverFlags : byte
{
    /// <summary>No capabilities.</summary>
    None = 0,

    /// <summary>Carry impulses across steps.</summary>
    WarmStarting = 1 << 0,

    /// <summary>Sweep fast bodies against thin geometry.</summary>
    ContinuousDetection = 1 << 1,

    /// <summary>Split the step into substeps.</summary>
    SubStepping = 1 << 2,

    /// <summary>Let quiet islands rest.</summary>
    SleepingBodies = 1 << 3,

    /// <summary>Push positions apart with Baumgarte stabilization.</summary>
    PositionCorrection = 1 << 4,

    /// <summary>Solve velocity constraints.</summary>
    VelocityConstraints = 1 << 5,

    /// <summary>Steady-state default: warm start, sleep, correct, constrain.</summary>
    Default = WarmStarting | SleepingBodies | PositionCorrection | VelocityConstraints,
}

/// <summary>
/// Stored body flags; kept in one byte alongside the type.
/// Shared layout so 2D and 3D stores read the same bits.
/// </summary>
[Flags]
public enum BodyFlags : byte
{
    /// <summary>No flags set.</summary>
    None = 0,

    /// <summary>Rotation is locked; angular velocity stays zero.</summary>
    FixedRotation = 1 << 0,

    /// <summary>Continuous collision detection hint; the solver treats motion as a sweep.</summary>
    Bullet = 1 << 1,

    /// <summary>Body-level sensor marker.</summary>
    Sensor = 1 << 2,

    /// <summary>Body is at rest and skipped by the solver.</summary>
    Sleeping = 1 << 3,

    /// <summary>The body may fall asleep when quiet.</summary>
    AutoSleep = 1 << 4,

    /// <summary>The body is awake and simulated.</summary>
    Awake = 1 << 5,

    /// <summary>The body participates in the current step.</summary>
    Active = 1 << 6,

    /// <summary>The body is assigned to an island.</summary>
    Island = 1 << 7,
}

/// <summary>
/// Well-known collision categories for filter presets.
/// </summary>
public enum CollisionCategory : uint
{
    /// <summary>Collides with nothing.</summary>
    None = 0,

    /// <summary>Default category.</summary>
    Default = 1,

    /// <summary>Static geometry.</summary>
    Static = 2,

    /// <summary>Dynamic bodies.</summary>
    Dynamic = 4,

    /// <summary>Kinematic bodies.</summary>
    Kinematic = 8,

    /// <summary>Trigger volumes.</summary>
    Trigger = 16,

    /// <summary>Collides with everything.</summary>
    All = 0xFFFF,
}
