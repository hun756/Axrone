namespace Axrone.Physics3D;

/// <summary>Joint archetypes; encoded in the upper bits of <see cref="JointId"/>.</summary>
public enum JointKind : byte
{
    /// <summary>Rigid rod between two anchors.</summary>
    Distance = 0,

    /// <summary>Point-to-point coincidence.</summary>
    BallSocket = 1,

    /// <summary>Full rigid lock between two frames.</summary>
    Weld = 2,

    /// <summary>Hinge around one axis.</summary>
    Hinge = 3,

    /// <summary>Slide along one axis.</summary>
    Prismatic = 4,

    /// <summary>Swing cone with twist stops.</summary>
    ConeTwist = 5,

    /// <summary>Per-axis locked, limited or driven motion.</summary>
    Configurable = 6,

    /// <summary>Force-based spring-damper along an axis pair.</summary>
    Spring = 7,
}
