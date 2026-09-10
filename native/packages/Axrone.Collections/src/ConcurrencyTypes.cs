namespace Axrone.Collections;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct BatchCapacity
{
    public const int MaximumCapacity = 0x3FFFFFFF;
    public int Value { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public BatchCapacity(int value)
    {
        if (value <= 0 || value > MaximumCapacity)
        {
            ThrowHelper.ThrowInvalidCapacity(value);
        }
        Value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(BatchCapacity capacity) => capacity.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator nuint(BatchCapacity capacity) => (nuint)capacity.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToInt32() => Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public nuint ToUIntPtr() => (nuint)Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BatchCapacity FromInt32(int value) => new(value);
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct MemoryAlignment
{
    public nuint Value { get; }

    public static readonly MemoryAlignment CacheLine64 = new(64);
    public static readonly MemoryAlignment CacheLine128 = new(128);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public MemoryAlignment(nuint value)
    {
        nuint minAlignment;
        unsafe { minAlignment = (nuint)sizeof(nuint); }
        if (value < minAlignment || (value & (value - 1)) != 0)
        {
            ThrowHelper.ThrowInvalidAlignment(value);
        }
        Value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator nuint(MemoryAlignment alignment) => alignment.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public nuint ToUIntPtr() => Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemoryAlignment FromUIntPtr(nuint value) => new(value);
}
