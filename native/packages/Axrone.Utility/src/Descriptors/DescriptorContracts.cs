namespace Axrone.Utility.Descriptors;

/// <summary>Zero-copy stack accessor over table memory.</summary>
public interface IDescriptorAccessor<TDescriptor, TContext>
    where TDescriptor : unmanaged
    where TContext : allows ref struct
{
    /// <summary>Inspects a descriptor without copying.</summary>
    static abstract void Access(ref readonly TDescriptor descriptor, scoped ref TContext context);
}

/// <summary>In-place mutator over table memory.</summary>
public interface IDescriptorMutator<TDescriptor, TContext>
    where TDescriptor : unmanaged
    where TContext : allows ref struct
{
    /// <summary>Mutates a descriptor in place.</summary>
    static abstract void Mutate(ref TDescriptor descriptor, scoped ref TContext context);
}

/// <summary>Diagnostic sink for descriptor lifecycle events.</summary>
public interface IDescriptorMetricsSink
{
    /// <summary>Slot allocated.</summary>
    void OnAllocated(uint slotIndex);

    /// <summary>Slot freed.</summary>
    void OnFreed(uint slotIndex);

    /// <summary>Slot status changed.</summary>
    void OnStatusChanged(uint slotIndex, DescriptorStatus newStatus);

    /// <summary>Table faulted.</summary>
    void OnFaulted(Exception error);
}

/// <summary>No-op sink eliminated by dead-code removal.</summary>
public readonly struct NullMetricsSink : IDescriptorMetricsSink
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnAllocated(uint slotIndex)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnFreed(uint slotIndex)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnStatusChanged(uint slotIndex, DescriptorStatus newStatus)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnFaulted(Exception error)
    {
    }
}
