namespace Axrone.Utility.Descriptors;

/// <summary>Descriptor lifecycle status.</summary>
public enum DescriptorStatus : ushort
{
    /// <summary>Slot is free.</summary>
    Free = 0,

    /// <summary>Slot is allocated.</summary>
    Allocated = 1,

    /// <summary>Slot is active.</summary>
    Active = 2,

    /// <summary>Slot is suspended.</summary>
    Suspended = 3,
}

/// <summary>
/// 8-byte generational handle: slot index plus monotonic generation eradicating
/// ABA races on slot reuse. Zero generation is the invalid sentinel.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct DescriptorHandle<TDescriptor>(uint SlotIndex, uint Generation) : IComparable<DescriptorHandle<TDescriptor>>
    where TDescriptor : unmanaged
{
    /// <summary>Invalid sentinel.</summary>
    public static DescriptorHandle<TDescriptor> Invalid => default;

    /// <summary>Whether the handle names a real allocation.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => Generation != 0;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(DescriptorHandle<TDescriptor> other)
    {
        int cmp = SlotIndex.CompareTo(other.SlotIndex);
        return cmp != 0 ? cmp : Generation.CompareTo(other.Generation);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => $"DescriptorHandle<{typeof(TDescriptor).Name}>(Slot={SlotIndex}, Gen={Generation})";
}

/// <summary>Descriptor table sizing. Capacity must be a nonzero power of two.</summary>
public sealed class DescriptorTableOptions
{
    /// <summary>Slot capacity.</summary>
    public uint Capacity
    {
        get => field;
        init
        {
            if (value == 0 || (value & (value - 1)) != 0)
            {
                ThrowHelper.ThrowInvalidCapacity((int)value);
            }

            field = value;
        }
    } = 1024;
}
