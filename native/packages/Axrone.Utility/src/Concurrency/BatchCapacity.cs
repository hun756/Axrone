namespace Axrone.Utility;

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// Strongly-typed, self-validating capacity for batch-oriented buffers.
/// Accepts any value in [1, 0x3FFFFFFF]; alignment to power-of-two is the consumer's concern.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct BatchCapacity : IEquatable<BatchCapacity>
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BatchCapacity FromBatchCapacity(int value) => new(value);
}
