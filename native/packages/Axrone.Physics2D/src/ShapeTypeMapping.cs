namespace Axrone.Physics2D;

/// <summary>
/// Bridges stored collider kinds to the shared <see cref="ShapeType"/> contract.
/// </summary>
public static class ColliderKindMapping
{
    /// <summary>Maps a stored kind to the shared contract.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShapeType ToShapeType(this ColliderKind kind) => kind switch
    {
        ColliderKind.Circle => ShapeType.Circle,
        ColliderKind.Capsule => ShapeType.Capsule,
        ColliderKind.Polygon => ShapeType.Polygon,
        ColliderKind.Edge => ShapeType.Segment,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown collider kind."),
    };
}
