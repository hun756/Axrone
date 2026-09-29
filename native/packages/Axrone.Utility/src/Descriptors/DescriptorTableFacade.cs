namespace Axrone.Utility.Descriptors;

using Axrone.Utility.Backoff.SpinPolicies;

/// <summary>Descriptor table with standard adaptive backoff and null metrics.</summary>
public sealed class DescriptorTable<TDescriptor> : IDisposable, IAsyncDisposable
    where TDescriptor : unmanaged
{
    private readonly DescriptorTable<TDescriptor, AdaptiveSpinBackoff, NullMetricsSink> _core;

    /// <summary>Slot capacity.</summary>
    public uint Capacity => _core.Capacity;

    /// <summary>Live descriptors.</summary>
    public long ActiveCount => _core.ActiveCount;

    /// <summary>Creates a table.</summary>
    public DescriptorTable(DescriptorTableOptions? options = null)
    {
        _core = new DescriptorTable<TDescriptor, AdaptiveSpinBackoff, NullMetricsSink>(options);
    }

    /// <summary>Allocates a slot and copies the descriptor in.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAllocate(in TDescriptor descriptor, out DescriptorHandle<TDescriptor> handle, ushort flags = 0) =>
        _core.TryAllocate(in descriptor, out handle, flags);

    /// <summary>Allocates an uninitialized slot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAllocate(out DescriptorHandle<TDescriptor> handle, ushort flags = 0) =>
        _core.TryAllocate(out handle, flags);

    /// <summary>Reclaims a handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFree(in DescriptorHandle<TDescriptor> handle) =>
        _core.TryFree(in handle);

    /// <summary>Reclaims a batch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FreeBatch(params ReadOnlySpan<DescriptorHandle<TDescriptor>> handles) =>
        _core.FreeBatch(handles);

    /// <summary>Copies the payload out.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(in DescriptorHandle<TDescriptor> handle, out TDescriptor descriptor) =>
        _core.TryGet(in handle, out descriptor);

    /// <summary>Zero-copy readonly reference.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly TDescriptor GetRef(in DescriptorHandle<TDescriptor> handle) =>
        ref _core.GetRef(in handle);

    /// <summary>Overwrites the payload in place.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryUpdate(in DescriptorHandle<TDescriptor> handle, in TDescriptor updated) =>
        _core.TryUpdate(in handle, updated);

    /// <summary>Runs a static mutator in place.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryMutate<TMutator, TContext>(in DescriptorHandle<TDescriptor> handle, scoped ref TContext context)
        where TMutator : struct, IDescriptorMutator<TDescriptor, TContext>
        where TContext : allows ref struct =>
        _core.TryMutate<TMutator, TContext>(in handle, ref context);

    /// <summary>Runs a static zero-copy reader.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAccess<TAccessor, TContext>(in DescriptorHandle<TDescriptor> handle, scoped ref TContext context)
        where TAccessor : struct, IDescriptorAccessor<TDescriptor, TContext>
        where TContext : allows ref struct =>
        _core.TryAccess<TAccessor, TContext>(in handle, ref context);

    /// <summary>Atomically changes status.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySetStatus(in DescriptorHandle<TDescriptor> handle, DescriptorStatus newStatus) =>
        _core.TrySetStatus(in handle, newStatus);

    /// <summary>Reads status.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DescriptorStatus GetStatus(in DescriptorHandle<TDescriptor> handle) =>
        _core.GetStatus(in handle);

    /// <summary>Transitions to draining or faulted.</summary>
    public void Complete(Exception? error = null) => _core.Complete(error);

    /// <summary>Drains live descriptors.</summary>
    public ValueTask DrainAsync(CancellationToken cancellationToken = default) => _core.DrainAsync(cancellationToken);

    /// <inheritdoc/>
    public void Dispose() => _core.Dispose();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _core.DisposeAsync();
}
