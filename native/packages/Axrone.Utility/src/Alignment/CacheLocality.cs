namespace Axrone.Utility.Alignment;

[StructLayout(LayoutKind.Explicit, Size = 64)]
public readonly struct CacheLine64Padded<T> : IEquatable<CacheLine64Padded<T>> where T : unmanaged
{
    [FieldOffset(0)]
    public readonly T Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CacheLine64Padded(T value)
    {
        if (Unsafe.SizeOf<T>() > 64)
        {
            ThrowHelper.ThrowTypeTooLargeForCacheLine(Unsafe.SizeOf<T>(), 64);
        }
        Value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(CacheLine64Padded<T> other) =>
        EqualityComparer<T>.Default.Equals(Value, other.Value);

    public override bool Equals(object? obj) =>
        obj is CacheLine64Padded<T> other && Equals(other);

    public override int GetHashCode() =>
        EqualityComparer<T>.Default.GetHashCode(Value);

    public static bool operator ==(CacheLine64Padded<T> left, CacheLine64Padded<T> right) =>
        left.Equals(right);

    public static bool operator !=(CacheLine64Padded<T> left, CacheLine64Padded<T> right) =>
        !left.Equals(right);
}

[StructLayout(LayoutKind.Explicit, Size = 128)]
public readonly struct CacheLine128Padded<T> : IEquatable<CacheLine128Padded<T>> where T : unmanaged
{
    [FieldOffset(0)]
    public readonly T Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CacheLine128Padded(T value)
    {
        if (Unsafe.SizeOf<T>() > 128)
        {
            ThrowHelper.ThrowTypeTooLargeForCacheLine(Unsafe.SizeOf<T>(), 128);
        }
        Value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(CacheLine128Padded<T> other) =>
        EqualityComparer<T>.Default.Equals(Value, other.Value);

    public override bool Equals(object? obj) =>
        obj is CacheLine128Padded<T> other && Equals(other);

    public override int GetHashCode() =>
        EqualityComparer<T>.Default.GetHashCode(Value);

    public static bool operator ==(CacheLine128Padded<T> left, CacheLine128Padded<T> right) =>
        left.Equals(right);

    public static bool operator !=(CacheLine128Padded<T> left, CacheLine128Padded<T> right) =>
        !left.Equals(right);
}

[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct AlignedAtomicCounter64
{
    [FieldOffset(0)]
    private long _value;

    public long Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Increment() => Interlocked.Increment(ref _value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Decrement() => Interlocked.Decrement(ref _value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Add(long delta) => Interlocked.Add(ref _value, delta);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CompareExchange(long newValue, long expected) =>
        Interlocked.CompareExchange(ref _value, newValue, expected) == expected;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => Volatile.Write(ref _value, 0);
}

[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct AlignedAtomicCounter128
{
    [FieldOffset(64)]
    private long _value;

    public long Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Increment() => Interlocked.Increment(ref _value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Decrement() => Interlocked.Decrement(ref _value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Add(long delta) => Interlocked.Add(ref _value, delta);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CompareExchange(long newValue, long expected) =>
        Interlocked.CompareExchange(ref _value, newValue, expected) == expected;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => Volatile.Write(ref _value, 0);
}
