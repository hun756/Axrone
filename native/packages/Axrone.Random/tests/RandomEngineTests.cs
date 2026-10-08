namespace Axrone.Random.Tests;

using System.Numerics;

/// <summary>
/// Contracts of the random SOURCE engine cores: the determinism every seedable engine must
/// honour, the published reference vectors that pin the two engines whose streams are standard
/// (SplitMix64 and MT19937), the zero-state entropy fallback, the GF(2) jump tables, and the
/// state export guard.
/// </summary>
/// <remarks>
/// The jump tests do not trust the hard-coded jump tables. They rebuild the state transition
/// matrix from the published recurrences, exponentiate it by repeated squaring - 64 times for
/// xoroshiro128++ (2^64), 128 for xoshiro256++ (2^128) - and require the tables to land on the
/// same state. A wrong constant therefore fails here rather than silently splitting a parallel
/// stream across an arbitrary distance.
/// </remarks>
public class RandomEngineTests
{
    private const ulong DeterminismSeed = 0x0123456789ABCDEFUL;
    private const int DeterminismDraws = 100;

    private static readonly string[] s_engineNames =
    [
        "SplitMix64",
        "WyRand",
        "Xoroshiro128PlusPlus",
        "Xoshiro256PlusPlus",
        "MersenneTwister"
    ];

    /// <summary>The published mt19937ar output for <c>init_genrand(5489)</c>.</summary>
    private static readonly uint[] s_referenceMersenneTwister =
    [
        3499211612u, 581869302u, 3890346734u, 3586334585u, 545404204u
    ];

    /// <summary>The published <c>init_by_array({0x123, 0x234, 0x345, 0x456})</c> output.</summary>
    private static readonly uint[] s_referenceArraySeedMersenneTwister =
    [
        1067595299u, 955945823u, 477289528u, 4107218783u, 4228976476u
    ];

    /// <summary>The published SplitMix64 output for seed <c>0</c>.</summary>
    private static readonly ulong[] s_referenceSplitMix64 =
    [
        0xE220A8397B1DCDAFUL, 0x6E789E6AA1B965F4UL, 0x6C45D188009454FUL
    ];

