namespace Axrone.Random;

/// <summary>
/// SplitMix64, the seeding engine of the suite (Steele, Lea and Flood, 2014). One 64-bit word
/// of state advanced by the golden-ratio increment and folded through two xor-multiply rounds:
/// six ALU operations, no multiply of two independent values, and a period that covers any
/// seeding need. It is the weakest stream here statistically and is meant as a seeder for the
/// xoshiro family, not as a long-lived generator.
/// </summary>
/// <remarks>
/// The reference vector for seed <c>0</c> starts at <c>0xE220A8397B1DCDAF</c>. Every engine in
/// this package expands its 64-bit seed through this generator, which is what makes seeding
/// reproducible across engines: one algorithm, one input, one answer.
/// </remarks>
public struct SplitMix64 : IRandomSource<SplitMix64>, IEquatable<SplitMix64>
{
    /// <summary>State increment: the 64-bit fractional part of the golden ratio (2^64 / phi).</summary>
    public const ulong Gamma = 0x9E3779B97F4A7C15UL;

    /// <summary>First mixing multiplier of the reference finalizer.</summary>
    public const ulong Multiplier1 = 0xBF58476D1CE4E5B9UL;

    /// <summary>Second mixing multiplier of the reference finalizer.</summary>
    public const ulong Multiplier2 = 0x94D049BB133111EBUL;

    private ulong _state;

    /// <summary>Creates a state whose first draw is a pure function of <paramref name="seed"/>.</summary>
    /// <param name="seed">The seed word; the state is the seed itself.</param>
    public SplitMix64(ulong seed) => _state = seed;

    /// <inheritdoc/>
    public static SplitMix64 Create(ulong seed) => new SplitMix64(seed);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ulong NextUInt64(ref SplitMix64 source)
    {
        source._state += Gamma;
        ulong z = source._state;
        z = (z ^ (z >> 30)) * Multiplier1;
        z = (z ^ (z >> 27)) * Multiplier2;
        return z ^ (z >> 31);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Takes the high half of the 64-bit draw. The low half of a finalizer output is the
    /// weakest part of the word, so the high half is the better 32-bit source.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static uint NextUInt32(ref SplitMix64 source) => (uint)(NextUInt64(ref source) >> 32);

    /// <summary>Compares the state words of two generators.</summary>
    /// <param name="other">The generator to compare against.</param>
    /// <returns><see langword="true"/> when both sit at the same position in the same stream.</returns>
    public bool Equals(SplitMix64 other) => _state == other._state;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is SplitMix64 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _state.GetHashCode();

    /// <summary>Equality operator.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both sit at the same position in the same stream.</returns>
    public static bool operator ==(SplitMix64 left, SplitMix64 right) => left._state == right._state;

    /// <summary>Inequality operator.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when the two are at different stream positions.</returns>
    public static bool operator !=(SplitMix64 left, SplitMix64 right) => left._state != right._state;
}
