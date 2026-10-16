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
        _buffer = GC.AllocateUninitializedArray<T>(initialCapacity);
    }

    /// <summary>Views all rows.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan() => _buffer;

    /// <summary>References a row.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Get(int row)
    {
        ref T start = ref MemoryMarshal.GetArrayDataReference(_buffer);
        return ref Unsafe.Add(ref start, (uint)row);
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
        T[] grown = GC.AllocateUninitializedArray<T>(newCapacity);
        Array.Copy(_buffer, grown, Math.Min(_buffer.Length, newCapacity));
        _buffer = grown;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveRow(int fromRow, int toRow)
    {
        ref T start = ref MemoryMarshal.GetArrayDataReference(_buffer);
        Unsafe.Add(ref start, (uint)toRow) = Unsafe.Add(ref start, (uint)fromRow);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(int sourceRow, IComponentColumn destinationColumn, int destinationRow)
    {
        ComponentColumn<T> target = Unsafe.As<ComponentColumn<T>>(destinationColumn);
        target.Set(destinationRow, in Get(sourceRow));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _buffer = Array.Empty<T>();
    }
}
