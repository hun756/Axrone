namespace Axrone.Utility.Descriptors;

/// <summary>Acquisition outcome.</summary>
public enum AcquisitionStatus : byte
{
    /// <summary>Slot acquired.</summary>
    Success = 0,

    /// <summary>Table is full.</summary>
    Exhausted = 1,

    /// <summary>Table is draining or terminated.</summary>
    Terminated = 2,
}

/// <summary>
/// Discriminated acquisition result: the handle on success, the reason
/// otherwise. Implicitly converts to <see cref="bool"/> for terse call sites.
/// </summary>
public readonly record struct AcquisitionResult<TDescriptor>(DescriptorHandle<TDescriptor> Handle, AcquisitionStatus Status)
    where TDescriptor : unmanaged
{
    /// <summary>Whether acquisition succeeded.</summary>
    public bool IsSuccess
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Status == AcquisitionStatus.Success;
    }

    /// <summary>Success result.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AcquisitionResult<TDescriptor> Ok(DescriptorHandle<TDescriptor> handle) => new(handle, AcquisitionStatus.Success);

    /// <summary>Table-full result.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AcquisitionResult<TDescriptor> Exhausted() => new(DescriptorHandle<TDescriptor>.Invalid, AcquisitionStatus.Exhausted);

    /// <summary>Draining/terminated result.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AcquisitionResult<TDescriptor> Terminated() => new(DescriptorHandle<TDescriptor>.Invalid, AcquisitionStatus.Terminated);

    /// <summary>Terse success check.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator bool(AcquisitionResult<TDescriptor> result) => result.IsSuccess;

    /// <inheritdoc/>
    public override string ToString() => IsSuccess
        ? string.Create(CultureInfo.InvariantCulture, $"Acquired({Handle})")
        : string.Create(CultureInfo.InvariantCulture, $"Rejected({Status})");
}
