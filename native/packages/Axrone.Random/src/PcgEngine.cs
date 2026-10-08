namespace Axrone.Random;

/// <summary>
/// PCG-XSH-RR 64/32 (O'Neill, 2014), the canonical form of the permuted congruential generator:
/// a 64-bit LCG state, a 64-bit stream increment, and an output function that xorshifts the high
/// bits down and rotates them by the top five bits of the <em>pre-step</em> state. One 64x64
/// multiply-add and three shifts on the state, no multiply at all in the output stage - the
/// cheapest 64-bit-state generator in the suite, and the only one here whose stream is a
/// published standard.
/// </summary>
/// <remarks>
/// <para><b>Seeding.</b> The reference <c>pcg_setseq_64_srandom_r(initstate, initseq)</c>: start
/// from a zero state, install the odd increment <c>(initseq &lt;&lt; 1) | 1</c>, advance once,
/// fold <c>initstate</c> into the state and advance again. Both seeding draws are discarded.
/// <see cref="Create"/> derives the sequence from the seed itself with
/// <see cref="StreamSelector"/>, so one 64-bit seed addresses a fixed, reproducible stream and
/// two seeds never collide unless they are equal - the whole seed space is the stream space.</para>
/// <para><b>Arithmetic.</b> Every step is <c>state = state * <see cref="Multiplier"/> + inc</c> in
/// wrapping 64-bit arithmetic: the multiply is 64x64-to-64, never widened, and the output stage
/// is pure shifts and one rotate. There is no 128-bit operation anywhere on a draw. (128-bit
/// arithmetic is what a <em>64-bit-output</em> PCG, whose state and multiplier are 128 bits wide,
/// needs - not this 64/32 form.)</para>
/// <para><b>Jump-ahead.</b> The step is the affine map <c>f(x) = Mx + c</c> over Z/2^64, so
/// <c>f^2(x) = M^2 x + (M + 1) c</c>: squaring a jump only needs a multiply and one extra add,
/// and composing two jumps is <c>(m1, p1) o (m2, p2) = (m1 m2, m1 p2 + p1)</c>. Binary
/// square-and-multiply therefore realises <c>f^steps</c> in O(log steps) with nothing but
/// wrapping 64-bit multiplies - no division, and no inversion of <c>M - 1</c>, which does not
/// exist modulo 2^64 because <c>M</c> is odd. <see cref="JumpAhead(ref PcgEngine, ulong)"/>
/// applies that composition; the increment is part of the stream's identity and never changes.</para>
/// <para><b>Why not <see cref="IJumpableRandomSource{TSelf}"/>.</b> The two fixed jump distances
/// of that interface are meaningless here and would be a trap: the LCG's period is exactly
/// 2^64, so the interface's "2^64-step" jump is the <em>identity</em>, and its "2^96-step" long
/// jump collapses to 2^32 because 2^96 = 2^32 (mod 2^64). A lane decomposition needs an
/// arbitrary distance, which <see cref="JumpAhead(ref PcgEngine, ulong)"/> gives directly
/// (<c>JumpAhead(2^32 * lane)</c> hands lane <c>k</c> a disjoint slice).</para>
/// <para><b>Dead state.</b> Any pair (state, <em>odd</em> increment) is a valid full-period LCG
/// state, so there is no zero-state fixed point to guard - a PCG stream cannot degenerate into
/// zeros. The one unusable state is the default-constructed one, whose increment is even (zero),
/// because then the state is frozen at zero forever. That single case is treated as
/// <em>unseeded</em> and reseeds from operating-system entropy on the first draw, and
/// <see cref="FromState(ReadOnlySpan{ulong})"/> rejects an even increment so a restored state can
/// never be the dead one.</para>
/// </remarks>
public struct PcgEngine : IRandomSource<PcgEngine>, IRandomStateSnapshot<PcgEngine>, IEquatable<PcgEngine>
{
    /// <summary>The LCG multiplier: the 64-bit approximation of the golden ratio used by the reference.</summary>
    public const ulong Multiplier = 6364136223846793005UL;

    /// <summary>
    /// The arbitrary fixed selector <see cref="Create"/> xors into a seed to choose its stream
    /// sequence. Any constant works; this one is fixed forever so seeds stay reproducible.
    /// </summary>
    public const ulong StreamSelector = 0xDA3E39CB94B95BDBUL;

    /// <summary>Number of 64-bit words in a state snapshot: the state, then the stream increment.</summary>
    public const int WordCount = 2;

    private ulong _state;
    private ulong _inc;

    /// <inheritdoc/>
    /// <remarks>Implemented explicitly so the constant stays usable in a <c>stackalloc</c>.</remarks>
    static int IRandomStateSnapshot<PcgEngine>.WordCount => WordCount;

    /// <summary>Creates the canonical reference state for the given seed pair.</summary>
    /// <param name="initstate">The seed folded into the state between the two discarded seeding draws.</param>
    /// <param name="initseq">The stream sequence; only its low bit is ignored, as the reference forces the increment odd.</param>
    public PcgEngine(ulong initstate, ulong initseq)
    {
        // The reference seeding dance, spelled out rather than folded into a helper: the two
        // discarded draws are what separate streams that share a state, and their order is the
        // part a "simplification" would silently get wrong.
        PcgEngine engine = default;
        engine._inc = (initseq << 1) | 1UL;
        NextUInt32(ref engine);
        engine._state = unchecked(engine._state + initstate);
        NextUInt32(ref engine);
        _state = engine._state;
        _inc = engine._inc;
    }

