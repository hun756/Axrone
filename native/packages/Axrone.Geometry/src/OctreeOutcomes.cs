namespace Axrone.Geometry;

/// <summary>
/// Generational identity of an octree item: the item-pool slot plus the
/// allocation epoch observed at insert. A recycled slot never answers to a
/// handle issued for its previous occupant. Named for its engine because the
/// packed <see cref="SpatialHandle"/> names node-pool slots, not item slots.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct OctreeHandle(SlotIndex Slot, Generation Generation)
{
    /// <summary>Invalid sentinel; never a live item.</summary>
    public static OctreeHandle Invalid => new(SlotIndex.Invalid, Generation.Zero);

    /// <summary>Whether this handle names a possibly-live item.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Slot.IsValid;
    }
}

/// <summary>One stored item: its handle, its tight bounds and its payload.</summary>
/// <typeparam name="T">Payload type carried alongside the handle.</typeparam>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SpatialItem<T>(OctreeHandle Handle, Aabb3D Bounds, T Value) where T : unmanaged;

/// <summary>Outcome of a handled insert.</summary>
public enum InsertOutcome : byte
{
    /// <summary>The item was stored and a handle was issued.</summary>
    Success = 0,

    /// <summary>The bounds fall outside the root domain.</summary>
    OutOfBounds = 1,

    /// <summary>The bounds are not a valid domain (NaN or inverted).</summary>
    InvalidDomain = 2,

    /// <summary>The tree is not active.</summary>
    Disposed = 3,
}

/// <summary>Verdict of <c>TryInsert</c>: what happened plus the issued handle.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct InsertResult
{
    /// <summary>What the insert did.</summary>
    public readonly InsertOutcome Outcome;

    /// <summary>Issued handle on success; invalid otherwise.</summary>
    public readonly OctreeHandle Handle;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal InsertResult(InsertOutcome outcome, OctreeHandle handle)
    {
        Outcome = outcome;
        Handle = handle;
    }

    /// <summary>Whether the item was stored.</summary>
    public bool IsSuccess
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Outcome == InsertOutcome.Success;
    }

    /// <summary>Builds a success verdict.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InsertResult Success(OctreeHandle handle) => new(InsertOutcome.Success, handle);

    /// <summary>Builds an out-of-bounds verdict.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InsertResult OutOfBounds() => new(InsertOutcome.OutOfBounds, OctreeHandle.Invalid);

    /// <summary>Builds an invalid-domain verdict.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InsertResult InvalidDomain() => new(InsertOutcome.InvalidDomain, OctreeHandle.Invalid);

    /// <summary>Builds a disposed verdict.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InsertResult Disposed() => new(InsertOutcome.Disposed, OctreeHandle.Invalid);
}

/// <summary>Outcome of a nearest-neighbour query.</summary>
public enum NearestOutcome : byte
{
    /// <summary>Nothing was found within the limit.</summary>
    NotFound = 0,

    /// <summary>A nearest item was found.</summary>
    Found = 1,
}

/// <summary>Nearest-neighbour answer: the closest item plus its squared distance.</summary>
/// <typeparam name="T">Payload type carried alongside the handle.</typeparam>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct NearestNeighborResult<T> where T : unmanaged
{
    private readonly NearestOutcome _outcome;
    private readonly SpatialItem<T> _item;
    private readonly float _distanceSquared;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal NearestNeighborResult(NearestOutcome outcome, SpatialItem<T> item, float distanceSquared)
    {
        _outcome = outcome;
        _item = item;
        _distanceSquared = distanceSquared;
    }

    /// <summary>Whether anything was found.</summary>
    public NearestOutcome Outcome
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _outcome;
    }

    /// <summary>Closest item; meaningless when nothing was found.</summary>
    public SpatialItem<T> Item
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _item;
    }

    /// <summary>Squared distance to the closest item; infinity when nothing was found.</summary>
    public float DistanceSquared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _distanceSquared;
    }

    /// <summary>Whether a nearest item was found.</summary>
    public bool HasFound
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _outcome == NearestOutcome.Found;
    }

    /// <summary>Builds a found answer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NearestNeighborResult<T> Found(SpatialItem<T> item, float distanceSquared) =>
        new(NearestOutcome.Found, item, distanceSquared);

    /// <summary>Builds a not-found answer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NearestNeighborResult<T> NotFound() =>
        new(NearestOutcome.NotFound, default, float.PositiveInfinity);
}
