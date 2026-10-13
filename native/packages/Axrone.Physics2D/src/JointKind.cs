namespace Axrone.Physics2D;

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
