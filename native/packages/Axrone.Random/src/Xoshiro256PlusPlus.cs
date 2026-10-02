namespace Axrone.Random;

/// <summary>
/// xoshiro256++ 1.0, the 256-bit member of the xoshiro family (Blackman and Vigna, 2018) with
/// the (45,17,23) rotation triple. Four 64-bit words of state, the same handful of operations
/// as xoroshiro128++ and a 2^256 - 1 period: the default choice whenever long-lived streams or
/// parallel lanes matter more than the last bit of scalar throughput.
/// </summary>
/// <remarks>
/// <para><b>Seeding.</b> A 64-bit seed is expanded through <see cref="SplitMix64"/> into the
/// four state words, so one seed produces the same 256-bit state on every platform.</para>
/// <para><b>Zero state.</b> An all-zero state is the only absorbing state of the recurrence, so
/// it is treated as <em>unseeded</em>: the first draw reseeds from operating-system entropy
/// instead of streaming zeros forever. Every other seed is fully deterministic.</para>
/// <para><b>Jumps.</b> As in xoroshiro128++, the state is a vector over GF(2) and a jump is the
/// polynomial <c>x^(2^k) mod p(x)</c> applied through the reference accumulate-and-step loop:
/// <c>Jump</c> uses <c>k = 128</c>, <c>LongJump</c> uses <c>k = 192</c>, and each costs 256 state
/// transitions instead of 2^128 or 2^192 draws. The characteristic polynomial of this triple is
/// <c>{0x9D116F2BB0F0F001, 0x0280002BCefd1A5E, 0x04B4EDCF26259F85, 0x0003C03C3F3ECB19}</c>,
/// with the x^256 term implicit.</para>
/// </remarks>
public struct Xoshiro256PlusPlus : IJumpableRandomSource<Xoshiro256PlusPlus>, IRandomStateSnapshot<Xoshiro256PlusPlus>, IEquatable<Xoshiro256PlusPlus>
{
    /// <summary>Coefficient words of <c>x^(2^128) mod p(x)</c>: the 2^128-step jump.</summary>
    private static ReadOnlySpan<ulong> JumpPolynomial =>
    [
        0x180EC6D33CFD0ABAUL,
        0xD5A61266F0C9392CUL,
        0xA9582618E03FC9AAUL,
        0x39ABDC4529B1661CUL
    ];

    /// <summary>Coefficient words of <c>x^(2^192) mod p(x)</c>: the 2^192-step long jump.</summary>
    private static ReadOnlySpan<ulong> LongJumpPolynomial =>
    [
        0x76E15D3EFEFDCBBFUL,
        0xC5004E441C522FB3UL,
        0x77710069854EE241UL,
        0x39109BB02ACBE635UL
    ];

    /// <summary>Number of 64-bit words in a state snapshot: the four state words.</summary>
    public const int WordCount = 4;

    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    /// <inheritdoc/>
    /// <remarks>Implemented explicitly so the constant stays usable in a <c>stackalloc</c>.</remarks>
    static int IRandomStateSnapshot<Xoshiro256PlusPlus>.WordCount => WordCount;

    /// <summary>Creates a state expanded from <paramref name="seed"/> through SplitMix64.</summary>
    /// <param name="seed">The seed; the same seed always yields the same stream.</param>
    public Xoshiro256PlusPlus(ulong seed)
    {
        SplitMix64 mixer = SplitMix64.Create(seed);
        _s0 = SplitMix64.NextUInt64(ref mixer);
        _s1 = SplitMix64.NextUInt64(ref mixer);
        _s2 = SplitMix64.NextUInt64(ref mixer);
        _s3 = SplitMix64.NextUInt64(ref mixer);

        // As in xoroshiro128++, the all-zero state is enforced never to exist rather than merely
        // assumed not to: a stream of zeros is a silent data defect, not a bad seed.
        if ((_s0 | _s1 | _s2 | _s3) == 0UL)
        {
            _s3 = RandomEntropy.NextUInt64();
        }
    }

    /// <inheritdoc/>
    public static Xoshiro256PlusPlus Create(ulong seed) => new Xoshiro256PlusPlus(seed);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ulong NextUInt64(ref Xoshiro256PlusPlus source)
    {
        if ((source._s0 | source._s1 | source._s2 | source._s3) == 0UL)
        {
            source.ReseedFromEntropy();
        }

        ulong result = BitOperations.RotateLeft(source._s0 + source._s3, 23) + source._s0;
        ulong t = source._s1 << 17;
        source._s2 ^= source._s0;
        source._s3 ^= source._s1;
        source._s1 ^= source._s2;
        source._s0 ^= source._s3;
        source._s2 ^= t;
        source._s3 = BitOperations.RotateLeft(source._s3, 45);
        return result;
    }

