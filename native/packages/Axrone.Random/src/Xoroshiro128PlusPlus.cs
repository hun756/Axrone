namespace Axrone.Random;

/// <summary>
/// xoroshiro128++ 1.1, the 128-bit member of the xoshiro family (Blackman and Vigna, 2018) with
/// the (49,21,28) rotation triple. Two 64-bit words of state, four xor-shifts, two rotations
/// and one add per draw, and a 2^128 - 1 period with linear complexity 128 - the quality of a
/// far larger state for the cost of a small one. This is the engine the ambient thread-local
/// stream runs on.
/// </summary>
/// <remarks>
/// <para><b>Seeding.</b> A 64-bit seed is expanded through <see cref="SplitMix64"/> into the
/// two state words, so one seed produces the same 128-bit state on every platform.</para>
/// <para><b>Zero state.</b> An all-zero state is the only fixed point of the recurrence, so it
/// is treated as <em>unseeded</em>: the first draw reseeds from operating-system entropy
/// instead of streaming zeros forever. Every other seed is fully deterministic.</para>
/// <para><b>Jumps.</b> The state is a vector over GF(2), so advancing it by 2^64 steps is a
/// linear map: the polynomial <c>x^(2^64) mod p(x)</c>, where <c>p(x)</c> is the characteristic
/// polynomial of the transition, applied through the same accumulate-and-step loop the reference
/// uses. The tables below are <c>x^(2^64) mod p(x)</c> and <c>x^(2^96) mod p(x)</c> as
/// little-endian coefficient words, so one jump is 128 state transitions instead of 2^64 draws:
/// a lane decomposition of 2^64 disjoint streams costs 128 steps per lane. The characteristic
/// polynomial of this triple is <c>{0x8DAE70779760B081, 0x0031BCF2F855D6E5}</c>, with the
/// x^128 term implicit.</para>
/// </remarks>
public struct Xoroshiro128PlusPlus : IJumpableRandomSource<Xoroshiro128PlusPlus>, IRandomStateSnapshot<Xoroshiro128PlusPlus>, IEquatable<Xoroshiro128PlusPlus>
{
    /// <summary>Coefficient words of <c>x^(2^64) mod p(x)</c>: the 2^64-step jump.</summary>
    private static ReadOnlySpan<ulong> JumpPolynomial => [0x2BD7A6A6E99C2DDCUL, 0x0992CCAF6A6FCA05UL];

    /// <summary>Coefficient words of <c>x^(2^96) mod p(x)</c>: the 2^96-step long jump.</summary>
    private static ReadOnlySpan<ulong> LongJumpPolynomial => [0x360FD5F2CF8D5D99UL, 0x9C6E6877736C46E3UL];

    /// <summary>Number of 64-bit words in a state snapshot: the two state words.</summary>
    public const int WordCount = 2;

    private ulong _s0;
    private ulong _s1;

    /// <inheritdoc/>
    /// <remarks>Implemented explicitly so the constant stays usable in a <c>stackalloc</c>.</remarks>
    static int IRandomStateSnapshot<Xoroshiro128PlusPlus>.WordCount => WordCount;

    /// <summary>Creates a state expanded from <paramref name="seed"/> through SplitMix64.</summary>
    /// <param name="seed">The seed; the same seed always yields the same stream.</param>
    public Xoroshiro128PlusPlus(ulong seed)
    {
        SplitMix64 mixer = SplitMix64.Create(seed);
        _s0 = SplitMix64.NextUInt64(ref mixer);
        _s1 = SplitMix64.NextUInt64(ref mixer);

        // SplitMix64 never returns two consecutive zeros in practice, but the all-zero state is
        // the one absorbing state of this recurrence, so the invariant is enforced here rather
        // than assumed: a dead stream is a silent data defect nobody would trace back to a seed.
        if ((_s0 | _s1) == 0UL)
        {
            _s1 = RandomEntropy.NextUInt64();
        }
    }

