namespace Axrone.Collections;

using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

/// <summary>
/// Strongly-typed, self-validating buffer capacity restricted strictly to powers of two.
/// </summary>
public readonly record struct BufferCapacity : IEquatable<BufferCapacity>, IComparable<BufferCapacity>
{
    public const uint MinCapacity = 2;
    public const uint MaxCapacity = 1u << 30;

    public uint Value { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BufferCapacity(uint capacity)
    {
        if (capacity < MinCapacity || capacity > MaxCapacity || !BitOperations.IsPow2(capacity))
        {
            ThrowHelper.ThrowInvalidBufferCapacity(capacity);
        }
        Value = capacity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BufferCapacity Create(uint capacity) => new(capacity);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(BufferCapacity capacity) => capacity.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(BufferCapacity capacity) => (int)capacity.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ToUInt32() => Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToInt32() => (int)Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BufferCapacity FromUInt32(uint value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(BufferCapacity other) => Value.CompareTo(other.Value);

    public static bool operator <(BufferCapacity left, BufferCapacity right) => left.Value < right.Value;
    public static bool operator <=(BufferCapacity left, BufferCapacity right) => left.Value <= right.Value;
    public static bool operator >(BufferCapacity left, BufferCapacity right) => left.Value > right.Value;
    public static bool operator >=(BufferCapacity left, BufferCapacity right) => left.Value >= right.Value;

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Monotonic sequence position tracker providing register-passable domain arithmetic.
/// </summary>
public readonly record struct SequenceNumber(nuint Value) : IComparable<SequenceNumber>
{
    public static SequenceNumber Zero => new(0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SequenceNumber Next() => new(Value + 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SequenceNumber Advance(uint count) => new(Value + count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public nint Difference(SequenceNumber other) => (nint)(Value - other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SequenceNumber other) => Value.CompareTo(other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator nuint(SequenceNumber sequence) => sequence.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator SequenceNumber(nuint value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public nuint ToUIntPtr() => Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SequenceNumber FromUIntPtr(nuint value) => new(value);

    public static bool operator <(SequenceNumber left, SequenceNumber right) => left.Value < right.Value;
    public static bool operator <=(SequenceNumber left, SequenceNumber right) => left.Value <= right.Value;
    public static bool operator >(SequenceNumber left, SequenceNumber right) => left.Value > right.Value;
    public static bool operator >=(SequenceNumber left, SequenceNumber right) => left.Value >= right.Value;

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
