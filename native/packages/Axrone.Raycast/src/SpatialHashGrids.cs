namespace Axrone.Raycast;

using Axrone.Geometry;
using Axrone.Numeric;

/// <summary>
/// Visitor over the items one broadphase cell contains. The interface is generic and
/// consumed through a <c>ref</c> parameter, so a query never allocates a delegate or a
/// closure; the engine's ref-struct query state implements it directly.
/// </summary>
/// <typeparam name="T">Indexed item type.</typeparam>
public interface ISpatialHashItemVisitor<T>
    where T : unmanaged, IEquatable<T>
{
    /// <summary>Reports one indexed item.</summary>
    /// <returns><see langword="true"/> to keep walking; <see langword="false"/> to stop early.</returns>
    bool OnItem(T item);
}

/// <summary>
/// Uniform-grid index of 2D boxes used as the 2D raycast broadphase. Insertion and
/// removal allocate cell buckets; queries never allocate and walk the grid with an
/// Amanatides-Woo DDA when the ray form is used.
/// </summary>
/// <typeparam name="T">Indexed item type.</typeparam>
public sealed class SpatialHashGrid2D<T>
    where T : unmanaged, IEquatable<T>
{
    private readonly float _cellSize;
    private readonly float _invCellSize;
    private readonly Dictionary<CellKey, List<T>> _cells = [];
    private readonly Dictionary<T, Aabb2D> _bounds = [];

    /// <summary>Creates a grid.</summary>
    /// <param name="cellSize">Cell size in world units.</param>
    public SpatialHashGrid2D(float cellSize)
    {
        if (!float.IsFinite(cellSize) || cellSize <= 0f)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(cellSize));
        }

        _cellSize = cellSize;
        _invCellSize = 1f / cellSize;
    }

    /// <summary>Gets the number of non-empty cells.</summary>
    public int CellCount => _cells.Count;

    /// <summary>Gets the number of indexed items.</summary>
    public int ItemCount => _bounds.Count;

    /// <summary>Adds an item; the item must not already be indexed.</summary>
    /// <param name="item">Item to index.</param>
    /// <param name="bounds">World bounds covered by the item.</param>
    public void Insert(T item, in Aabb2D bounds)
    {
        if (_bounds.ContainsKey(item))
        {
            ThrowHelper.ThrowInvalidOperation("Broadphase item is already indexed.");
        }

        if (!bounds.IsValid)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(bounds));
        }

        _bounds.Add(item, bounds);
        InsertCells(item, in bounds);
    }

    /// <summary>Replaces the bounds of an indexed item.</summary>
    /// <param name="item">Indexed item.</param>
    /// <param name="bounds">New world bounds.</param>
    public void Update(T item, in Aabb2D bounds)
    {
        if (!_bounds.TryGetValue(item, out Aabb2D existing))
        {
            ThrowHelper.ThrowInvalidOperation("Broadphase item is not indexed.");
        }

        if (!bounds.IsValid)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(bounds));
        }

        RemoveCells(item, in existing);
        _bounds[item] = bounds;
        InsertCells(item, in bounds);
    }

    /// <summary>Removes an item.</summary>
    /// <param name="item">Item to remove.</param>
    /// <returns><see langword="true"/> when the item was indexed.</returns>
    public bool Remove(T item)
    {
        if (!_bounds.Remove(item, out Aabb2D bounds))
        {
            return false;
        }

        RemoveCells(item, in bounds);
        return true;
    }

    /// <summary>Gets whether an item is indexed.</summary>
    /// <param name="item">Item to test.</param>
    public bool Contains(T item) => _bounds.ContainsKey(item);

    /// <summary>
    /// Collects every item stored in a cell the box overlaps. An item that spans
    /// several cells is reported several times; use the visitor overload when identity
    /// matters.
    /// </summary>
    /// <param name="bounds">Query box.</param>
    /// <param name="results">Destination span.</param>
    /// <returns>The number of items written.</returns>
    public int Query(in Aabb2D bounds, Span<T> results)
    {
        if (!bounds.IsValid)
        {
            return 0;
        }

        var count = 0;
        CellRange range = GetCellRange(in bounds);
        for (var y = range.MinY; y <= range.MaxY; y++)
        {
            for (var x = range.MinX; x <= range.MaxX; x++)
            {
                if (!_cells.TryGetValue(new CellKey(x, y), out List<T>? bucket))
                {
                    continue;
                }

                Span<T> items = CollectionsMarshal.AsSpan(bucket);
                for (var index = 0; index < items.Length && count < results.Length; index++)
                {
                    results[count++] = items[index];
                }

                if (count == results.Length)
                {
                    return count;
                }
            }
        }

        return count;
    }

    /// <summary>Reports every item stored in a cell the box overlaps.</summary>
    /// <typeparam name="TVisitor">Ref-struct visitor.</typeparam>
    /// <param name="bounds">Query box.</param>
    /// <param name="visitor">Visitor receiving each item.</param>
    /// <returns><see langword="false"/> when the visitor asked to stop.</returns>
    public bool Query<TVisitor>(in Aabb2D bounds, ref TVisitor visitor)
        where TVisitor : ISpatialHashItemVisitor<T>, allows ref struct
    {
        if (!bounds.IsValid)
        {
            return true;
        }

        CellRange range = GetCellRange(in bounds);
        for (var y = range.MinY; y <= range.MaxY; y++)
        {
            for (var x = range.MinX; x <= range.MaxX; x++)
            {
                if (!VisitCell(x, y, ref visitor))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Reports every item the ray reaches, walking one cell at a time.</summary>
    /// <typeparam name="TVisitor">Ref-struct visitor.</typeparam>
    /// <param name="ray">Normalized query ray.</param>
    /// <param name="maxDistance">Positive distance limit.</param>
    /// <param name="visitor">Visitor receiving each item.</param>
    /// <returns><see langword="false"/> when the visitor asked to stop.</returns>
    public bool Raycast<TVisitor>(in Ray2D ray, float maxDistance, ref TVisitor visitor)
        where TVisitor : ISpatialHashItemVisitor<T>, allows ref struct
    {
        if (!float.IsFinite(maxDistance) || maxDistance < 0f)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(maxDistance));
        }

        float originX = ray.Origin.X;
        float originY = ray.Origin.Y;
        var cellX = (int)MathF.Floor(originX * _invCellSize);
        var cellY = (int)MathF.Floor(originY * _invCellSize);

        float directionX = ray.Direction.X;
        float directionY = ray.Direction.Y;
        int stepX = directionX > 0f ? 1 : directionX < 0f ? -1 : 0;
        int stepY = directionY > 0f ? 1 : directionY < 0f ? -1 : 0;
        float deltaX = stepX != 0 ? _cellSize / MathF.Abs(directionX) : float.PositiveInfinity;
        float deltaY = stepY != 0 ? _cellSize / MathF.Abs(directionY) : float.PositiveInfinity;
        float nextX = stepX != 0
            ? ((((stepX > 0 ? cellX + 1 : cellX) * _cellSize) - originX) / directionX)
            : float.PositiveInfinity;
        float nextY = stepY != 0
            ? ((((stepY > 0 ? cellY + 1 : cellY) * _cellSize) - originY) / directionY)
            : float.PositiveInfinity;

        while (true)
        {
            if (!VisitCell(cellX, cellY, ref visitor))
            {
                return false;
            }

            float boundary = MathF.Min(nextX, nextY);
            if (!(boundary <= maxDistance))
            {
                break;
            }

            if (nextX < nextY)
            {
                cellX += stepX;
                nextX += deltaX;
            }
            else if (nextY < nextX)
            {
                cellY += stepY;
                nextY += deltaY;
            }
            else
            {
                cellX += stepX;
                cellY += stepY;
                nextX += deltaX;
                nextY += deltaY;
            }
        }

        return true;
    }

    /// <summary>Removes every item.</summary>
    public void Clear()
    {
        _cells.Clear();
        _bounds.Clear();
    }

    private bool VisitCell<TVisitor>(int x, int y, ref TVisitor visitor)
        where TVisitor : ISpatialHashItemVisitor<T>, allows ref struct
    {
        if (!_cells.TryGetValue(new CellKey(x, y), out List<T>? bucket))
        {
            return true;
        }

        Span<T> items = CollectionsMarshal.AsSpan(bucket);
        for (var index = 0; index < items.Length; index++)
        {
            if (!visitor.OnItem(items[index]))
            {
                return false;
            }
        }

        return true;
    }

    private void InsertCells(T item, in Aabb2D bounds)
    {
        CellRange range = GetCellRange(in bounds);
        for (var y = range.MinY; y <= range.MaxY; y++)
        {
            for (var x = range.MinX; x <= range.MaxX; x++)
            {
                var key = new CellKey(x, y);
                if (!_cells.TryGetValue(key, out List<T>? bucket))
                {
                    bucket = [];
                    _cells.Add(key, bucket);
                }

                bucket.Add(item);
            }
        }
    }

    private void RemoveCells(T item, in Aabb2D bounds)
    {
        CellRange range = GetCellRange(in bounds);
        for (var y = range.MinY; y <= range.MaxY; y++)
        {
            for (var x = range.MinX; x <= range.MaxX; x++)
            {
                var key = new CellKey(x, y);
                if (!_cells.TryGetValue(key, out List<T>? bucket))
                {
                    continue;
                }

                bucket.Remove(item);
                if (bucket.Count == 0)
                {
                    _cells.Remove(key);
                }
            }
        }
    }

    private CellRange GetCellRange(in Aabb2D bounds)
    {
        var minX = (int)MathF.Floor(bounds.Min.X * _invCellSize);
        var minY = (int)MathF.Floor(bounds.Min.Y * _invCellSize);
        var maxX = (int)MathF.Floor(bounds.Max.X * _invCellSize);
        var maxY = (int)MathF.Floor(bounds.Max.Y * _invCellSize);
        return new CellRange(minX, minY, maxX, maxY);
    }

    private readonly record struct CellKey(int X, int Y);

    private readonly record struct CellRange(int MinX, int MinY, int MaxX, int MaxY);
}