    /// <inheritdoc/>
    /// <remarks>Takes the high half of the 64-bit draw.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static uint NextUInt32(ref Xoshiro256PlusPlus source) => (uint)(NextUInt64(ref source) >> 32);

    /// <inheritdoc/>
    public static void Jump(ref Xoshiro256PlusPlus source) => source.ApplyPolynomial(JumpPolynomial);

    /// <inheritdoc/>
    public static void LongJump(ref Xoshiro256PlusPlus source) => source.ApplyPolynomial(LongJumpPolynomial);

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
        destination[2] = _s2;
        destination[3] = _s3;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Xoshiro256PlusPlus FromState(ReadOnlySpan<ulong> state)
    {
        if (state.Length != WordCount)
        {
            RandomThrowHelper.ThrowStateSpanLengthMismatch(WordCount, state.Length, nameof(state));
        }

        // A restored all-zero state is the unseeded state, not a dead one: the next draw reseeds
        // from entropy, exactly as a default-constructed engine does.
        Xoshiro256PlusPlus engine = default;
        engine._s0 = state[0];
        engine._s1 = state[1];
        engine._s2 = state[2];
        engine._s3 = state[3];
        return engine;
    }

    /// <summary>Compares the state words of two generators.</summary>
    /// <param name="other">The generator to compare against.</param>
    /// <returns><see langword="true"/> when both sit at the same position in the same stream.</returns>
    public bool Equals(Xoshiro256PlusPlus other) =>
        _s0 == other._s0 && _s1 == other._s1 && _s2 == other._s2 && _s3 == other._s3;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Xoshiro256PlusPlus other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_s0, _s1, _s2, _s3);

    /// <summary>Equality operator.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both sit at the same position in the same stream.</returns>
    public static bool operator ==(Xoshiro256PlusPlus left, Xoshiro256PlusPlus right) =>
        left._s0 == right._s0 && left._s1 == right._s1 && left._s2 == right._s2 && left._s3 == right._s3;

    /// <summary>Inequality operator.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when the two are at different stream positions.</returns>
    public static bool operator !=(Xoshiro256PlusPlus left, Xoshiro256PlusPlus right) => !(left == right);

    /// <summary>
    /// Applies a GF(2) jump polynomial to the state: for every set coefficient bit, the current
    /// state is accumulated and the state is advanced one step, which realises the polynomial as a
    /// sum of powers of the transition matrix.
    /// </summary>
    /// <param name="polynomial">Little-endian coefficient words; bit <c>64 * i + b</c> selects <c>x^(64 * i + b)</c>.</param>
    private void ApplyPolynomial(ReadOnlySpan<ulong> polynomial)
    {
        if ((_s0 | _s1 | _s2 | _s3) == 0UL)
        {
            ReseedFromEntropy();
        }

        ulong accumulator0 = 0UL;
        ulong accumulator1 = 0UL;
        ulong accumulator2 = 0UL;
        ulong accumulator3 = 0UL;
        foreach (ulong word in polynomial)
        {
            for (int bit = 0; bit < 64; bit++)
            {
                if ((word & (1UL << bit)) != 0UL)
                {
                    accumulator0 ^= _s0;
                    accumulator1 ^= _s1;
                    accumulator2 ^= _s2;
                    accumulator3 ^= _s3;
                }

                StepState();
            }
        }

        _s0 = accumulator0;
        _s1 = accumulator1;
        _s2 = accumulator2;
        _s3 = accumulator3;
    }

    /// <summary>Advances the state without producing the output word.</summary>
    private void StepState()
    {
        ulong t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);
    }

    /// <summary>Replaces the zero state with an entropy-derived one.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ReseedFromEntropy()
    {
        SplitMix64 mixer = SplitMix64.Create(RandomEntropy.NextUInt64());
        _s0 = SplitMix64.NextUInt64(ref mixer);
        _s1 = SplitMix64.NextUInt64(ref mixer);
        _s2 = SplitMix64.NextUInt64(ref mixer);
        _s3 = SplitMix64.NextUInt64(ref mixer);
    }
}
