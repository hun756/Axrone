namespace Axrone.Utility.Descriptors;

using Axrone.Utility.Backoff;

/// <summary>
/// Stack-bound read guard over a live slot. The lease pins the payload:
/// freeing while borrowed retires the slot instead of reclaiming it, and
/// the final <see cref="Dispose"/> completes the release. Single-dispose
/// by design; double disposal throws.
/// </summary>
public readonly ref struct DescriptorLease<TDescriptor, TBackoff, TMetrics>
    where TDescriptor : unmanaged
    where TBackoff : struct, ISpinBackoff
    where TMetrics : struct, IDescriptorMetricsSink
{
    private readonly DescriptorTable<TDescriptor, TBackoff, TMetrics>? _table;
    private readonly SlotIndex _index;
    private readonly Generation _generation;
    private readonly bool _active;

    /// <summary>Whether the lease guards a live slot.</summary>
    public bool IsActive
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _active && _table is not null;
    }

    /// <summary>Borrowed handle.</summary>
    public DescriptorHandle<TDescriptor> Handle
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new((uint)_index, (uint)_generation);
    }

    /// <summary>Zero-copy payload. Throws when the slot moved on.</summary>
    public ref readonly TDescriptor Payload
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (!_active || _table is null)
            {
                ThrowHelper.ThrowInvalidOperationException("The descriptor lease is inactive or has been released.");
            }

            return ref _table.GetLeasedRef(_index, _generation);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal DescriptorLease(
        DescriptorTable<TDescriptor, TBackoff, TMetrics> table,
        SlotIndex index,
        Generation generation)
    {
        _table = table;
        _index = index;
        _generation = generation;
        _active = true;
    }

    /// <summary>Releases the guard; completes a retired slot on last release.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (!_active || _table is null)
        {
            ThrowHelper.ThrowInvalidOperationException("The descriptor lease is inactive or has already been released.");
        }

        _table.ReleaseLease(_index, _generation);
    }
}