/// <summary>
/// Uniform-grid index of 3D boxes used as the 3D raycast broadphase. Queries are
/// allocation-free; the ray form walks cells with the Amanatides-Woo DDA.
/// </summary>
/// <typeparam name="T">Indexed item type.</typeparam>
public sealed class SpatialHashGrid3D<T>
    where T : unmanaged, IEquatable<T>
{
    private readonly float _cellSize;
    private readonly float _invCellSize;
    private readonly Dictionary<CellKey, List<T>> _cells = [];
    private readonly Dictionary<T, Aabb3D> _bounds = [];

    /// <summary>Creates a grid.</summary>
    /// <param name="cellSize">Cell size in world units.</param>
    public SpatialHashGrid3D(float cellSize)
    {
        if (!float.IsFinite(cellSize) || cellSize <= 0f)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(cellSize));
        }

        _cellSize = cellSize;
        _invCellSize = 1f / cellSize;
    }

    /// <summary>Gets the number of non-empty cells.</summary>
    public int CellCount => _cells.Count;

    /// <summary>Gets the number of indexed items.</summary>
    public int ItemCount => _bounds.Count;

    /// <summary>Adds an item; the item must not already be indexed.</summary>
    /// <param name="item">Item to index.</param>
    /// <param name="bounds">World bounds covered by the item.</param>
    public void Insert(T item, in Aabb3D bounds)
    {
        if (_bounds.ContainsKey(item))
        {
            ThrowHelper.ThrowInvalidOperation("Broadphase item is already indexed.");
        }

        if (!bounds.IsValid)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(bounds));
        }

        _bounds.Add(item, bounds);
        InsertCells(item, in bounds);
    }

    /// <summary>Replaces the bounds of an indexed item.</summary>
    /// <param name="item">Indexed item.</param>
    /// <param name="bounds">New world bounds.</param>
    public void Update(T item, in Aabb3D bounds)
    {
        if (!_bounds.TryGetValue(item, out Aabb3D existing))
        {
            ThrowHelper.ThrowInvalidOperation("Broadphase item is not indexed.");
        }

        if (!bounds.IsValid)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(bounds));
        }

        RemoveCells(item, in existing);
        _bounds[item] = bounds;
        InsertCells(item, in bounds);
    }

    /// <summary>Removes an item.</summary>
    /// <param name="item">Item to remove.</param>
    /// <returns><see langword="true"/> when the item was indexed.</returns>
    public bool Remove(T item)
    {
        if (!_bounds.Remove(item, out Aabb3D bounds))
        {
            return false;
        }

        RemoveCells(item, in bounds);
        return true;
    }

    /// <summary>Gets whether an item is indexed.</summary>
    /// <param name="item">Item to test.</param>
    public bool Contains(T item) => _bounds.ContainsKey(item);

    /// <summary>
    /// Collects every item stored in a cell the box overlaps. An item that spans
    /// several cells is reported several times; use the visitor overload when identity
    /// matters.
    /// </summary>
    /// <param name="bounds">Query box.</param>
    /// <param name="results">Destination span.</param>
    /// <returns>The number of items written.</returns>
    public int Query(in Aabb3D bounds, Span<T> results)
    {
        if (!bounds.IsValid)
        {
            return 0;
        }

        var count = 0;
        CellRange range = GetCellRange(in bounds);
        for (var z = range.MinZ; z <= range.MaxZ; z++)
        {
            for (var y = range.MinY; y <= range.MaxY; y++)
            {
                for (var x = range.MinX; x <= range.MaxX; x++)
                {
                    if (!_cells.TryGetValue(new CellKey(x, y, z), out List<T>? bucket))
                    {
                        continue;
                    }

                    Span<T> items = CollectionsMarshal.AsSpan(bucket);
                    for (var index = 0; index < items.Length && count < results.Length; index++)
                    {
                        results[count++] = items[index];
                    }

                    if (count == results.Length)
                    {
                        return count;
                    }
                }
            }
        }

        return count;
    }

    /// <summary>Reports every item stored in a cell the box overlaps.</summary>
    /// <typeparam name="TVisitor">Ref-struct visitor.</typeparam>
    /// <param name="bounds">Query box.</param>
    /// <param name="visitor">Visitor receiving each item.</param>
    /// <returns><see langword="false"/> when the visitor asked to stop.</returns>
    public bool Query<TVisitor>(in Aabb3D bounds, ref TVisitor visitor)
        where TVisitor : ISpatialHashItemVisitor<T>, allows ref struct
    {
        if (!bounds.IsValid)
        {
            return true;
        }

        CellRange range = GetCellRange(in bounds);
        for (var z = range.MinZ; z <= range.MaxZ; z++)
        {
            for (var y = range.MinY; y <= range.MaxY; y++)
            {
                for (var x = range.MinX; x <= range.MaxX; x++)
                {
                    if (!VisitCell(x, y, z, ref visitor))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>Reports every item the ray reaches, walking one cell at a time.</summary>
    /// <typeparam name="TVisitor">Ref-struct visitor.</typeparam>
    /// <param name="ray">Normalized query ray.</param>
    /// <param name="maxDistance">Positive distance limit.</param>
    /// <param name="visitor">Visitor receiving each item.</param>
    /// <returns><see langword="false"/> when the visitor asked to stop.</returns>
    public bool Raycast<TVisitor>(in Ray3D ray, float maxDistance, ref TVisitor visitor)
        where TVisitor : ISpatialHashItemVisitor<T>, allows ref struct =>
        RaycastCore(in ray, maxDistance, default, false, ref visitor);

    /// <summary>
    /// Reports every item whose cells intersect a cast volume swept along the ray. Each
    /// DDA cell is widened by <paramref name="expansion"/>, which is the world half
    /// extents of the swept volume; the visitor may therefore receive the same item more
    /// than once unless it deduplicates.
    /// </summary>
    /// <typeparam name="TVisitor">Ref-struct visitor.</typeparam>
    /// <param name="ray">Normalized sweep ray.</param>
    /// <param name="maxDistance">Positive sweep limit.</param>
    /// <param name="expansion">World half extents of the swept volume.</param>
    /// <param name="visitor">Visitor receiving each item.</param>
    /// <returns><see langword="false"/> when the visitor asked to stop.</returns>
    public bool RaycastExpanded<TVisitor>(
        in Ray3D ray,
        float maxDistance,
        Vec3 expansion,
        ref TVisitor visitor)
        where TVisitor : ISpatialHashItemVisitor<T>, allows ref struct =>
        RaycastCore(in ray, maxDistance, expansion, true, ref visitor);

    private bool RaycastCore<TVisitor>(
        in Ray3D ray,
        float maxDistance,
        Vec3 expansion,
        bool expanded,
        ref TVisitor visitor)
        where TVisitor : ISpatialHashItemVisitor<T>, allows ref struct
    {
        if (!float.IsFinite(maxDistance) || maxDistance < 0f)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(maxDistance));
        }

        float originX = ray.Origin.X;
        float originY = ray.Origin.Y;
        float originZ = ray.Origin.Z;
        var cellX = (int)MathF.Floor(originX * _invCellSize);
        var cellY = (int)MathF.Floor(originY * _invCellSize);
        var cellZ = (int)MathF.Floor(originZ * _invCellSize);

        float directionX = ray.Direction.X;
        float directionY = ray.Direction.Y;
        float directionZ = ray.Direction.Z;
        int stepX = directionX > 0f ? 1 : directionX < 0f ? -1 : 0;
        int stepY = directionY > 0f ? 1 : directionY < 0f ? -1 : 0;
        int stepZ = directionZ > 0f ? 1 : directionZ < 0f ? -1 : 0;
        float deltaX = stepX != 0 ? _cellSize / MathF.Abs(directionX) : float.PositiveInfinity;
        float deltaY = stepY != 0 ? _cellSize / MathF.Abs(directionY) : float.PositiveInfinity;
        float deltaZ = stepZ != 0 ? _cellSize / MathF.Abs(directionZ) : float.PositiveInfinity;
        float nextX = stepX != 0
            ? ((((stepX > 0 ? cellX + 1 : cellX) * _cellSize) - originX) / directionX)
            : float.PositiveInfinity;
        float nextY = stepY != 0
            ? ((((stepY > 0 ? cellY + 1 : cellY) * _cellSize) - originY) / directionY)
            : float.PositiveInfinity;
        float nextZ = stepZ != 0
            ? ((((stepZ > 0 ? cellZ + 1 : cellZ) * _cellSize) - originZ) / directionZ)
            : float.PositiveInfinity;

        while (true)
        {
            bool keepGoing = expanded
                ? VisitExpandedCell(cellX, cellY, cellZ, expansion, ref visitor)
                : VisitCell(cellX, cellY, cellZ, ref visitor);
            if (!keepGoing)
            {
                return false;
            }

            float boundary = MathF.Min(nextX, MathF.Min(nextY, nextZ));
            if (!(boundary <= maxDistance))
            {
                break;
            }

            if (nextX < nextY)
            {
                if (nextX < nextZ)
                {
                    cellX += stepX;
                    nextX += deltaX;
                }
                else
                {
                    cellZ += stepZ;
                    nextZ += deltaZ;
                }
            }
            else if (nextY < nextZ)
            {
                cellY += stepY;
                nextY += deltaY;
            }
            else
            {
                cellZ += stepZ;
                nextZ += deltaZ;
            }
        }

        return true;
    }

    /// <summary>Removes every item.</summary>
    public void Clear()
    {
        _cells.Clear();
        _bounds.Clear();
    }

    private bool VisitCell<TVisitor>(int x, int y, int z, ref TVisitor visitor)
        where TVisitor : ISpatialHashItemVisitor<T>, allows ref struct
    {
        if (!_cells.TryGetValue(new CellKey(x, y, z), out List<T>? bucket))
        {
            return true;
        }

        Span<T> items = CollectionsMarshal.AsSpan(bucket);
        for (var index = 0; index < items.Length; index++)
        {
            if (!visitor.OnItem(items[index]))
            {
                return false;
            }
        }

        return true;
    }

    private bool VisitExpandedCell<TVisitor>(int x, int y, int z, Vec3 expansion, ref TVisitor visitor)
        where TVisitor : ISpatialHashItemVisitor<T>, allows ref struct
    {
        float minX = (x * _cellSize) - expansion.X;
        float minY = (y * _cellSize) - expansion.Y;
        float minZ = (z * _cellSize) - expansion.Z;
        float maxX = ((x + 1) * _cellSize) + expansion.X;
        float maxY = ((y + 1) * _cellSize) + expansion.Y;
        float maxZ = ((z + 1) * _cellSize) + expansion.Z;
        var expandedBounds = new Aabb3D(minX, minY, minZ, maxX, maxY, maxZ);
        CellRange range = GetCellRange(in expandedBounds);
        for (var cz = range.MinZ; cz <= range.MaxZ; cz++)
        {
            for (var cy = range.MinY; cy <= range.MaxY; cy++)
            {
                for (var cx = range.MinX; cx <= range.MaxX; cx++)
                {
                    if (!VisitCell(cx, cy, cz, ref visitor))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private void InsertCells(T item, in Aabb3D bounds)
    {
        CellRange range = GetCellRange(in bounds);
        for (var z = range.MinZ; z <= range.MaxZ; z++)
        {
            for (var y = range.MinY; y <= range.MaxY; y++)
            {
                for (var x = range.MinX; x <= range.MaxX; x++)
                {
                    var key = new CellKey(x, y, z);
                    if (!_cells.TryGetValue(key, out List<T>? bucket))
                    {
                        bucket = [];
                        _cells.Add(key, bucket);
                    }

                    bucket.Add(item);
                }
            }
        }
    }

    private void RemoveCells(T item, in Aabb3D bounds)
    {
        CellRange range = GetCellRange(in bounds);
        for (var z = range.MinZ; z <= range.MaxZ; z++)
        {
            for (var y = range.MinY; y <= range.MaxY; y++)
            {
                for (var x = range.MinX; x <= range.MaxX; x++)
                {
                    var key = new CellKey(x, y, z);
                    if (!_cells.TryGetValue(key, out List<T>? bucket))
                    {
                        continue;
                    }

                    bucket.Remove(item);
                    if (bucket.Count == 0)
                    {
                        _cells.Remove(key);
                    }
                }
            }
        }
    }

    private CellRange GetCellRange(in Aabb3D bounds)
    {
        var minX = (int)MathF.Floor(bounds.Min.X * _invCellSize);
        var minY = (int)MathF.Floor(bounds.Min.Y * _invCellSize);
        var minZ = (int)MathF.Floor(bounds.Min.Z * _invCellSize);
        var maxX = (int)MathF.Floor(bounds.Max.X * _invCellSize);
        var maxY = (int)MathF.Floor(bounds.Max.Y * _invCellSize);
        var maxZ = (int)MathF.Floor(bounds.Max.Z * _invCellSize);
        return new CellRange(minX, minY, minZ, maxX, maxY, maxZ);
    }

    private readonly record struct CellKey(int X, int Y, int Z);

    private readonly record struct CellRange(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ);
}