    public static TheoryData<string> EngineNames
    {
        get
        {
            TheoryData<string> data = new TheoryData<string>();
            foreach (string name in s_engineNames)
            {
                data.Add(name);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(EngineNames))]
    public void Create_SameSeed_ReplaysIdenticalFirstDraws(string engine)
    {
        ulong[] first = Draw(engine, DeterminismSeed, DeterminismDraws);
        ulong[] second = Draw(engine, DeterminismSeed, DeterminismDraws);

        first.Should().Equal(second);
        first.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [MemberData(nameof(EngineNames))]
    public void Create_DifferentSeed_ProducesDifferentStream(string engine)
    {
        ulong[] first = Draw(engine, DeterminismSeed, DeterminismDraws);
        ulong[] second = Draw(engine, DeterminismSeed + 1, DeterminismDraws);

        first.Should().NotEqual(second);
    }

    [Fact]
    public void Create_SameSeed_ProducesDifferentStreamsAcrossEngines()
    {
        for (int i = 0; i < s_engineNames.Length; i++)
        {
            for (int j = i + 1; j < s_engineNames.Length; j++)
            {
                Draw(s_engineNames[i], DeterminismSeed, DeterminismDraws)
                    .Should().NotEqual(
                        Draw(s_engineNames[j], DeterminismSeed, DeterminismDraws),
                        $"{s_engineNames[i]} and {s_engineNames[j]} must not share a stream for one seed");
            }
        }
    }

    [Fact]
    public void NextUInt32_TakesHighHalfOfTheSixtyFourBitDraw()
    {
        var wide = new SplitMix64(DeterminismSeed);
        var narrow = new SplitMix64(DeterminismSeed);

        SplitMix64.NextUInt32(ref narrow).Should().Be((uint)(SplitMix64.NextUInt64(ref wide) >> 32));
    }

    [Fact]
    public void SplitMix64_SeedZero_MatchesPublishedVector()
    {
        var engine = new SplitMix64(0UL);

        for (int i = 0; i < s_referenceSplitMix64.Length; i++)
        {
            SplitMix64.NextUInt64(ref engine).Should().Be(s_referenceSplitMix64[i]);
        }
    }

    [Fact]
    public void MersenneTwister_InitGenRand_MatchesPublishedVector()
    {
        var engine = new MersenneTwister(MersenneTwister.ReferenceSeed);

        for (int i = 0; i < s_referenceMersenneTwister.Length; i++)
        {
            MersenneTwister.NextUInt32(ref engine).Should().Be(s_referenceMersenneTwister[i]);
        }
    }

    [Fact]
    public void MersenneTwister_InitByArray_MatchesPublishedVector()
    {
        ReadOnlySpan<uint> key = [0x123u, 0x234u, 0x345u, 0x456u];
        var engine = new MersenneTwister(key);

        for (int i = 0; i < s_referenceArraySeedMersenneTwister.Length; i++)
        {
            MersenneTwister.NextUInt32(ref engine).Should().Be(s_referenceArraySeedMersenneTwister[i]);
        }
    }

    [Fact]
    public void MersenneTwister_EmptyKey_IsRejected()
    {
        Action act = () => _ = new MersenneTwister(ReadOnlySpan<uint>.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("key");
    }

    [Fact]
    public void Xoroshiro128PlusPlus_DefaultState_DoesNotStreamZeros()
    {
        Xoroshiro128PlusPlus engine = default;
        ulong[] draws = Draw(ref engine, 64);

        draws.Should().NotContain(0UL);
        draws.Distinct().Should().HaveCount(draws.Length);
    }

    [Fact]
    public void Xoshiro256PlusPlus_DefaultState_DoesNotStreamZeros()
    {
        Xoshiro256PlusPlus engine = default;
        ulong[] draws = Draw(ref engine, 64);

        draws.Should().NotContain(0UL);
        draws.Distinct().Should().HaveCount(draws.Length);
    }

    [Fact]
    public void WyRand_DefaultState_DoesNotStreamZeros()
    {
        WyRand engine = default;
        ulong[] draws = Draw(ref engine, 64);

        draws.Should().NotContain(0UL);
        draws.Distinct().Should().HaveCount(draws.Length);
    }

    [Fact]
    public void MersenneTwister_DefaultState_IsSeededFromEntropy()
    {
        MersenneTwister engine = default;
        ulong[] draws = Draw(ref engine, 8);

        draws.Should().NotContain(0UL);
        draws.Distinct().Should().HaveCount(draws.Length);
    }

    [Fact]
    public void Jump_MovesXoroshiro128PlusPlusState()
    {
        Xoroshiro128PlusPlus baseline = Xoroshiro128PlusPlus.Create(DeterminismSeed);
        ulong before = Xoroshiro128PlusPlus.NextUInt64(ref baseline);

        Xoroshiro128PlusPlus jumped = Xoroshiro128PlusPlus.Create(DeterminismSeed);
        Xoroshiro128PlusPlus.Jump(ref jumped);

        Xoroshiro128PlusPlus.NextUInt64(ref jumped).Should().NotBe(before);
    }

    [Fact]
    public void Jump_MovesXoshiro256PlusPlusState()
    {
        Xoshiro256PlusPlus baseline = Xoshiro256PlusPlus.Create(DeterminismSeed);
        ulong before = Xoshiro256PlusPlus.NextUInt64(ref baseline);

        Xoshiro256PlusPlus jumped = Xoshiro256PlusPlus.Create(DeterminismSeed);
        Xoshiro256PlusPlus.Jump(ref jumped);

        Xoshiro256PlusPlus.NextUInt64(ref jumped).Should().NotBe(before);
    }

    [Fact]
    public void LongJump_MovesState()
    {
        Xoshiro256PlusPlus baseline = Xoshiro256PlusPlus.Create(DeterminismSeed);
        ulong before = Xoshiro256PlusPlus.NextUInt64(ref baseline);

        Xoshiro256PlusPlus jumped = Xoshiro256PlusPlus.Create(DeterminismSeed);
        Xoshiro256PlusPlus.LongJump(ref jumped);

        Xoshiro256PlusPlus.NextUInt64(ref jumped).Should().NotBe(before);
    }

    [Fact]
    public void Jump_Twice_IsNotJump_Once()
    {
        Xoroshiro128PlusPlus once = Xoroshiro128PlusPlus.Create(DeterminismSeed);
        Xoroshiro128PlusPlus.Jump(ref once);

        Xoroshiro128PlusPlus twice = Xoroshiro128PlusPlus.Create(DeterminismSeed);
        Xoroshiro128PlusPlus.Jump(ref twice);
        Xoroshiro128PlusPlus.Jump(ref twice);

        Draw(ref twice, 16).Should().NotEqual(Draw(ref once, 16));
    }

    [Fact]
    public void LongJump_IsNotJump()
    {
        Xoshiro256PlusPlus jumped = Xoshiro256PlusPlus.Create(DeterminismSeed);
        Xoshiro256PlusPlus.Jump(ref jumped);

        Xoshiro256PlusPlus longJumped = Xoshiro256PlusPlus.Create(DeterminismSeed);
        Xoshiro256PlusPlus.LongJump(ref longJumped);

        Draw(ref longJumped, 16).Should().NotEqual(Draw(ref jumped, 16));
    }

    [Fact]
    public void Xoroshiro128PlusPlus_SeededDraws_MatchIndependentRecurrence()
    {
        ulong[] state = SeedXoroshiro128PlusPlus(DeterminismSeed);
        var engine = new Xoroshiro128PlusPlus(DeterminismSeed);

        for (int i = 0; i < 64; i++)
        {
            Xoroshiro128PlusPlus.NextUInt64(ref engine).Should().Be(DrawNextXoroshiro128PlusPlus(state));
        }
    }

    [Fact]
    public void Xoshiro256PlusPlus_SeededDraws_MatchIndependentRecurrence()
    {
        ulong[] state = SeedXoshiro256PlusPlus(DeterminismSeed);
        var engine = new Xoshiro256PlusPlus(DeterminismSeed);

        for (int i = 0; i < 64; i++)
        {
            Xoshiro256PlusPlus.NextUInt64(ref engine).Should().Be(DrawNextXoshiro256PlusPlus(state));
        }
    }

    [Fact]
    public void WyRand_SeededDraws_MatchIndependentRecurrence()
    {
        ulong state = DeterminismSeed;
        var engine = new WyRand(DeterminismSeed);

        for (int i = 0; i < 64; i++)
        {
            WyRand.NextUInt64(ref engine).Should().Be(DrawNextWyRand(ref state));
        }
    }

    [Fact]
    public void Xoroshiro128PlusPlus_Jump_EqualsTwoPow64Transitions()
    {
        ulong[] transition = PowerTransition(2, StepXoroshiro128PlusPlus, 64);
        ulong[] expected = Apply(transition, SeedXoroshiro128PlusPlus(DeterminismSeed), 2);

        var engine = new Xoroshiro128PlusPlus(DeterminismSeed);
        Xoroshiro128PlusPlus.Jump(ref engine);

        Xoroshiro128PlusPlus.NextUInt64(ref engine).Should().Be(DrawNextXoroshiro128PlusPlus(expected));
    }

    [Fact]
    public void Xoroshiro128PlusPlus_LongJump_EqualsTwoPow96Transitions()
    {
        ulong[] transition = PowerTransition(2, StepXoroshiro128PlusPlus, 96);
        ulong[] expected = Apply(transition, SeedXoroshiro128PlusPlus(DeterminismSeed), 2);

        var engine = new Xoroshiro128PlusPlus(DeterminismSeed);
        Xoroshiro128PlusPlus.LongJump(ref engine);

        Xoroshiro128PlusPlus.NextUInt64(ref engine).Should().Be(DrawNextXoroshiro128PlusPlus(expected));
    }

    [Fact]
    public void Xoshiro256PlusPlus_Jump_EqualsTwoPow128Transitions()
    {
        ulong[] transition = PowerTransition(4, StepXoshiro256PlusPlus, 128);
        ulong[] expected = Apply(transition, SeedXoshiro256PlusPlus(DeterminismSeed), 4);

        var engine = new Xoshiro256PlusPlus(DeterminismSeed);
        Xoshiro256PlusPlus.Jump(ref engine);

        Xoshiro256PlusPlus.NextUInt64(ref engine).Should().Be(DrawNextXoshiro256PlusPlus(expected));
    }

    [Fact]
    public void Xoshiro256PlusPlus_LongJump_EqualsTwoPow192Transitions()
    {
        ulong[] transition = PowerTransition(4, StepXoshiro256PlusPlus, 192);
        ulong[] expected = Apply(transition, SeedXoshiro256PlusPlus(DeterminismSeed), 4);

        var engine = new Xoshiro256PlusPlus(DeterminismSeed);
        Xoshiro256PlusPlus.LongJump(ref engine);

        Xoshiro256PlusPlus.NextUInt64(ref engine).Should().Be(DrawNextXoshiro256PlusPlus(expected));
    }

    [Fact]
    public void MersenneTwister_CopyStateTo_ShortSpan_IsRejected()
    {
        var engine = new MersenneTwister(MersenneTwister.ReferenceSeed);
        uint[] destination = new uint[MersenneTwister.StateWords - 1];

        Action act = () => engine.CopyStateTo(destination);

        act.Should().Throw<ArgumentException>().WithParameterName("destination");
    }

    [Fact]
    public void MersenneTwister_CopyStateTo_ExactSpan_ExportsState()
    {
        var engine = new MersenneTwister(MersenneTwister.ReferenceSeed);
        uint[] destination = new uint[MersenneTwister.StateWords];

        engine.CopyStateTo(destination);

        destination[0].Should().Be(MersenneTwister.ReferenceSeed);
        destination.Distinct().Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Engines_CompareByValue()
    {
        var first = new Xoshiro256PlusPlus(DeterminismSeed);
        var second = new Xoshiro256PlusPlus(DeterminismSeed);
        var other = new Xoshiro256PlusPlus(DeterminismSeed + 1);

        (first == second).Should().BeTrue();
        (first != second).Should().BeFalse();
        first.Equals((object)second).Should().BeTrue();
        first.Equals((object)other).Should().BeFalse();
        (first == other).Should().BeFalse();
        (first != other).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());

        var engine = new MersenneTwister(MersenneTwister.ReferenceSeed);
        var copy = new MersenneTwister(MersenneTwister.ReferenceSeed);
        (engine == copy).Should().BeTrue();
        uint firstDraw = MersenneTwister.NextUInt32(ref engine);
        uint nextDraw = MersenneTwister.NextUInt32(ref engine);
        firstDraw.Should().NotBe(nextDraw);
        (engine != copy).Should().BeTrue();
        (engine == copy).Should().BeFalse();
    }

    private static ulong[] Draw(string engine, ulong seed, int count) => engine switch
    {
        "SplitMix64" => Draw<SplitMix64>(seed, count),
        "WyRand" => Draw<WyRand>(seed, count),
        "Xoroshiro128PlusPlus" => Draw<Xoroshiro128PlusPlus>(seed, count),
        "Xoshiro256PlusPlus" => Draw<Xoshiro256PlusPlus>(seed, count),
        "MersenneTwister" => Draw<MersenneTwister>(seed, count),
        _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unknown engine.")
    };

    private static ulong[] Draw<TEngine>(ulong seed, int count)
        where TEngine : struct, IRandomSource<TEngine>
    {
        TEngine engine = TEngine.Create(seed);
        ulong[] values = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = TEngine.NextUInt64(ref engine);
        }

        return values;
    }

    private static ulong[] Draw(ref Xoroshiro128PlusPlus source, int count)
    {
        ulong[] values = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = Xoroshiro128PlusPlus.NextUInt64(ref source);
        }

        return values;
    }

    private static ulong[] Draw(ref Xoshiro256PlusPlus source, int count)
    {
        ulong[] values = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = Xoshiro256PlusPlus.NextUInt64(ref source);
        }

        return values;
    }