    /// <inheritdoc/>
    /// <remarks>Uses <c>seed ^ <see cref="StreamSelector"/></c> as the stream sequence; see the remarks.</remarks>
    public static PcgEngine Create(ulong seed) => new PcgEngine(seed, seed ^ StreamSelector);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static uint NextUInt32(ref PcgEngine source)
    {
        // `_inc == 0` is exactly the default-constructed (unseeded) state: any constructed state
        // has an odd increment by construction, so this one compare-and-branch is the whole guard.
        if (source._inc == 0UL)
        {
            source.ReseedFromEntropy();
        }

        ulong oldState = source._state;
        source._state = unchecked((oldState * Multiplier) + source._inc);

        // XSH-RR: fold the state with itself shifted 18 bits up (so the low half of the word can
        // never dominate the output), take bits 27..58 down into a 32-bit word, and rotate right
        // by the top five bits of the pre-step state. The shift by 59 leaves 0..31, which is
        // exactly the rotate range, so the rotation count needs no masking.
        uint xorshifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        return BitOperations.RotateRight(xorshifted, (int)(oldState >> 59));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Pairs two 32-bit draws, low word first. PCG-XSH-RR 64/32 is a 32-bit-output generator:
    /// this is a widening of its output, not the distinct 64-bit-output pcg64 stream.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ulong NextUInt64(ref PcgEngine source)
    {
        uint low = NextUInt32(ref source);
        uint high = NextUInt32(ref source);
        return ((ulong)high << 32) | low;
    }

    /// <summary>
    /// Advances the state by <paramref name="steps"/> draws in O(log steps), producing none of the
    /// words in between: the lane-decomposition primitive of a permuted congruential generator.
    /// </summary>
    /// <param name="source">The state to advance. It is mutated in place.</param>
    /// <param name="steps">
    /// The number of draws to skip, modulo 2^64 - the LCG's period, so distances beyond it wrap.
    /// Zero is a no-op.
    /// </param>
    /// <remarks>
    /// Square-and-multiply over the affine step; see the remarks for the algebra. Cold path:
    /// non-inlined, and it allocates nothing.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void JumpAhead(ref PcgEngine source, ulong steps) => source.Advance(steps);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public readonly void CopyStateTo(Span<ulong> destination)
    {
        if (destination.Length < WordCount)
        {
            RandomThrowHelper.ThrowDestinationTooShortForState(WordCount, nameof(destination));
        }

        destination[0] = _state;
        destination[1] = _inc;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static PcgEngine FromState(ReadOnlySpan<ulong> state)
    {
        if (state.Length != WordCount)
        {
            RandomThrowHelper.ThrowStateSpanLengthMismatch(WordCount, state.Length, nameof(state));
        }

        // An even increment is not a state a PCG stream can be resumed from - it would run at a
        // fraction of the period - and it is the shape the unseeded default state has, so the
        // guard is what keeps a restored engine from becoming a stream of zeros.
        if ((state[1] & 1UL) == 0UL)
        {
            RandomThrowHelper.ThrowStreamIncrementMustBeOdd(nameof(state));
        }

        PcgEngine engine = default;
        engine._state = state[0];
        engine._inc = state[1];
        return engine;
    }

    /// <summary>Compares the state and increment of two generators.</summary>
    /// <param name="other">The generator to compare against.</param>
    /// <returns><see langword="true"/> when both sit at the same position in the same stream.</returns>
    public bool Equals(PcgEngine other) => _state == other._state && _inc == other._inc;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is PcgEngine other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_state, _inc);

    /// <summary>Equality operator.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both sit at the same position in the same stream.</returns>
    public static bool operator ==(PcgEngine left, PcgEngine right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when the two are at different stream positions.</returns>
    public static bool operator !=(PcgEngine left, PcgEngine right) => !left.Equals(right);

    /// <summary>Replaces the unseeded default state with an entropy-derived one.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ReseedFromEntropy()
    {
        // An odd increment makes the LCG full period and one entropy word makes the state
        // uniformly distributed over the period; no reference draw has to be discarded because
        // no part of the pair is derived from the other.
        _inc = (RandomEntropy.NextUInt64() << 1) | 1UL;
        _state = RandomEntropy.NextUInt64();
    }

    /// <summary>
    /// Applies the step map <c>f(steps)</c> to the state, where <c>f(x) = Mx + inc</c>, by binary
    /// square-and-multiply over the affine pairs (multiplier, addend).
    /// </summary>
    /// <param name="steps">The number of draws to skip; consumed by this method.</param>
    private void Advance(ulong steps)
    {
        if (_inc == 0UL)
        {
            ReseedFromEntropy();
        }

        ulong accumulatedMultiplier = 1UL;
        ulong accumulatedAddend = 0UL;
        ulong multiplier = Multiplier;
        ulong addend = _inc;

        while (steps != 0UL)
        {
            if ((steps & 1UL) != 0UL)
            {
                // f^a o f^b = (ma mb, ma pb + pa): the accumulated map is composed with the
                // current power of two. Every product is a wrapping 64-bit multiply - the modulus
                // is applied by the arithmetic itself, so no masking and no 128-bit carry is needed.
                accumulatedAddend = unchecked((accumulatedAddend * multiplier) + addend);
                accumulatedMultiplier = unchecked(accumulatedMultiplier * multiplier);
            }

            // Squaring: f^2k = (mk^2, (mk + 1) pk).
            addend = unchecked((multiplier + 1UL) * addend);
            multiplier = unchecked(multiplier * multiplier);
            steps >>= 1;
        }

        _state = unchecked((accumulatedMultiplier * _state) + accumulatedAddend);
    }
}