namespace Axrone.Random;

/// <summary>
/// WyRand, the 64-bit seeder of the wyhash family (Wang, 2021). One word of state, one
/// add, two xor-shift-multiply rounds and one final xor-shift: four dependent multiplies of a
/// single word, which is the cheapest nonlinear mixer available and still passes BigCrush. It
/// is the throughput choice for short streams, where the seeder and the generator are the same
/// code path.
/// </summary>
/// <remarks>
/// <para><b>Zero state.</b> A zero state is the generator's fixed point - the stream would be
/// zeros forever - so it is treated as <em>unseeded</em>: the first draw reseeds from
/// operating-system entropy. A deliberately zero seed therefore asks for a random stream
/// rather than an all-zero one, which is the only behaviour that cannot be a silent data
/// defect. Every other seed is fully deterministic.</para>
/// <para>The reference vector for seed <c>0</c> is not published for this generator; the
/// determinism contract, not a magic constant, is what pins the stream.</para>
/// <para><b>AOT cost.</b> WyRand's mixing is multiply-bound, and under ILC a 64x64-to-128
/// widening multiply lowers to a schoolbook helper rather than a single hardware instruction,
/// so the core loses its throughput edge in a Native AOT image. Prefer the xoroshiro family
/// there, whose state updates are rotate/xor/add only.</para>
/// </remarks>
public struct WyRand : IRandomSource<WyRand>, IRandomStateSnapshot<WyRand>, IEquatable<WyRand>
{
    /// <summary>State increment and mixing seed: the fractional part of sqrt(3) - 1.</summary>
    public const ulong Secret0 = 0x2D358DCCAA6C78A5UL;

    /// <summary>Mixing multiplier, applied twice.</summary>
    public const ulong Secret1 = 0x8BB84B93962EACC9UL;

    /// <summary>Number of 64-bit words in a state snapshot: the single state word.</summary>
    public const int WordCount = 1;

    private ulong _state;

    /// <inheritdoc/>
    /// <remarks>Implemented explicitly so the constant stays usable in a <c>stackalloc</c>.</remarks>
    static int IRandomStateSnapshot<WyRand>.WordCount => WordCount;

    /// <summary>Creates a state whose first draw is a pure function of <paramref name="seed"/>.</summary>
    /// <param name="seed">The seed word. Zero is legal and means "unseeded"; see the remarks.</param>
    public WyRand(ulong seed) => _state = seed;

    /// <inheritdoc/>
    public static WyRand Create(ulong seed) => new WyRand(seed);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ulong NextUInt64(ref WyRand source)
    {
        if (source._state == 0UL)
        {
            source.ReseedFromEntropy();
        }

        source._state += Secret0;
        ulong t = source._state;
        t = (t ^ (t >> 48)) * Secret1;
        t = (t ^ (t >> 48)) * Secret1;
        return t ^ (t >> 32);
    }

    /// <inheritdoc/>
    /// <remarks>Takes the high half of the 64-bit draw.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static uint NextUInt32(ref WyRand source) => (uint)(NextUInt64(ref source) >> 32);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public readonly void CopyStateTo(Span<ulong> destination)
    {
        if (destination.Length < WordCount)
        {
            RandomThrowHelper.ThrowDestinationTooShortForState(WordCount, nameof(destination));
        }

        destination[0] = _state;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static WyRand FromState(ReadOnlySpan<ulong> state)
    {
        if (state.Length != WordCount)
        {
            RandomThrowHelper.ThrowStateSpanLengthMismatch(WordCount, state.Length, nameof(state));
        }

        // A restored zero word is the unseeded state, not a dead one: the next draw reseeds from
        // entropy, exactly as a default-constructed engine does.
        return new WyRand(state[0]);
    }

    /// <summary>Compares the state words of two generators.</summary>
    /// <param name="other">The generator to compare against.</param>
    /// <returns><see langword="true"/> when both sit at the same position in the same stream.</returns>
    public bool Equals(WyRand other) => _state == other._state;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is WyRand other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _state.GetHashCode();

    /// <summary>Equality operator.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both sit at the same position in the same stream.</returns>
    public static bool operator ==(WyRand left, WyRand right) => left._state == right._state;

    /// <summary>Inequality operator.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when the two are at different stream positions.</returns>
    public static bool operator !=(WyRand left, WyRand right) => left._state != right._state;

    /// <summary>Replaces the zero state with an entropy-derived one.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ReseedFromEntropy() => _state = RandomEntropy.NextUInt64() | 1UL;
}
