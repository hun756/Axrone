namespace Axrone.Random;

/// <summary>
/// MT19937, the 32-bit Mersenne Twister (Matsumoto and Nishimura, 1998, with the 2002
/// initialization). A 624-word state twisted in place, a 2^19937 - 1 period and a 32-bit
/// tempered output. It is the reference engine of the suite: bit-for-bit the published
/// mt19937ar stream, which is what makes it the only engine here whose output can be checked
/// against an external vector at all.
/// </summary>
/// <remarks>
/// <para><b>Cost.</b> The state is 2 496 bytes - 260x the xoshiro256++ state - so a draw is
/// measured in memory traffic rather than arithmetic, and the wrapper never hoists this state
/// into a local. Reach for it when reproducing an existing MT stream matters more than
/// throughput; otherwise use the xoshiro family.</para>
/// <para><b>Seeding.</b> <see cref="MersenneTwister(uint)"/> is <c>init_genrand</c> from the
/// reference, and reproduces its published vectors exactly: seed <see cref="ReferenceSeed"/>
/// draws <c>3499211612</c> first. <see cref="MersenneTwister(ReadOnlySpan{uint})"/> and
/// <see cref="Create"/> are <c>init_by_array</c>, which folds an arbitrary-length key into the
/// state and starts from <see cref="ArrayInitialiserSeed"/>.</para>
/// <para><b>Zero state.</b> The all-zero default state is treated as <em>unseeded</em>: the
/// first draw initializes from operating-system entropy. A <c>default(MersenneTwister)</c>
/// therefore still produces a live, non-zero stream.</para>
/// <para><b>Seeding cost.</b> <c>init_genrand</c> runs 624 rounds of the recurrence before the
/// first draw, so constructing an engine costs far more than drawing from one. Seed a long-lived
/// engine once and share it; do not create a MersenneTwister per entity.</para>
/// </remarks>
public struct MersenneTwister : IRandomSource<MersenneTwister>, IEquatable<MersenneTwister>
{
    /// <summary>State size in 32-bit words.</summary>
    public const int StateWords = 624;

    /// <summary>Offset of the middle word in the recurrence.</summary>
    public const int MiddleWord = 397;

    /// <summary>Twist matrix coefficient <c>a_{w-1}</c>.</summary>
    public const uint MatrixA = 0x9908B0DFU;

    /// <summary>Tempering mask <c>b</c> for the left shift by 7.</summary>
    public const uint TemperingMaskB = 0x9D2C5680U;

    /// <summary>Tempering mask <c>c</c> for the left shift by 15.</summary>
    public const uint TemperingMaskC = 0xEFC60000U;

    /// <summary>The reference seed of the published MT19937 test vectors (5489).</summary>
    public const uint ReferenceSeed = 5489U;

    /// <summary>The fixed base seed <c>init_by_array</c> starts its folding from.</summary>
    public const uint ArrayInitialiserSeed = 19650218U;

    private const uint InitialiserMultiplier = 1812433253U;
    private const uint KeyMultiplier = 1664525U;
    private const uint ScrambleMultiplier = 1566083941U;
    private const uint UpperMask = 0x80000000U;
    private const uint LowerMask = 0x7FFFFFFFU;
    private const int WrapOffset = StateWords - MiddleWord;
    private const int SeedKeyWords = 8;

    /// <summary>
    /// The 624-word state, stored inline. The engine is a value type with no heap allocation and
    /// no indirection: advancing it is a pure register/stack operation, at the price of a
    /// 2 496-byte copy whenever the struct is copied.
    /// </summary>
    [InlineArray(StateWords)]
    private struct StateBuffer
    {
        private uint _word;
    }

    private StateBuffer _state;
    private int _index;
    private bool _initialised;

    /// <summary>Creates an engine seeded from operating-system entropy.</summary>
    public MersenneTwister() => InitialiseFromEntropy();

    /// <summary>Creates an engine seeded with the reference <c>init_genrand</c>.</summary>
    /// <param name="seed">
    /// The 32-bit seed. <see cref="ReferenceSeed"/> reproduces the published mt19937ar vector.
    /// </param>
    public MersenneTwister(uint seed) => InitialiseGenRand(seed);

