namespace Axrone.Ecs;

/// <summary>
/// Component type index allocator: first touch of a component type claims
/// the next slot, at most 256 registered types.
/// </summary>
public static class ComponentTypeRegistry
{
    private static int s_counter;

    /// <summary>Allocates the next component id.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort AllocateId()
    {
        int id = Interlocked.Increment(ref s_counter) - 1;
        if ((uint)id >= 256U)
        {
            EcsThrowHelper.ThrowMaxComponentsExceeded();
        }

        return (ushort)id;
    }
}

/// <summary>
/// Per-type component identity, claimed once per closed generic.
/// </summary>
/// <typeparam name="T">Unmanaged component type.</typeparam>
public static class ComponentType<T> where T : unmanaged
{
    /// <summary>Type index.</summary>
    public static readonly ComponentId Id = new(ComponentTypeRegistry.AllocateId());

    /// <summary>Single-bit mask.</summary>
    public static readonly ComponentMask Mask = ComponentMask.Empty.With(Id);
}

/// <summary>
/// Type-erased component column.
/// </summary>
public interface IComponentColumn : IDisposable
{
    /// <summary>Component type index.</summary>
    ComponentId Id { get; }

    /// <summary>Row capacity.</summary>
    int Capacity { get; }

    /// <summary>Grows preserving rows.</summary>
    void Resize(int newCapacity);

    /// <summary>Moves a row within the column.</summary>
    void MoveRow(int fromRow, int toRow);

    /// <summary>Copies one row into another column of the same type.</summary>
    void CopyTo(int sourceRow, IComponentColumn destinationColumn, int destinationRow);
}

/// <summary>
/// Column factory registry: component types opt in with a column factory
/// instead of the world hardcoding known types.
/// </summary>
public static class ComponentRegistry
{
    private static readonly Dictionary<ushort, Func<int, IComponentColumn>> _factories = new();
    private static readonly Lock _gate = new();

    /// <summary>Registers a column factory.</summary>
    public static void Register<T>() where T : unmanaged
    {
        lock (_gate)
        {
            ushort id = ComponentType<T>.Id.Value;
            _factories[id] = static capacity => new ComponentColumn<T>(capacity);
        }
    }

    /// <summary>Creates a column for a registered type.</summary>
    internal static IComponentColumn Create(ComponentId id, int capacity)
    {
        lock (_gate)
        {
            if (_factories.TryGetValue(id.Value, out Func<int, IComponentColumn>? factory))
            {
                return factory(capacity);
            }
        }

        EcsThrowHelper.ThrowUnknownComponentType(id);
        return null!;
    }
}

/// <summary>
/// Dense column over a managed array with ref row access.
/// </summary>
/// <typeparam name="T">Unmanaged component type.</typeparam>
public sealed class ComponentColumn<T> : IComponentColumn where T : unmanaged
{
    private T[] _buffer;
    private int[] _added;
    private int[] _changed;

    /// <inheritdoc/>
    public ComponentId Id
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ComponentType<T>.Id;
    }

    /// <inheritdoc/>
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _buffer.Length;
    }

    /// <summary>Creates a column.</summary>
    public ComponentColumn(int initialCapacity)
    {
        _buffer = GC.AllocateArray<T>(initialCapacity, pinned: true);
        _added = GC.AllocateArray<int>(initialCapacity, pinned: true);
        _changed = GC.AllocateArray<int>(initialCapacity, pinned: true);
    }

    /// <summary>Views all rows.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan() => _buffer;

    /// <summary>Views the added-tick of every capacity slot.</summary>
    public ReadOnlySpan<int> AddedTicks => _added;

    /// <summary>Views the changed-tick of every capacity slot.</summary>
    public ReadOnlySpan<int> ChangedTicks => _changed;

    /// <summary>References a row.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Get(int row)
    {
        ref T start = ref MemoryMarshal.GetArrayDataReference(_buffer);
        return ref Unsafe.Add(ref start, (uint)row);
    }

    /// <summary>Reads the tick at which a row was added.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetAddedTick(int row)
    {
        ref int start = ref MemoryMarshal.GetArrayDataReference(_added);
        return Unsafe.Add(ref start, (uint)row);
    }

    /// <summary>Reads the tick at which a row last changed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetChangedTick(int row)
    {
        ref int start = ref MemoryMarshal.GetArrayDataReference(_changed);
        return Unsafe.Add(ref start, (uint)row);
    }

    /// <summary>Marks a row as added and changed at <paramref name="tick"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkAdded(int row, int tick)
    {
        ref int added = ref MemoryMarshal.GetArrayDataReference(_added);
        ref int changed = ref MemoryMarshal.GetArrayDataReference(_changed);
        Unsafe.Add(ref added, (uint)row) = tick;
        Unsafe.Add(ref changed, (uint)row) = tick;
    }

    /// <summary>Marks a row as changed at <paramref name="tick"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkChanged(int row, int tick)
    {
        ref int changed = ref MemoryMarshal.GetArrayDataReference(_changed);
        Unsafe.Add(ref changed, (uint)row) = tick;
    }

    /// <summary>Writes a row.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int row, in T value)
    {
        ref T start = ref MemoryMarshal.GetArrayDataReference(_buffer);
        Unsafe.Add(ref start, (uint)row) = value;
    }

    /// <inheritdoc/>
    public void Resize(int newCapacity)
    {
        T[] grown = GC.AllocateArray<T>(newCapacity, pinned: true);
        int[] added = GC.AllocateArray<int>(newCapacity, pinned: true);
        int[] changed = GC.AllocateArray<int>(newCapacity, pinned: true);
        int copy = Math.Min(_buffer.Length, newCapacity);
        Array.Copy(_buffer, grown, copy);
        Array.Copy(_added, added, copy);
        Array.Copy(_changed, changed, copy);
        _buffer = grown;
        _added = added;
        _changed = changed;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveRow(int fromRow, int toRow)
    {
        ref T start = ref MemoryMarshal.GetArrayDataReference(_buffer);
        ref int added = ref MemoryMarshal.GetArrayDataReference(_added);
        ref int changed = ref MemoryMarshal.GetArrayDataReference(_changed);
        Unsafe.Add(ref start, (uint)toRow) = Unsafe.Add(ref start, (uint)fromRow);
        Unsafe.Add(ref added, (uint)toRow) = Unsafe.Add(ref added, (uint)fromRow);
        Unsafe.Add(ref changed, (uint)toRow) = Unsafe.Add(ref changed, (uint)fromRow);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(int sourceRow, IComponentColumn destinationColumn, int destinationRow)
    {
        ComponentColumn<T> target = Unsafe.As<ComponentColumn<T>>(destinationColumn);
        target.Set(destinationRow, in Get(sourceRow));
        target.SetTicks(destinationRow, GetAddedTick(sourceRow), GetChangedTick(sourceRow));
    }

    private void SetTicks(int row, int addedTick, int changedTick)
    {
        ref int added = ref MemoryMarshal.GetArrayDataReference(_added);
        ref int changed = ref MemoryMarshal.GetArrayDataReference(_changed);
        Unsafe.Add(ref added, (uint)row) = addedTick;
        Unsafe.Add(ref changed, (uint)row) = changedTick;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _buffer = Array.Empty<T>();
        _added = Array.Empty<int>();
        _changed = Array.Empty<int>();
    }
}
