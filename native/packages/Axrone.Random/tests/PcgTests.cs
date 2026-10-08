namespace Axrone.Random.Tests;

using System.Numerics;

/// <summary>
/// Contracts of <see cref="PcgEngine"/>: the published reference vector that pins the canonical
/// 64-bit-state form, the seeding selector, the O(log n) jump-ahead, the unseeded default state
/// and the value identity of the two-word state.
/// </summary>
/// <remarks>
/// The vector test is the whole point of this engine: PCG-XSH-RR 64/32 is the only stream in the
/// suite whose words can be checked against an external implementation, so it is checked against
/// the reference demo's published output for <c>pcg_setseq_64_xsh_rr_32(42, 54)</c> word for word.
/// A 32-bit-word emulation of the same algorithm cannot pass this test, which is the point.
/// </remarks>
public class PcgTests
{
    /// <summary>The reference seed of the published pcg-c demo vectors.</summary>
    private const ulong ReferenceSeed = 42UL;

    /// <summary>The reference stream sequence of the published pcg-c demo vectors.</summary>
    private const ulong ReferenceSequence = 54UL;

    /// <summary>
    /// The published <c>pcg_setseq_64_xsh_rr_32(42, 54)</c> output: the six words the reference
    /// demo prints for the canonical 64-bit-state generator.
    /// </summary>
    private static readonly uint[] s_referencePcg =
    [
        0xA15C02B7u, 0x7B47F409u, 0xBA1D3330u, 0x83D2F293u, 0xBFA4784Bu, 0xCBED606Eu
    ];

    private const ulong DeterminismSeed = 0x0123456789ABCDEFUL;
    private const int DeterminismDraws = 100;
    private const ulong LaneSize = 1UL << 20;

    public static TheoryData<ulong> JumpDistances
    {
        get
        {
            TheoryData<ulong> data = new TheoryData<ulong>();
            foreach (ulong distance in (ulong[])[1UL, 2UL, 3UL, 7UL, 63UL, 64UL, 65UL, 1000UL, 65537UL])
            {
                data.Add(distance);
            }

            return data;
        }
    }

    [Fact]
    public void Constructor_ReferenceSeedAndSequence_MatchesPublishedPcgVectors()
    {
        var engine = new PcgEngine(ReferenceSeed, ReferenceSequence);

        for (int i = 0; i < s_referencePcg.Length; i++)
        {
            PcgEngine.NextUInt32(ref engine)
                .Should()
                .Be(s_referencePcg[i], $"draw {i} must match the published pcg-c vector");
        }
    }

    [Fact]
    public void Constructor_ReferenceSeedAndSequence_HasOddStreamIncrement()
    {
        var engine = new PcgEngine(ReferenceSeed, ReferenceSequence);

        Span<ulong> snapshot = stackalloc ulong[PcgEngine.WordCount];
        engine.CopyStateTo(snapshot);

        snapshot[1].Should().Be((ReferenceSequence << 1) | 1UL);
    }

    [Fact]
    public void Constructor_SeedsThroughZeroStateAndTwoDiscardedDraws()
    {
        // The reference seeding order, re-derived independently of the engine: zero state, odd
        // increment, advance, fold initstate, advance. Any reordering of these four steps changes
        // the stream, which is why the check is written out instead of trusted.
        var engine = new PcgEngine(ReferenceSeed, ReferenceSequence);

        ulong state = 0UL;
        ulong increment = (ReferenceSequence << 1) | 1UL;
        Advance(ref state, increment);
        state = unchecked(state + ReferenceSeed);
        Advance(ref state, increment);

        Span<ulong> snapshot = stackalloc ulong[PcgEngine.WordCount];
        engine.CopyStateTo(snapshot);

        snapshot[0].Should().Be(state);
        snapshot[1].Should().Be(increment);
    }

    [Fact]
    public void Create_UsesTheDocumentedStreamSelector()
    {
        var created = PcgEngine.Create(DeterminismSeed);
        var explicitSelector = new PcgEngine(DeterminismSeed, DeterminismSeed ^ PcgEngine.StreamSelector);

        Draw(ref created, DeterminismDraws).Should().Equal(Draw(ref explicitSelector, DeterminismDraws));
    }