    /// <inheritdoc/>
    public static Xoroshiro128PlusPlus Create(ulong seed) => new Xoroshiro128PlusPlus(seed);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ulong NextUInt64(ref Xoroshiro128PlusPlus source)
    {
        if ((source._s0 | source._s1) == 0UL)
        {
            source.ReseedFromEntropy();
        }

        // The output word is formed from the state *before* s1 is folded into s0: the reference
        // computes rotl(s0 + s1, 17) + s0 first and only then applies s1 ^= s0 to the transition.
        ulong s0 = source._s0;
        ulong s1 = source._s1;
        ulong result = BitOperations.RotateLeft(s0 + s1, 17) + s0;
        s1 ^= s0;
        source._s0 = BitOperations.RotateLeft(s0, 49) ^ s1 ^ (s1 << 21);
        source._s1 = BitOperations.RotateLeft(s1, 28);
        return result;
    }

    /// <inheritdoc/>
    /// <remarks>Takes the high half of the 64-bit draw.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static uint NextUInt32(ref Xoroshiro128PlusPlus source) => (uint)(NextUInt64(ref source) >> 32);

    /// <inheritdoc/>
    public static void Jump(ref Xoroshiro128PlusPlus source) => source.ApplyPolynomial(JumpPolynomial);

    /// <inheritdoc/>
    public static void LongJump(ref Xoroshiro128PlusPlus source) => source.ApplyPolynomial(LongJumpPolynomial);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public readonly void CopyStateTo(Span<ulong> destination)
    {
        if (destination.Length < WordCount)
        {
            RandomThrowHelper.ThrowDestinationTooShortForState(WordCount, nameof(destination));
        }

        destination[0] = _s0;
        destination[1] = _s1;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Xoroshiro128PlusPlus FromState(ReadOnlySpan<ulong> state)
    {
        if (state.Length != WordCount)
        {
            RandomThrowHelper.ThrowStateSpanLengthMismatch(WordCount, state.Length, nameof(state));
        }

        // A restored all-zero state is the unseeded state, not a dead one: the next draw reseeds
        // from entropy, exactly as a default-constructed engine does.
        Xoroshiro128PlusPlus engine = default;
        engine._s0 = state[0];
        engine._s1 = state[1];
        return engine;
    }

    /// <summary>Compares the state words of two generators.</summary>
    /// <param name="other">The generator to compare against.</param>
    /// <returns><see langword="true"/> when both sit at the same position in the same stream.</returns>
    public bool Equals(Xoroshiro128PlusPlus other) => _s0 == other._s0 && _s1 == other._s1;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Xoroshiro128PlusPlus other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_s0, _s1);

    /// <summary>Equality operator.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both sit at the same position in the same stream.</returns>
    public static bool operator ==(Xoroshiro128PlusPlus left, Xoroshiro128PlusPlus right) =>
        left._s0 == right._s0 && left._s1 == right._s1;

    /// <summary>Inequality operator.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when the two are at different stream positions.</returns>
    public static bool operator !=(Xoroshiro128PlusPlus left, Xoroshiro128PlusPlus right) => !(left == right);

    /// <summary>
    /// Applies a GF(2) jump polynomial to the state: for every set coefficient bit, the current
    /// state is accumulated and the state is advanced one step, which realises the polynomial as a
    /// sum of powers of the transition matrix.
    /// </summary>
    /// <param name="polynomial">Little-endian coefficient words; bit <c>64 * i + b</c> selects <c>x^(64 * i + b)</c>.</param>
    private void ApplyPolynomial(ReadOnlySpan<ulong> polynomial)
    {
        if ((_s0 | _s1) == 0UL)
        {
            ReseedFromEntropy();
        }

        ulong accumulator0 = 0UL;
        ulong accumulator1 = 0UL;
        foreach (ulong word in polynomial)
        {
            for (int bit = 0; bit < 64; bit++)
            {
                if ((word & (1UL << bit)) != 0UL)
                {
                    accumulator0 ^= _s0;
                    accumulator1 ^= _s1;
                }

                StepState();
            }
        }

        _s0 = accumulator0;
        _s1 = accumulator1;
    }

    /// <summary>Advances the state without producing the output word.</summary>
    private void StepState()
    {
        ulong s1 = _s1 ^ _s0;
        _s0 = BitOperations.RotateLeft(_s0, 49) ^ s1 ^ (s1 << 21);
        _s1 = BitOperations.RotateLeft(s1, 28);
    }

    /// <summary>Replaces the zero state with an entropy-derived one.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ReseedFromEntropy()
    {
        SplitMix64 mixer = SplitMix64.Create(RandomEntropy.NextUInt64());
        _s0 = SplitMix64.NextUInt64(ref mixer);
        _s1 = SplitMix64.NextUInt64(ref mixer);
    }
}
