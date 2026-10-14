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
}