    [Fact]
    public void Create_SameSeed_ReplaysIdenticalFirstDraws()
    {
        var first = PcgEngine.Create(DeterminismSeed);
        var second = PcgEngine.Create(DeterminismSeed);

        Draw(ref first, DeterminismDraws).Should().Equal(Draw(ref second, DeterminismDraws));
        Draw(ref first, DeterminismDraws).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Create_DifferentSeed_ProducesDifferentStream()
    {
        var first = PcgEngine.Create(DeterminismSeed);
        var other = PcgEngine.Create(DeterminismSeed + 1UL);

        Draw(ref first, DeterminismDraws).Should().NotEqual(Draw(ref other, DeterminismDraws));
    }

    [Fact]
    public void Create_DoesNotShareAStreamWithTheXoshiroFamily()
    {
        var pcg = PcgEngine.Create(DeterminismSeed);
        var xoshiro = Xoshiro256PlusPlus.Create(DeterminismSeed);

        uint[] pcgWords = new uint[DeterminismDraws];
        uint[] xoshiroWords = new uint[DeterminismDraws];
        for (int i = 0; i < DeterminismDraws; i++)
        {
            pcgWords[i] = PcgEngine.NextUInt32(ref pcg);
            xoshiroWords[i] = Xoshiro256PlusPlus.NextUInt32(ref xoshiro);
        }

        pcgWords.Should().NotEqual(xoshiroWords);
    }

    [Fact]
    public void NextUInt64_PairsTwoNarrowDrawsLowWordFirst()
    {
        var wide = new PcgEngine(ReferenceSeed, ReferenceSequence);
        var narrow = new PcgEngine(ReferenceSeed, ReferenceSequence);

        ulong low = PcgEngine.NextUInt32(ref narrow);
        ulong high = PcgEngine.NextUInt32(ref narrow);

        PcgEngine.NextUInt64(ref wide).Should().Be(((ulong)high << 32) | low);
    }

    [Fact]
    public void NextUInt32_UsesOnlySixtyFourBitArithmetic()
    {
        // The XSH-RR output stage is a shift pair and a rotate: re-deriving a draw from the
        // exported state proves the word is the reference one, with no 128-bit intermediate and
        // no widening multiply anywhere in the step.
        var engine = new PcgEngine(ReferenceSeed, ReferenceSequence);

        Span<ulong> snapshot = stackalloc ulong[PcgEngine.WordCount];
        engine.CopyStateTo(snapshot);

        uint expected = (uint)(((snapshot[0] >> 18) ^ snapshot[0]) >> 27);
        expected = BitOperations.RotateRight(expected, (int)(snapshot[0] >> 59));

        PcgEngine.NextUInt32(ref engine).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(JumpDistances))]
    public void JumpAhead_EqualsTheSameNumberOfDraws(ulong distance)
    {
        var drawn = PcgEngine.Create(DeterminismSeed);
        for (ulong i = 0UL; i < distance; i++)
        {
            _ = PcgEngine.NextUInt32(ref drawn);
        }

        var jumped = PcgEngine.Create(DeterminismSeed);
        PcgEngine.JumpAhead(ref jumped, distance);

        (jumped == drawn).Should().BeTrue();
        Draw(ref jumped, 16).Should().Equal(Draw(ref drawn, 16));
    }

    [Theory]
    [MemberData(nameof(JumpDistances))]
    public void JumpAhead_IsAdditive(ulong distance)
    {
        var combined = PcgEngine.Create(DeterminismSeed);
        PcgEngine.JumpAhead(ref combined, distance * 3UL);

        var split = PcgEngine.Create(DeterminismSeed);
        PcgEngine.JumpAhead(ref split, distance);
        PcgEngine.JumpAhead(ref split, distance);
        PcgEngine.JumpAhead(ref split, distance);

        (split == combined).Should().BeTrue();
    }

    [Fact]
    public void JumpAhead_Zero_IsNoOp()
    {
        var engine = PcgEngine.Create(DeterminismSeed);
        PcgEngine before = engine;

        PcgEngine.JumpAhead(ref engine, 0UL);

        (engine == before).Should().BeTrue();
    }

    [Fact]
    public void JumpAhead_DistanceIsTakenModuloThePeriod()
    {
        // The LCG period is 2^64, so 2^63 twice is one full period and must land back on the
        // starting state. This is the property a fixed 2^64 jump table cannot express.
        var engine = PcgEngine.Create(DeterminismSeed);
        PcgEngine before = engine;

        PcgEngine.JumpAhead(ref engine, 1UL << 63);
        PcgEngine.JumpAhead(ref engine, 1UL << 63);

        (engine == before).Should().BeTrue();
    }

