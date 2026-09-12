namespace Axrone.Utility.Alignment;

public readonly struct ByteAlignmentPolicy : IAlignmentPolicy<ByteAlignmentPolicy>
{
    public static Alignment TargetAlignment => Alignment.Byte;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignUp(nuint address) => address;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignDown(nuint address) => address;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsAligned(nuint address) => true;
}

public readonly struct WordAlignmentPolicy : IAlignmentPolicy<WordAlignmentPolicy>
{
    public static Alignment TargetAlignment => Alignment.Word;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignUp(nuint address) => (address + 1) & ~((nuint)1);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignDown(nuint address) => address & ~((nuint)1);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsAligned(nuint address) => (address & 1) == 0;
}

public readonly struct DWordAlignmentPolicy : IAlignmentPolicy<DWordAlignmentPolicy>
{
    public static Alignment TargetAlignment => Alignment.DWord;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignUp(nuint address) => (address + 3) & ~((nuint)3);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignDown(nuint address) => address & ~((nuint)3);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsAligned(nuint address) => (address & 3) == 0;
}

public readonly struct QWordAlignmentPolicy : IAlignmentPolicy<QWordAlignmentPolicy>
{
    public static Alignment TargetAlignment => Alignment.QWord;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignUp(nuint address) => (address + 7) & ~((nuint)7);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignDown(nuint address) => address & ~((nuint)7);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsAligned(nuint address) => (address & 7) == 0;
}

public readonly struct Vector128AlignmentPolicy : IAlignmentPolicy<Vector128AlignmentPolicy>
{
    public static Alignment TargetAlignment => Alignment.Vector128;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignUp(nuint address) => (address + 15) & ~((nuint)15);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignDown(nuint address) => address & ~((nuint)15);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsAligned(nuint address) => (address & 15) == 0;
}

public readonly struct Vector256AlignmentPolicy : IAlignmentPolicy<Vector256AlignmentPolicy>
{
    public static Alignment TargetAlignment => Alignment.Vector256;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignUp(nuint address) => (address + 31) & ~((nuint)31);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignDown(nuint address) => address & ~((nuint)31);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsAligned(nuint address) => (address & 31) == 0;
}

public readonly struct CacheLine64AlignmentPolicy : IAlignmentPolicy<CacheLine64AlignmentPolicy>
{
    public static Alignment TargetAlignment => Alignment.CacheLine64;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignUp(nuint address) => (address + 63) & ~((nuint)63);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignDown(nuint address) => address & ~((nuint)63);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsAligned(nuint address) => (address & 63) == 0;
}

public readonly struct CacheLine128AlignmentPolicy : IAlignmentPolicy<CacheLine128AlignmentPolicy>
{
    public static Alignment TargetAlignment => Alignment.CacheLine128;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignUp(nuint address) => (address + 127) & ~((nuint)127);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignDown(nuint address) => address & ~((nuint)127);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsAligned(nuint address) => (address & 127) == 0;
}

public readonly struct Page4KAlignmentPolicy : IAlignmentPolicy<Page4KAlignmentPolicy>
{
    public static Alignment TargetAlignment => Alignment.Page4K;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignUp(nuint address) => (address + 4095) & ~((nuint)4095);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static nuint AlignDown(nuint address) => address & ~((nuint)4095);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsAligned(nuint address) => (address & 4095) == 0;
}
