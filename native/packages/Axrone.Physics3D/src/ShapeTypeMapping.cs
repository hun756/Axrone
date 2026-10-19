namespace Axrone.Physics3D;

/// <summary>
/// Bridges stored collider kinds to the shared <see cref="ShapeType3D"/> contract.
/// </summary>
public static class ColliderKindMapping
{
    /// <summary>Maps a stored kind to the shared contract.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShapeType3D ToShapeType(this ColliderKind kind) => kind switch
    {
        ColliderKind.Sphere => ShapeType3D.Sphere,
        ColliderKind.Box => ShapeType3D.Box,
        ColliderKind.Capsule => ShapeType3D.Capsule,
        ColliderKind.Cylinder => ShapeType3D.Cylinder,
        ColliderKind.Cone => ShapeType3D.Cone,
        ColliderKind.Hull => ShapeType3D.ConvexHull,
        ColliderKind.Mesh => ShapeType3D.TriangleMesh,
        ColliderKind.Heightfield => ShapeType3D.HeightField,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown collider kind."),
    };
}