    [Fact]
    public void JumpAhead_GivesEveryLaneADisjointStream()
    {
        var lanes = new PcgEngine[4];
        for (int lane = 0; lane < lanes.Length; lane++)
        {
            lanes[lane] = PcgEngine.Create(DeterminismSeed);
            PcgEngine.JumpAhead(ref lanes[lane], (ulong)lane * LaneSize);
        }

        List<uint[]> streams = new List<uint[]>(lanes.Length);
        for (int lane = 0; lane < lanes.Length; lane++)
        {
            uint[] words = new uint[32];
            for (int i = 0; i < words.Length; i++)
            {
                words[i] = PcgEngine.NextUInt32(ref lanes[lane]);
            }

            streams.Add(words);
        }

        for (int i = 0; i < streams.Count; i++)
        {
            for (int j = i + 1; j < streams.Count; j++)
            {
                streams[i].Should().NotEqual(
                    streams[j],
                    $"lane {i} and lane {j} of a {LaneSize}-draw decomposition must not overlap");
            }
        }
    }

    [Fact]
    public void JumpAhead_LeavesTheStreamIncrementAlone()
    {
        var engine = PcgEngine.Create(DeterminismSeed);

        Span<ulong> before = stackalloc ulong[PcgEngine.WordCount];
        engine.CopyStateTo(before);
        PcgEngine.JumpAhead(ref engine, 12345UL);

        Span<ulong> after = stackalloc ulong[PcgEngine.WordCount];
        engine.CopyStateTo(after);

        after[1].Should().Be(before[1]);
        after[0].Should().NotBe(before[0]);
    }

    [Fact]
    public void DefaultState_FirstDraw_IsSeededFromEntropy()
    {
        PcgEngine engine = default;

        uint[] draws = new uint[64];
        for (int i = 0; i < draws.Length; i++)
        {
            draws[i] = PcgEngine.NextUInt32(ref engine);
        }

        draws.Should().NotContain(0u);
        draws.Distinct().Should().HaveCount(draws.Length);
    }

    [Fact]
    public void DefaultState_DrawsAreNotReproducible()
    {
        var first = default(PcgEngine);
        var second = default(PcgEngine);

        Draw(ref first, 16).Should().NotEqual(Draw(ref second, 16));
    }

    [Fact]
    public void WordCount_IsTheStateAndTheIncrement()
    {
        PcgEngine.WordCount.Should().Be(2);

        var engine = PcgEngine.Create(DeterminismSeed);
        ulong[] destination = new ulong[PcgEngine.WordCount + 1];

        engine.CopyStateTo(destination);

        destination[0].Should().NotBe(0UL);
        destination[1].Should().Be(1UL | ((DeterminismSeed ^ PcgEngine.StreamSelector) << 1));
    }

    [Fact]
    public void Engines_CompareByValue()
    {
        var first = PcgEngine.Create(DeterminismSeed);
        var copy = PcgEngine.Create(DeterminismSeed);
        var other = PcgEngine.Create(DeterminismSeed + 1UL);

        (first == copy).Should().BeTrue();
        (first != copy).Should().BeFalse();
        first.Equals((object)copy).Should().BeTrue();
        first.Equals((object)other).Should().BeFalse();
        (first == other).Should().BeFalse();
        (first != other).Should().BeTrue();
        first.GetHashCode().Should().Be(copy.GetHashCode());

        _ = PcgEngine.NextUInt32(ref first);
        (first == copy).Should().BeFalse();
    }

    [Fact]
    public void Equality_DistinguishesStateFromIncrement()
    {
        ulong sequence = DeterminismSeed ^ PcgEngine.StreamSelector;
        var engine = new PcgEngine(DeterminismSeed, sequence);
        var sameStream = new PcgEngine(DeterminismSeed, sequence);
        var shiftedSequence = new PcgEngine(DeterminismSeed, sequence + 1UL);

        Span<ulong> words = stackalloc ulong[PcgEngine.WordCount];
        Span<ulong> sameWords = stackalloc ulong[PcgEngine.WordCount];
        engine.CopyStateTo(words);
        sameStream.CopyStateTo(sameWords);

        // Same constructor arguments, so the state and the increment are both identical.
        words[0].Should().Be(sameWords[0]);
        words[1].Should().Be(sameWords[1]);
        (engine == sameStream).Should().BeTrue();

        // One bit of the sequence changed: the increment is folded into every step, so the state
        // moves too - and equality has to notice, because two engines that are equal would hand
        // out the same words.
        shiftedSequence.CopyStateTo(words);
        words[1].Should().NotBe(sameWords[1]);
        (engine == shiftedSequence).Should().BeFalse();
        Draw(ref engine, 8).Should().NotEqual(Draw(ref shiftedSequence, 8));
    }

    private static void Advance(ref ulong state, ulong increment)
    {
        ulong oldState = state;
        state = unchecked((oldState * PcgEngine.Multiplier) + increment);
    }

    private static uint[] Draw(ref PcgEngine source, int count)
    {
        uint[] values = new uint[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = PcgEngine.NextUInt32(ref source);
        }

        return values;
    }
}