namespace Axrone.Utility.Backoff;

public static class FastPcgRng
{
    public const ulong DefaultStreamConstant = 1442695040888963407UL;
    public const ulong DefaultMultiplier = 6364136223846793005UL;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ulong Next(ref ulong state)
    {
        ulong oldState = state;
        state = unchecked((oldState * DefaultMultiplier) + DefaultStreamConstant);
        ulong xorShifted = unchecked(((oldState >> 18) ^ oldState) >> 27);
        int rot = unchecked((int)(oldState >> 59));
        return unchecked((xorShifted >> rot) | (xorShifted << ((-rot) & 31)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double NextDouble(ref ulong state)
    {
        return (Next(ref state) & 0x1FFFFFFFFFFFFFUL) * (1.0 / (1UL << 53));
    }
}
