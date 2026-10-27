namespace Axrone.Scene.Runtime;

using Axrone.Physics3D;

/// <summary>Body and shape pair created for one terrain chunk.</summary>
/// <param name="Body">Static physics body.</param>
/// <param name="Shape">Heightfield shape.</param>
public readonly record struct TerrainPhysicsAttachment(BodyId Body, ShapeId Shape);

/// <summary>
/// Attaches terrain chunk heightfields to the 3D physics world as static bodies. The
/// heightfield local frame is origin-centered, so the body position is the chunk center.
/// </summary>
public static class SceneTerrainPhysicsBinder
{
    /// <summary>Creates a static body and attaches the chunk heightfield.</summary>
    /// <param name="bridge">Scene physics bridge.</param>
    /// <param name="source">Chunk heightfield source.</param>
    /// <param name="center">Chunk center in world space.</param>
    /// <param name="material">Surface material.</param>
    /// <param name="isSensor">Whether the heightfield reports overlaps only.</param>
    /// <returns>The created body and shape.</returns>
    public static TerrainPhysicsAttachment Attach(
        ScenePhysicsBridge3D bridge,
        TerrainHeightfieldSource source,
        Vec3 center,
        PhysicsMaterial material = default,
        bool isSensor = false)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(source);

        var bodyDefinition = new BodyDef
        {
            Type = BodyType.Static,
            Position = center,
        };

        BodyId body = bridge.PhysicsWorld.Bodies.Create(in bodyDefinition);
        try
        {
            ShapeId shape = bridge.PhysicsWorld.Shapes.AttachHeightfield<
                SurfaceAreaHeuristicStrategy,
                NullSpatialMetricsSink>(
                bridge.PhysicsWorld.Bodies,
                bridge.PhysicsWorld.Broadphase,
                body,
                source.Heights,
                source.Width,
                source.Depth,
                source.ScaleX,
                source.ScaleY,
                source.ScaleZ,
                1f,
                default,
                material,
                default,
                Quat.Identity,
                isSensor);

            return new TerrainPhysicsAttachment(body, shape);
        }
        catch
        {
            bridge.PhysicsWorld.Bodies.Destroy(body);
            throw;
        }
    }

    /// <summary>Detaches the heightfield shape and destroys the body.</summary>
    /// <param name="bridge">Scene physics bridge.</param>
    /// <param name="attachment">Attachment returned by <see cref="Attach"/>.</param>
    public static void Detach(ScenePhysicsBridge3D bridge, in TerrainPhysicsAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(bridge);

        bridge.PhysicsWorld.Shapes.Detach<
            SurfaceAreaHeuristicStrategy,
            NullSpatialMetricsSink>(
            bridge.PhysicsWorld.Bodies,
            bridge.PhysicsWorld.Broadphase,
            attachment.Shape);

        bridge.PhysicsWorld.Bodies.Destroy(attachment.Body);
    }
}