    /// <summary>Creates an engine seeded with the reference <c>init_by_array</c>.</summary>
    /// <param name="key">The seed key, folded into the state. Must contain at least one word.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    public MersenneTwister(ReadOnlySpan<uint> key) => InitialiseByKey(key);

    /// <inheritdoc/>
    /// <remarks>
    /// The 64-bit seed is expanded through <see cref="SplitMix64"/> into an eight-word
    /// <c>init_by_array</c> key, so no seed entropy is discarded. A stream created this way is
    /// reproducible, but it is not one of the published reference vectors: for those, construct
    /// with <see cref="MersenneTwister(uint)"/> or <see cref="MersenneTwister(ReadOnlySpan{uint})"/>.
    /// </remarks>
    public static MersenneTwister Create(ulong seed)
    {
        Span<uint> key = stackalloc uint[SeedKeyWords];
        SplitMix64 mixer = SplitMix64.Create(seed);
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = (uint)SplitMix64.NextUInt64(ref mixer);
        }

        MersenneTwister source = default;
        source.InitialiseByKey(key);
        return source;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static uint NextUInt32(ref MersenneTwister source)
    {
        source.EnsureInitialised();
        return source.Draw();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Pairs two 32-bit draws, low word first. MT19937 is a 32-bit generator: this is a widening
    /// of its output, not the distinct 64-bit MT19937-64 stream.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ulong NextUInt64(ref MersenneTwister source)
    {
        uint low = NextUInt32(ref source);
        uint high = NextUInt32(ref source);
        return ((ulong)high << 32) | low;
    }

    /// <summary>Copies the raw state words into <paramref name="destination"/>.</summary>
    /// <param name="destination">The span to fill. Must hold at least <see cref="StateWords"/> entries.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is shorter than <see cref="StateWords"/>.
    /// </exception>
    /// <remarks>
    /// The read cursor is not part of the exported words; a state restored from this buffer
    /// begins drawing from word 0 after a twist.
    /// </remarks>
    public readonly void CopyStateTo(Span<uint> destination)
    {
        if (destination.Length < StateWords)
        {
            RandomThrowHelper.ThrowDestinationTooShortForState(StateWords, nameof(destination));
        }

        for (int i = 0; i < StateWords; i++)
        {
            destination[i] = _state[i];
        }
    }

    /// <summary>Compares the full state of two engines.</summary>
    /// <param name="other">The engine to compare against.</param>
    /// <returns><see langword="true"/> when both hold the same state, cursor and initialization.</returns>
    public bool Equals(MersenneTwister other)
    {
        if (_index != other._index || _initialised != other._initialised)
        {
            return false;
        }

        for (int i = 0; i < StateWords; i++)
        {
            if (_state[i] != other._state[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is MersenneTwister other && Equals(other);

    /// <inheritdoc/>
    /// <remarks>
    /// Samples three state words plus the cursor instead of folding all 624: equal states always
    /// hash equally, and a 2 496-byte hash would dominate the cost of using one as a key.
    /// </remarks>
    public override int GetHashCode() => HashCode.Combine(_state[0], _state[StateWords >> 1], _state[StateWords - 1], _index);

    /// <summary>Equality operator.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both hold the same state.</returns>
    public static bool operator ==(MersenneTwister left, MersenneTwister right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when the two hold different states.</returns>
    public static bool operator !=(MersenneTwister left, MersenneTwister right) => !left.Equals(right);

    /// <summary>Initializes from entropy unless a seeding path already ran.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureInitialised()
    {
        if (!_initialised)
        {
            InitialiseFromEntropy();
        }
    }

    /// <summary>Consumes one tempered word, twisting the state when the cursor runs off the end.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint Draw()
    {
        if (_index >= StateWords)
        {
            Twist();
        }

        uint y = _state[_index++];
        y ^= y >> 11;
        y ^= (y << 7) & TemperingMaskB;
        y ^= (y << 15) & TemperingMaskC;
        y ^= y >> 18;
        return y;
    }

    /// <summary>The reference <c>init_genrand</c>: a Knuth-multiplier recurrence over the state.</summary>
    /// <param name="seed">The 32-bit seed.</param>
    private void InitialiseGenRand(uint seed)
    {
        _state[0] = seed;
        for (int i = 1; i < StateWords; i++)
        {
            uint previous = _state[i - 1];
            _state[i] = (InitialiserMultiplier * (previous ^ (previous >> 30))) + (uint)i;
        }

        _index = StateWords;
        _initialised = true;
    }

    /// <summary>
    /// The reference <c>init_by_array</c>: the state is seeded from
    /// <see cref="ArrayInitialiserSeed"/> and then folded twice over the key, once with
    /// <see cref="KeyMultiplier"/> and once with <see cref="ScrambleMultiplier"/>. Folding an
    /// arbitrary-length key is what the 2002 initialization added over <c>init_genrand</c>.
    /// </summary>
    /// <param name="key">The seed key. Must not be empty.</param>
    private void InitialiseByKey(ReadOnlySpan<uint> key)
    {
        if (key.IsEmpty)
        {
            RandomThrowHelper.ThrowKeyMustNotBeEmpty(nameof(key));
        }

        InitialiseGenRand(ArrayInitialiserSeed);

        int index = 1;
        int keyIndex = 0;
        int rounds = Math.Max(StateWords, key.Length);
        for (int i = 0; i < rounds; i++)
        {
            uint previous = _state[index - 1];
            _state[index] = (_state[index] ^ ((previous ^ (previous >> 30)) * KeyMultiplier)) + key[keyIndex] + (uint)keyIndex;
            index++;
            keyIndex++;
            if (index >= StateWords)
            {
                _state[0] = _state[StateWords - 1];
                index = 1;
            }

            if (keyIndex >= key.Length)
            {
                keyIndex = 0;
            }
        }

        for (int i = StateWords - 1; i > 0; i--)
        {
            uint previous = _state[index - 1];
            _state[index] = (_state[index] ^ ((previous ^ (previous >> 30)) * ScrambleMultiplier)) - (uint)index;
            index++;
            if (index >= StateWords)
            {
                _state[0] = _state[StateWords - 1];
                index = 1;
            }
        }

        // The reference sets the top bit of the first word here; that bit is what makes the
        // tempered output and the recurrence stay non-degenerate for every key.
        _state[0] = UpperMask;
    }

    /// <summary>Twists the whole state in place, then rewinds the cursor.</summary>
    /// <remarks>
    /// The <c>mag01</c> selection of <see cref="MatrixA"/> is written branchlessly as
    /// <c>(0 - (y &amp; 1)) &amp; MatrixA</c>, which is a subtract and an and where the reference
    /// indexes a two-entry table; the loop is otherwise the reference recurrence, unrolled by the
    /// compiler over a fixed 624-word state.
    /// </remarks>
    private void Twist()
    {
        for (int i = 0; i < StateWords - MiddleWord; i++)
        {
            uint y = (_state[i] & UpperMask) | (_state[i + 1] & LowerMask);
            _state[i] = _state[i + MiddleWord] ^ (y >> 1) ^ ((0U - (y & 1U)) & MatrixA);
        }

        for (int i = StateWords - MiddleWord; i < StateWords - 1; i++)
        {
            uint y = (_state[i] & UpperMask) | (_state[i + 1] & LowerMask);
            _state[i] = _state[i - WrapOffset] ^ (y >> 1) ^ ((0U - (y & 1U)) & MatrixA);
        }

        uint last = (_state[StateWords - 1] & UpperMask) | (_state[0] & LowerMask);
        _state[StateWords - 1] = _state[MiddleWord - 1] ^ (last >> 1) ^ ((0U - (last & 1U)) & MatrixA);
        _index = 0;
    }

    /// <summary>Seeds the state from operating-system entropy through the array initializer.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InitialiseFromEntropy()
    {
        Span<uint> key = stackalloc uint[SeedKeyWords];
        RandomEntropy.Fill(key);
        InitialiseByKey(key);
    }
}