    private static ulong[] Draw(ref WyRand source, int count)
    {
        ulong[] values = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = WyRand.NextUInt64(ref source);
        }

        return values;
    }

    private static ulong[] Draw(ref MersenneTwister source, int count)
    {
        ulong[] values = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = MersenneTwister.NextUInt64(ref source);
        }

        return values;
    }

    // ---- independent GF(2) oracle for the jump tables ----

    private static ulong SplitMix64Reference(ref ulong state)
    {
        state += SplitMix64.Gamma;
        ulong z = state;
        z = (z ^ (z >> 30)) * SplitMix64.Multiplier1;
        z = (z ^ (z >> 27)) * SplitMix64.Multiplier2;
        return z ^ (z >> 31);
    }

    private static ulong[] SeedXoroshiro128PlusPlus(ulong seed)
    {
        ulong state = seed;
        return
        [
            SplitMix64Reference(ref state),
            SplitMix64Reference(ref state)
        ];
    }

    private static ulong[] SeedXoshiro256PlusPlus(ulong seed)
    {
        ulong state = seed;
        return
        [
            SplitMix64Reference(ref state),
            SplitMix64Reference(ref state),
            SplitMix64Reference(ref state),
            SplitMix64Reference(ref state)
        ];
    }

    private static void StepXoroshiro128PlusPlus(ulong[] state)
    {
        ulong s0 = state[0];
        ulong s1 = state[1] ^ s0;
        state[0] = BitOperations.RotateLeft(s0, 49) ^ s1 ^ (s1 << 21);
        state[1] = BitOperations.RotateLeft(s1, 28);
    }

    private static void StepXoshiro256PlusPlus(ulong[] state)
    {
        ulong t = state[1] << 17;
        state[2] ^= state[0];
        state[3] ^= state[1];
        state[1] ^= state[2];
        state[0] ^= state[3];
        state[2] ^= t;
        state[3] = BitOperations.RotateLeft(state[3], 45);
    }

    private static ulong DrawNextXoroshiro128PlusPlus(ulong[] state)
    {
        ulong s0 = state[0];
        ulong s1 = state[1];
        ulong result = BitOperations.RotateLeft(s0 + s1, 17) + s0;
        StepXoroshiro128PlusPlus(state);
        return result;
    }

    private static ulong DrawNextXoshiro256PlusPlus(ulong[] state)
    {
        ulong result = BitOperations.RotateLeft(state[0] + state[3], 23) + state[0];
        StepXoshiro256PlusPlus(state);
        return result;
    }

    private static ulong DrawNextWyRand(ref ulong state)
    {
        state += WyRand.Secret0;
        ulong t = state;
        t = (t ^ (t >> 48)) * WyRand.Secret1;
        t = (t ^ (t >> 48)) * WyRand.Secret1;
        return t ^ (t >> 32);
    }

    /// <summary>Builds the state-transition matrix of a GF(2) generator, row by basis vector.</summary>
    private static ulong[] BuildTransition(int words, Action<ulong[]> step)
    {
        int bits = words * 64;
        ulong[] matrix = new ulong[bits * words];
        for (int bit = 0; bit < bits; bit++)
        {
            ulong[] basis = new ulong[words];
            basis[bit >> 6] = 1UL << (bit & 63);
            step(basis);
            basis.CopyTo(matrix, bit * words);
        }

        return matrix;
    }

    /// <summary>Squares a GF(2) matrix: row <c>i</c> of the square is the xor of the rows its row selects.</summary>
    private static ulong[] Square(ulong[] matrix, int words)
    {
        int bits = words * 64;
        ulong[] result = new ulong[bits * words];
        for (int row = 0; row < bits; row++)
        {
            for (int column = 0; column < bits; column++)
            {
                if (((matrix[(row * words) + (column >> 6)] >> (column & 63)) & 1UL) == 0UL)
                {
                    continue;
                }

                for (int word = 0; word < words; word++)
                {
                    result[(row * words) + word] ^= matrix[(column * words) + word];
                }
            }
        }

        return result;
    }

    /// <summary>Raises a transition matrix to the 2^exponent-th power by repeated squaring.</summary>
    private static ulong[] PowerTransition(int words, Action<ulong[]> step, int exponent)
    {
        ulong[] matrix = BuildTransition(words, step);
        for (int i = 0; i < exponent; i++)
        {
            matrix = Square(matrix, words);
        }

        return matrix;
    }

    /// <summary>Applies a GF(2) matrix to a state vector.</summary>
    private static ulong[] Apply(ulong[] matrix, ulong[] state, int words)
    {
        int bits = words * 64;
        ulong[] result = new ulong[words];
        for (int column = 0; column < bits; column++)
        {
            if (((state[column >> 6] >> (column & 63)) & 1UL) == 0UL)
            {
                continue;
            }

            for (int word = 0; word < words; word++)
            {
                result[word] ^= matrix[(column * words) + word];
            }
        }

        return result;
    }
}
