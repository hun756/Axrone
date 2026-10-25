namespace Axrone.Scene.Rendering;

/// <summary>
/// Collects visible mesh renderers for one pass, resolves bounds, frustum-culls
/// and sorts the queue. The item array is reused and only grows on structural
/// changes, so a warmed frame allocates nothing.
/// </summary>
public sealed class SceneRenderItemCollector
{
    private SceneRenderItem[] _items;
    private int _count;

    /// <summary>Creates a collector with an initial item capacity.</summary>
    public SceneRenderItemCollector(int initialCapacity = 256)
    {
        if (initialCapacity <= 0)
        {
            throw new SceneConfigurationException(
                $"Render-item capacity must be positive; received {initialCapacity}.");
        }

        _items = new SceneRenderItem[Math.Max(initialCapacity, 16)];
    }

    /// <summary>Current number of planned items.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    /// <summary>Planned items in queue order.</summary>
    public ReadOnlySpan<SceneRenderItem> Items
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _items.AsSpan(0, _count);
    }

    /// <summary>Collects and sorts render items for one pass.</summary>
    /// <returns>Number of planned items.</returns>
    public int Collect(
        SceneWorld world,
        SceneCameraFrame cameraFrame,
        SceneAssetId<RenderPassAssetTag> passId,
        SceneAssetRegistry? assets = null,
        bool cull = true)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cameraFrame);

        _count = 0;
        EnsureCapacity(Math.Max(world.ActorCount, 16));

        CameraFrustum frustum = cameraFrame.Frustum;
        Vec3 cameraPosition = cameraFrame.Position;

        foreach (SceneActor actor in world.Actors)
        {
            if (!world.IsActiveInHierarchy(actor) ||
                !world.TryGetData(actor, out SceneMeshRenderer renderer) ||
                !renderer.Visible ||
                !renderer.MeshId.IsValid ||
                !renderer.MaterialId.IsValid ||
                renderer.PassId != passId)
            {
                continue;
            }

            BoundingSphere3 worldBounds = default;
            bool hasWorldBounds = false;
            BoundingSphere3 localBounds = default;
            bool hasLocalBounds = false;

            if (renderer.Bounds is { } explicitBounds)
            {
                localBounds = explicitBounds;
                hasLocalBounds = true;
            }
            else if (assets is not null &&
                     assets.Meshes.TryGet(renderer.MeshId, out SceneMeshDefinition? mesh) &&
                     mesh.Bounds is { } meshBounds)
            {
                localBounds = meshBounds;
                hasLocalBounds = true;
            }

            if (hasLocalBounds)
            {
                Mat4 worldMatrix = world.GetWorldMatrix3D(actor);
                Vec4 center = worldMatrix * new Vec4(localBounds.Center, 1.0f);
                float scaleX = MathF.Sqrt(
                    (worldMatrix.M11 * worldMatrix.M11) +
                    (worldMatrix.M12 * worldMatrix.M12) +
                    (worldMatrix.M13 * worldMatrix.M13));
                float scaleY = MathF.Sqrt(
                    (worldMatrix.M21 * worldMatrix.M21) +
                    (worldMatrix.M22 * worldMatrix.M22) +
                    (worldMatrix.M23 * worldMatrix.M23));
                float scaleZ = MathF.Sqrt(
                    (worldMatrix.M31 * worldMatrix.M31) +
                    (worldMatrix.M32 * worldMatrix.M32) +
                    (worldMatrix.M33 * worldMatrix.M33));
                float maxScale = MathF.Max(scaleX, MathF.Max(scaleY, scaleZ));
                worldBounds = new BoundingSphere3(
                    new Vec3(center.X, center.Y, center.Z),
                    localBounds.Radius * maxScale);
                hasWorldBounds = true;
            }

            if (cull && hasWorldBounds && !frustum.IntersectsSphere(in worldBounds))
            {
                continue;
            }

            Vec3 worldPosition = world.GetWorldPosition3D(actor);
            float dx = worldPosition.X - cameraPosition.X;
            float dy = worldPosition.Y - cameraPosition.Y;
            float dz = worldPosition.Z - cameraPosition.Z;

            if (_count == _items.Length)
            {
                Grow();
            }

            ref SceneRenderItem item = ref _items[_count];
            item.Actor = actor;
            item.MeshId = renderer.MeshId;
            item.MaterialId = renderer.MaterialId;
            item.RenderOrder = renderer.RenderOrder;
            item.DistanceSquared = (dx * dx) + (dy * dy) + (dz * dz);
            item.Transparent = renderer.Transparent;
            item.ReceiveLighting = renderer.ReceiveLighting;
            item.HasBounds = hasWorldBounds;
            item.WorldBounds = worldBounds;
            _count++;
        }

        _items.AsSpan(0, _count).Sort(new RenderItemComparer());
        return _count;
    }

    /// <summary>Drops the current plan without releasing the buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => _count = 0;

    private void EnsureCapacity(int required)
    {
        if (required <= _items.Length)
        {
            return;
        }

        GrowTo(required);
    }

    private void Grow()
    {
        if (_items.Length > int.MaxValue / 2)
        {
            throw new SceneConfigurationException("Render-item capacity overflow.");
        }

        GrowTo(_items.Length * 2);
    }

    private void GrowTo(int required)
    {
        int capacity = _items.Length;
        while (capacity < required)
        {
            if (capacity > int.MaxValue / 2)
            {
                throw new SceneConfigurationException("Render-item capacity overflow.");
            }

            capacity *= 2;
        }

        Array.Resize(ref _items, capacity);
    }

    private struct RenderItemComparer : IComparer<SceneRenderItem>
    {
        public int Compare(SceneRenderItem left, SceneRenderItem right)
        {
            int order = left.RenderOrder.CompareTo(right.RenderOrder);
            if (order != 0)
            {
                return order;
            }

            int queue = left.Transparent.CompareTo(right.Transparent);
            if (queue != 0)
            {
                return queue;
            }

            if (left.Transparent)
            {
                int distance = right.DistanceSquared.CompareTo(left.DistanceSquared);
                if (distance != 0)
                {
                    return distance;
                }
            }

            int material = left.MaterialId.CompareTo(right.MaterialId);
            if (material != 0)
            {
                return material;
            }

            int mesh = left.MeshId.CompareTo(right.MeshId);
            return mesh != 0 ? mesh : left.Actor.CompareTo(right.Actor);
        }
    }
}
