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
    private readonly RenderItemComparer _comparer;

    /// <summary>Creates a collector with an initial item capacity.</summary>
    public SceneRenderItemCollector(int initialCapacity = 256)
    {
        if (initialCapacity <= 0)
        {
            throw new SceneConfigurationException(
                $"Render-item capacity must be positive; received {initialCapacity}.");
        }

        _items = new SceneRenderItem[Math.Max(initialCapacity, 16)];
        _comparer = new RenderItemComparer();
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

        SortPlannedItems();
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

    /// <summary>
    /// Allocation-free introsort over the planned queue. The BCL's span sort
    /// boxes the struct comparer (88 bytes per call at this writing), which
    /// would break the zero-allocation frame contract.
    /// </summary>
    private void SortPlannedItems()
    {
        int count = _count;
        if (count < 2)
        {
            return;
        }

        IntrospectiveSort(_items.AsSpan(0, count), 2 * BitOperations.Log2((uint)count));
    }

    private void IntrospectiveSort(Span<SceneRenderItem> span, int depthLimit)
    {
        while (span.Length > 16)
        {
            if (depthLimit == 0)
            {
                HeapSort(span);
                return;
            }

            depthLimit--;

            int pivot = Partition(span);
            Span<SceneRenderItem> right = span[(pivot + 1)..];
            Span<SceneRenderItem> left = span[..pivot];

            // Recurse into the smaller half; loop over the larger one.
            if (left.Length < right.Length)
            {
                IntrospectiveSort(left, depthLimit);
                span = right;
            }
            else
            {
                IntrospectiveSort(right, depthLimit);
                span = left;
            }
        }

        InsertionSort(span);
    }

    private int Partition(Span<SceneRenderItem> span)
    {
        int last = span.Length - 1;
        int mid = last >> 1;

        // Order first/middle/last, then park the median at the tail.
        if (Less(span[last], span[0]))
        {
            Swap(span, 0, last);
        }

        if (Less(span[mid], span[0]))
        {
            Swap(span, 0, mid);
        }

        if (Less(span[last], span[mid]))
        {
            Swap(span, mid, last);
        }

        SceneRenderItem pivot = span[mid];
        Swap(span, mid, last);

        int store = 0;
        for (int i = 0; i < last; i++)
        {
            if (Less(span[i], pivot))
            {
                Swap(span, i, store);
                store++;
            }
        }

        Swap(span, store, last);
        return store;
    }

    private void InsertionSort(Span<SceneRenderItem> span)
    {
        for (int i = 1; i < span.Length; i++)
        {
            SceneRenderItem item = span[i];
            int j = i - 1;

            while (j >= 0 && Less(item, span[j]))
            {
                span[j + 1] = span[j];
                j--;
            }

            span[j + 1] = item;
        }
    }

    private void HeapSort(Span<SceneRenderItem> span)
    {
        int count = span.Length;

        for (int root = (count >> 1) - 1; root >= 0; root--)
        {
            SiftDown(span, root, count);
        }

        for (int end = count - 1; end > 0; end--)
        {
            Swap(span, 0, end);
            SiftDown(span, 0, end);
        }
    }

    private void SiftDown(Span<SceneRenderItem> span, int root, int count)
    {
        while (true)
        {
            int child = (root << 1) + 1;
            if (child >= count)
            {
                return;
            }

            if (child + 1 < count && Less(span[child], span[child + 1]))
            {
                child++;
            }

            if (!Less(span[root], span[child]))
            {
                return;
            }

            Swap(span, root, child);
            root = child;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Less(in SceneRenderItem left, in SceneRenderItem right) =>
        _comparer.Compare(left, right) < 0;

    private static void Swap(Span<SceneRenderItem> span, int left, int right)
    {
        SceneRenderItem temp = span[left];
        span[left] = span[right];
        span[right] = temp;
    }

    private readonly struct RenderItemComparer : IComparer<SceneRenderItem>
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
