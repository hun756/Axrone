namespace Axrone.Ecs;

using System.Buffers;

/// <summary>
/// Explicit AOT-safe component codec. Stable ids, not runtime component slots, are
/// written into snapshots, so a snapshot survives process restarts and registration
/// order changes.
/// </summary>
/// <typeparam name="T">Unmanaged component type.</typeparam>
public interface IComponentCodec<T> where T : unmanaged
{
    /// <summary>Stable wire id; zero is reserved.</summary>
    static abstract ushort StableId { get; }

    /// <summary>Writes one component value.</summary>
    static abstract void Write(IBufferWriter<byte> writer, in T value);

    /// <summary>Reads one component value and reports the consumed byte count.</summary>
    static abstract bool TryRead(ReadOnlySpan<byte> source, out T value, out int consumed);
}

/// <summary>Compile-time stable id policy used by <see cref="BlittableCodec{T, TStableId}"/>.</summary>
public interface IComponentStableId
{
    /// <summary>Stable wire id; zero is reserved.</summary>
    static abstract ushort Value { get; }
}

/// <summary>
/// Raw little-endian codec for unmanaged components. Blittable layouts are the default
/// engine storage shape, so most components need only a stable-id policy.
/// </summary>
/// <typeparam name="T">Unmanaged component type.</typeparam>
/// <typeparam name="TStableId">Stable id policy.</typeparam>
public readonly struct BlittableCodec<T, TStableId> : IComponentCodec<T>
    where T : unmanaged
    where TStableId : struct, IComponentStableId
{
    /// <inheritdoc/>
    public static ushort StableId => TStableId.Value;

    /// <inheritdoc/>
    public static void Write(IBufferWriter<byte> writer, in T value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        int size = Unsafe.SizeOf<T>();
        Span<byte> span = writer.GetSpan(size);
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(span), value);
        writer.Advance(size);
    }

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> source, out T value, out int consumed)
    {
        int size = Unsafe.SizeOf<T>();
        if (source.Length < size)
        {
            value = default;
            consumed = 0;
            return false;
        }

        value = MemoryMarshal.Read<T>(source);
        consumed = size;
        return true;
    }
}

/// <summary>
/// Explicit codec registry. Registering a codec also registers the component column
/// factory, so a restored world can materialize the component without reflection.
/// </summary>
public static class ComponentCodecRegistry
{
    private static readonly Dictionary<ushort, IComponentCodecBinding> s_byStableId = new();
    private static readonly Dictionary<ushort, IComponentCodecBinding> s_byComponentId = new();
    private static readonly Lock s_gate = new();

    /// <summary>Registers a codec and its component column factory.</summary>
    public static void Register<T, TCodec>()
        where T : unmanaged
        where TCodec : struct, IComponentCodec<T>
    {
        ushort stableId = TCodec.StableId;
        if (stableId == 0)
        {
            EcsThrowHelper.ThrowSnapshotInvalid("Snapshot stable component ids must be non-zero.");
        }

        ComponentRegistry.Register<T>();
        ComponentId componentId = ComponentType<T>.Id;
        lock (s_gate)
        {
            if (s_byComponentId.TryGetValue(componentId.Value, out IComponentCodecBinding? existing))
            {
                if (existing.StableId != stableId)
                {
                    EcsThrowHelper.ThrowSnapshotDuplicateStableId(stableId);
                }

                return;
            }

            if (s_byStableId.ContainsKey(stableId))
            {
                EcsThrowHelper.ThrowSnapshotDuplicateStableId(stableId);
            }

            var binding = new ComponentCodecBinding<T, TCodec>();
            s_byComponentId.Add(componentId.Value, binding);
            s_byStableId.Add(stableId, binding);
        }
    }

    internal static bool TryGet(ComponentId id, out IComponentCodecBinding? binding)
    {
        lock (s_gate)
        {
            return s_byComponentId.TryGetValue(id.Value, out binding);
        }
    }

    internal static bool TryGet(ushort stableId, out IComponentCodecBinding? binding)
    {
        lock (s_gate)
        {
            return s_byStableId.TryGetValue(stableId, out binding);
        }
    }

    internal static void Clear()
    {
        lock (s_gate)
        {
            s_byStableId.Clear();
            s_byComponentId.Clear();
        }
    }
}

/// <summary>Type-erased snapshot operations for one component codec.</summary>
internal interface IComponentCodecBinding
{
    /// <summary>Stable wire id.</summary>
    ushort StableId { get; }

    /// <summary>Writes one row of a type-erased column.</summary>
    void Write(IBufferWriter<byte> writer, IComponentColumn column, int row);

    /// <summary>Validates one payload without materializing it.</summary>
    bool Validate(ReadOnlySpan<byte> payload);

    /// <summary>Reads the payload and adds the component to an entity.</summary>
    void Apply(World world, in Entity entity, ReadOnlySpan<byte> payload);
}

internal sealed class ComponentCodecBinding<T, TCodec> : IComponentCodecBinding
    where T : unmanaged
    where TCodec : struct, IComponentCodec<T>
{
    public ushort StableId => TCodec.StableId;

    public void Write(IBufferWriter<byte> writer, IComponentColumn column, int row)
    {
        var typed = (ComponentColumn<T>)column;
        ref T value = ref typed.Get(row);
        TCodec.Write(writer, in value);
    }

    public bool Validate(ReadOnlySpan<byte> payload) =>
        TCodec.TryRead(payload, out _, out int consumed) && consumed == payload.Length;

    public void Apply(World world, in Entity entity, ReadOnlySpan<byte> payload)
    {
        if (!TCodec.TryRead(payload, out T value, out int consumed) || consumed != payload.Length)
        {
            EcsThrowHelper.ThrowSnapshotInvalidPayload();
        }

        world.AddComponent(entity, in value);
    }
}
