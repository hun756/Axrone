namespace Axrone.Utility.Alignment;

[StructLayout(LayoutKind.Sequential, Pack = sizeof(uint))]
public readonly record struct Alignment : IComparable<Alignment>, IEquatable<Alignment>, ISpanFormattable
{
    private readonly uint _value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Alignment(uint value, bool _) => _value = value;

    public uint Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
    }

    public nuint Mask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (nuint)_value - 1;
    }

    public int Shift
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BitOperations.TrailingZeroCount(_value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Alignment(uint value)
    {
        if (!BitOperations.IsPow2(value))
        {
            ThrowHelper.ThrowInvalidAlignment(value);
        }
        _value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCreate(uint value, out Alignment alignment)
    {
        if (BitOperations.IsPow2(value))
        {
            alignment = new Alignment(value, true);
            return true;
        }
        alignment = default;
        return false;
    }

    public static Alignment Byte => new(1, true);
    public static Alignment Word => new(2, true);
    public static Alignment DWord => new(4, true);
    public static Alignment QWord => new(8, true);
    public static Alignment Vector128 => new(16, true);
    public static Alignment Vector256 => new(32, true);
    public static Alignment Vector512 => new(64, true);
    public static Alignment CacheLine64 => new(64, true);
    public static Alignment CacheLine128 => new(128, true);
    public static Alignment Page4K => new(4096, true);

    public const int CacheLine64Bytes = 64;
    public const int CacheLine128Bytes = 128;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint AlignUp(nuint address)
    {
        nuint mask = (nuint)_value - 1;
        return (address + mask) & ~mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint AlignDown(nuint address)
    {
        nuint mask = (nuint)_value - 1;
        return address & ~mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsAligned(nuint address) => (address & ((nuint)_value - 1)) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe void* AlignUp(void* p) => (void*)AlignUp((nuint)p);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe void* AlignDown(void* p) => (void*)AlignDown((nuint)p);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe bool IsAligned(void* p) => IsAligned((nuint)p);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ulong AlignUp(ulong address)
    {
        ulong mask = (ulong)_value - 1;
        return (address + mask) & ~mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ulong AlignDown(ulong address)
    {
        ulong mask = (ulong)_value - 1;
        return address & ~mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsAligned(ulong address) => (address & ((ulong)_value - 1)) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public long AlignUp(long address)
    {
        long mask = (long)_value - 1;
        return (address + mask) & ~mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public long AlignDown(long address)
    {
        long mask = (long)_value - 1;
        return address & ~mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsAligned(long address) => (address & ((long)_value - 1)) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nint AlignUp(nint address)
    {
        nint mask = (nint)_value - 1;
        return (address + mask) & ~mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nint AlignDown(nint address)
    {
        nint mask = (nint)_value - 1;
        return address & ~mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsAligned(nint address) => (address & ((nint)_value - 1)) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Alignment other) => _value.CompareTo(other._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => _value.ToString(CultureInfo.InvariantCulture);

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) =>
        _value.TryFormat(destination, out charsWritten, format, provider);

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) =>
        _value.ToString(format, formatProvider);
}
