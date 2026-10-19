namespace Axrone.Physics.Core;

/// <summary>
/// Unified 2D shape discriminant mirroring the web physics-core contract.
/// </summary>
public enum ShapeType : byte
{
    /// <summary>Disc collider.</summary>
    Circle = 0,

    /// <summary>Stadium collider.</summary>
    Capsule = 1,

    /// <summary>Convex polygon collider.</summary>
    Polygon = 2,

    /// <summary>Axis-aligned box collider.</summary>
    Box = 3,

    /// <summary>Thin static segment collider.</summary>
    Segment = 4,

    /// <summary>Ball collider.</summary>
    Sphere = 5,

    /// <summary>Cylinder collider.</summary>
    Cylinder = 6,

    /// <summary>Cone collider.</summary>
    Cone = 7,

    /// <summary>Convex point-cloud collider.</summary>
    ConvexHull = 8,

    /// <summary>Triangle-soup collider.</summary>
    TriangleMesh = 9,

    /// <summary>Terrain-grid collider.</summary>
    HeightField = 10,
}

/// <summary>
/// Unified 2D constraint discriminant mirroring the web physics-core contract.
/// </summary>
public enum ConstraintType : byte
{
    /// <summary>Rigid rod between two anchors.</summary>
    Distance = 0,

    /// <summary>Pinned hinge with optional limits and motor.</summary>
    Revolute = 1,

    /// <summary>Slider along an axis.</summary>
    Prismatic = 2,

    /// <summary>Locked relative pose.</summary>
    Weld = 3,

    /// <summary>Sprung suspension along an axis.</summary>
    Wheel = 4,

    /// <summary>Servo driving a body toward a moving target frame.</summary>
    Motor = 5,

    /// <summary>Spring toward a world-space target with capped force.</summary>
    Mouse = 6,

    /// <summary>Ratio between two revolute/prismatic coordinates.</summary>
    Gear = 7,

    /// <summary>Max-distance tether between two anchors.</summary>
    Rope = 8,
}

/// <summary>
/// Unified 3D shape discriminant mirroring the web physics-core contract.
/// Values match <see cref="ShapeType"/> so 2D and 3D registries interop.
/// </summary>
public enum ShapeType3D : byte
{
    /// <summary>Stadium collider.</summary>
    Capsule = 1,

    /// <summary>Axis-aligned box collider.</summary>
    Box = 3,

    /// <summary>Ball collider.</summary>
    Sphere = 5,

    /// <summary>Cylinder collider.</summary>
    Cylinder = 6,

    /// <summary>Cone collider.</summary>
    Cone = 7,

    /// <summary>Convex point-cloud collider.</summary>
    ConvexHull = 8,

    /// <summary>Triangle-soup collider.</summary>
    TriangleMesh = 9,

    /// <summary>Terrain-grid collider.</summary>
    HeightField = 10,
}

/// <summary>
/// Unified 3D constraint discriminant mirroring the web physics-core contract.
/// </summary>
public enum ConstraintType3D : byte
{
    /// <summary>Full rigid lock between two frames.</summary>
    Fixed = 0,

    /// <summary>Point-to-point coincidence.</summary>
    Spherical = 1,

    /// <summary>Hinge around one axis.</summary>
    Hinge = 2,

    /// <summary>Slide along one axis.</summary>
    Slider = 3,

    /// <summary>Swing cone with twist stops.</summary>
    ConeTwist = 4,

    /// <summary>Per-axis locked, limited or driven motion.</summary>
    Generic = 5,

    /// <summary>Force-based spring-damper along an axis pair.</summary>
    Spring = 6,
}
