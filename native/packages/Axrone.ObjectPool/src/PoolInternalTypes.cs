namespace Axrone.ObjectPool;

/// <summary>
/// Represents a single pooled item with its associated metadata.
/// Shared between <see cref="ObjectPool{T}"/> and internal storage implementations.
/// </summary>
/// <typeparam name="T">The reference type of the pooled object.</typeparam>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct PooledItem<T> where T : class
{
    /// <summary>The pooled object instance.</summary>
    public readonly T Instance;

    /// <summary>UTC timestamp when this item was created or last recycled.</summary>
    public readonly DateTime Created;

    /// <summary>Generation counter incremented on each pool clear/scavenge cycle.</summary>
    public readonly int Generation;

    /// <summary>Number of times this item has been rented.</summary>
    public readonly long RentCount;

    /// <summary>Unique identifier for this pooled instance.</summary>
    public readonly long Id;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PooledItem(T instance, DateTime created, int generation, long rentCount, long id)
    {
        Instance = instance;
        Created = created;
        Generation = generation;
        RentCount = rentCount;
        Id = id;
    }
}
