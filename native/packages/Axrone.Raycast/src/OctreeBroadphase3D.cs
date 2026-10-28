namespace Axrone.Raycast;

using Axrone.Geometry;
using Axrone.Numeric;

/// <summary>
/// Adapter that exposes <see cref="NativeOctree{T}"/> as a 3D raycast broadphase. The
/// adapter owns the generational handles, so callers index and remove items by payload.
/// </summary>
/// <typeparam name="T">Indexed item type.</typeparam>
public sealed class OctreeBroadphase3D<T> : IDisposable
    where T : unmanaged, IEquatable<T>
{
    private readonly NativeOctree<T> _tree;
    private readonly Dictionary<T, OctreeHandle> _handles = [];

    /// <summary>Creates a broadphase over a fixed root domain.</summary>
    /// <param name="rootBounds">Root domain; items outside it are rejected.</param>
    public OctreeBroadphase3D(in Aabb3D rootBounds)
    {
        if (!rootBounds.IsValid)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(rootBounds));
        }

        _tree = new NativeOctree<T>(in rootBounds);
    }

    /// <summary>Creates a broadphase over the default ±1024 domain.</summary>
    public OctreeBroadphase3D()
    {
        _tree = new NativeOctree<T>();
    }

    /// <summary>Gets the indexed item count.</summary>
    public int Count => _handles.Count;

    /// <summary>Gets the fixed root domain.</summary>
    public Aabb3D RootBounds => _tree.RootBounds;

    /// <summary>Adds an item; the item must not already be indexed.</summary>
    /// <param name="item">Item to index.</param>
    /// <param name="bounds">World bounds covered by the item.</param>
    public void Insert(T item, in Aabb3D bounds)
    {
        if (_handles.ContainsKey(item))
        {
            ThrowHelper.ThrowInvalidOperation("Broadphase item is already indexed.");
        }

        if (!bounds.IsValid)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(bounds));
        }

        OctreeHandle handle = _tree.Insert(in bounds, in item);
        _handles.Add(item, handle);
    }

    /// <summary>Removes an item.</summary>
    /// <param name="item">Item to remove.</param>
    /// <returns><see langword="true"/> when the item was indexed.</returns>
    public bool Remove(T item)
    {
        if (!_handles.Remove(item, out OctreeHandle handle))
        {
            return false;
        }

        return _tree.Remove(handle);
    }

    /// <summary>Replaces an item's bounds.</summary>
    /// <param name="item">Indexed item.</param>
    /// <param name="bounds">New world bounds.</param>
    /// <returns><see langword="false"/> when the item is not indexed.</returns>
    public bool Update(T item, in Aabb3D bounds)
    {
        if (!_handles.TryGetValue(item, out OctreeHandle handle))
        {
            return false;
        }

        if (!bounds.IsValid)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(bounds));
        }

        return _tree.Update(handle, in bounds, in item);
    }

    /// <summary>Gets whether an item is indexed.</summary>
    /// <param name="item">Item to test.</param>
    public bool Contains(T item) => _handles.ContainsKey(item);

    /// <summary>Collects items reached by a ray into a caller span.</summary>
    /// <param name="ray">Normalized query ray.</param>
    /// <param name="maxDistance">Positive distance limit.</param>
    /// <param name="results">Destination span.</param>
    /// <returns>The number of items written.</returns>
    public int QueryRay(in Ray3D ray, float maxDistance, Span<T> results)
    {
        ValidateDistance(maxDistance);
        var visitor = new RayVisitor<T>(results);
        _tree.Raycast(in ray, new Distance(maxDistance), ref visitor);
        return visitor.Count;
    }

    /// <summary>Reports items reached by a ray without allocating.</summary>
    /// <typeparam name="TVisitor">Ref-struct visitor.</typeparam>
    /// <param name="ray">Normalized query ray.</param>
    /// <param name="maxDistance">Positive distance limit.</param>
    /// <param name="visitor">Visitor receiving each item.</param>
    /// <returns><see langword="false"/> when the visitor asked to stop.</returns>
    public bool QueryRay<TVisitor>(in Ray3D ray, float maxDistance, ref TVisitor visitor)
        where TVisitor : IRaycastVisitor<T>, allows ref struct
    {
        ValidateDistance(maxDistance);
        return _tree.Raycast(in ray, new Distance(maxDistance), ref visitor);
    }

    /// <summary>Collects items overlapping a box into a caller span.</summary>
    /// <param name="bounds">Query box.</param>
    /// <param name="results">Destination span.</param>
    /// <returns>The number of items written.</returns>
    public int QueryBounds(in Aabb3D bounds, Span<T> results)
    {
        var visitor = new BoxVisitor<T>(results);
        _tree.QueryBox(in bounds, ref visitor);
        return visitor.Count;
    }

    /// <summary>Reports items overlapping a box without allocating.</summary>
    /// <typeparam name="TVisitor">Ref-struct visitor.</typeparam>
    /// <param name="bounds">Query box.</param>
    /// <param name="visitor">Visitor receiving each item.</param>
    /// <returns><see langword="false"/> when the visitor asked to stop.</returns>
    public bool QueryBounds<TVisitor>(in Aabb3D bounds, ref TVisitor visitor)
        where TVisitor : IOctreeVisitor<T>, allows ref struct =>
        _tree.QueryBox(in bounds, ref visitor);

    /// <summary>Removes every item.</summary>
    public void Clear()
    {
        _handles.Clear();
        _tree.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _handles.Clear();
        _tree.Dispose();
    }

    private static void ValidateDistance(float maxDistance)
    {
        if (!float.IsFinite(maxDistance) || maxDistance < 0f)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(maxDistance));
        }
    }

    private ref struct RayVisitor<TItem> : IRaycastVisitor<TItem>
        where TItem : unmanaged
    {
        private readonly Span<TItem> _results;
        private int _count;

        public RayVisitor(Span<TItem> results)
        {
            _results = results;
            _count = 0;
        }

        public readonly int Count => _count;

        public bool OnHit(OctreeHandle handle, in Aabb3D bounds, in TItem value, Distance distance)
        {
            if ((uint)_count >= (uint)_results.Length)
            {
                return false;
            }

            _results[_count++] = value;
            return true;
        }
    }

    private ref struct BoxVisitor<TItem> : IOctreeVisitor<TItem>
        where TItem : unmanaged
    {
        private readonly Span<TItem> _results;
        private int _count;

        public BoxVisitor(Span<TItem> results)
        {
            _results = results;
            _count = 0;
        }

        public readonly int Count => _count;

        public bool OnVisit(OctreeHandle handle, in Aabb3D bounds, in TItem value)
        {
            if ((uint)_count >= (uint)_results.Length)
            {
                return false;
            }

            _results[_count++] = value;
            return true;
        }
    }
}
