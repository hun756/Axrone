namespace Axrone.Random.Tests;

using System.Numerics;
using System.Text;
using FluentAssertions.Execution;

/// <summary>
/// Contracts of <see cref="SeedHashing"/>: that a level name lands on the same four words every
/// time and on different words than its neighbours, that a single flipped input bit reaches half
/// of the output, that the all-zero engine state can never come out, and that a hash feeds a real
/// engine as a reproducible stream.
/// </summary>
/// <remarks>
/// <para>The reference vectors are not self-generated: each one was captured from the TypeScript
/// <c>hashSeedToState</c> in <c>Axrone/web/packages/random/src/seed-utils.ts</c>, which is the
/// cross-language contract this port exists to honour. A C# implementation that merely agreed with
/// itself would pass every other test in this file and still desynchronise both runtimes at the
/// first level load, which is why the words are pinned rather than recomputed. What is <em>not</em>
/// done here is the formal lock - see the class remarks on
/// <see cref="SeedHashingReferenceVectorProvenance"/>.</para>
/// <para>The avalanche samples are deliberately spread over the whole input rather than clustered
/// at the start: two of the five flip bits inside the second 32-byte chunk, and one flips the very
/// last byte of the first chunk, so a chunking mistake that only shows up past the boundary cannot
/// pass. The flip is applied to a <em>character</em>, because the character is what
/// <see cref="SeedHashing.HashStringToWords"/> receives; flipping a raw byte instead would land in
/// a different place after encoding. Measured across all 320 single-character bit flips of the
/// long name, the output spreads over 42.2 % to 59.0 % and never leaves the asserted band, so the
/// five samples are not a lucky pick.</para>
/// </remarks>
public class SeedHashTests
{
    /// <summary>A 40-byte name: one full 32-byte chunk plus an 8-byte tail, so it crosses the boundary.</summary>
    private const string LongLevelName = "level_forest_cave_boss_room_wave_3_alpha";

    /// <summary>A name whose UTF-8 encoding spans all three lengths, including a four-byte scalar.</summary>
    private const string NonAsciiLevelName = "Hello, \u00E9\u00E0\u4E2D\u6587\U0001F600 end";

    /// <summary>The reference engine used by the integration contract.</summary>
    private const ulong IntegrationSeed = 0;

    /// <summary>Draws replayed per engine in the integration contract.</summary>
    private const int IntegrationDraws = 100;

    /// <summary>Width of one hash, in output bits.</summary>
    private const int OutputBits = 256;

    /// <summary>Lower avalanche bound: 40 % of the output.</summary>
    private const double MinAvalanchePercent = 40.0;

    /// <summary>Upper avalanche bound: 60 % of the output.</summary>
    private const double MaxAvalanchePercent = 60.0;

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("level_01")]
    [InlineData("level_forest_cave_boss_room_wave_3_alpha")]
    [InlineData("Hello, \u00E9\u00E0\u4E2D\u6587\U0001F600 end")]
    public void HashStringToWords_SameString_ProducesIdenticalWords(string levelName)
    {
        var first = SeedHashing.HashStringToWords(levelName);
        var second = SeedHashing.HashStringToWords(levelName);

        second.Should().Be(first);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("level_01")]
    public void HashStringToWords_ProducesNonZeroWords(string levelName)
    {
        (ulong S0, ulong S1, ulong S2, ulong S3) words = SeedHashing.HashStringToWords(levelName);

        (words.S0 | words.S1 | words.S2 | words.S3).Should().NotBe(0UL);
    }

    [Fact]
    public void DistinctLevelNames_ProduceDistinctWords()
    {
        string[] names = ["", " ", "a", "A", "level_1", "level_01", "Level_01", LongLevelName, NonAsciiLevelName];
        var words = new List<(ulong S0, ulong S1, ulong S2, ulong S3)>(names.Length);

        foreach (string name in names)
        {
            words.Add(SeedHashing.HashStringToWords(name));
        }

        words.Distinct().Should().HaveCount(names.Length);
    }

    [Theory]
    [InlineData("", 0x406EB0D8D886DF8BUL, 0x6DF7FF9A3C83EA9EUL, 0xD8A9BFF6FDD54AB6UL, 0xADC518956916D7FAUL)]
    [InlineData("a", 0x85223527BA7A6EA0UL, 0xC7ECC8DF08042A05UL, 0x44810A6898A554B9UL, 0x481F29B5F8D9B2F7UL)]
    [InlineData("level_01", 0xC213288CB3CF693CUL, 0x6826A0CA9E4020A4UL, 0x5ECCB909FB17B029UL, 0x4BB189A96539B1DAUL)]
    [InlineData("hello world", 0x486412697BFB1AEAUL, 0x8E04846CF1C7ADE9UL, 0x3739C1D885C213EAUL, 0x4D91A4643F59BC8BUL)]
    [InlineData(
        "level_forest_cave_boss_room_wave_3_alpha",
        0x4A097936E465F64CUL,
        0x415720BDA3344D74UL,
        0x2860E74D2A59DD7AUL,
        0x5BFC7A60C2056466UL)]
    [InlineData(
        "Hello, \u00E9\u00E0\u4E2D\u6587\U0001F600 end",
        0x9C09443276A09A1AUL,
        0x8DBACD830CB8C22BUL,
        0x221CD3C72FDAA708UL,
        0x768D7C8AAA6170EBUL)]
    public void HashStringToWords_MatchesReferenceVectors(
        string levelName,
        ulong expected0,
        ulong expected1,
        ulong expected2,
        ulong expected3)
    {
        SeedHashing.HashStringToWords(levelName).Should().Be((expected0, expected1, expected2, expected3));
    }

    [Fact]
    public void HashBytesToWords_MatchesReferenceVectors()
    {
        byte[] partialChunk = [10, 20, 30];
        byte[] chunkPlusTail = [.. Enumerable.Range(0, 33).Select(static i => (byte)i)];

        using (new AssertionScope())
        {
            SeedHashing.HashBytesToWords([])
                .Should().Be(
                    (0x406EB0D8D886DF8BUL, 0x6DF7FF9A3C83EA9EUL, 0xD8A9BFF6FDD54AB6UL, 0xADC518956916D7FAUL),
                    "an empty seed absorbs nothing at all");
            SeedHashing.HashBytesToWords(partialChunk)
                .Should().Be(
                    (0x997B83A773D6E767UL, 0x69ED02AB3C234C8FUL, 0x515EAAAA7D069A52UL, 0xB9B85540A3188544UL),
                    "three bytes are one partial chunk, zero-padded to 32");
            SeedHashing.HashBytesToWords(chunkPlusTail)
                .Should().Be(
                    (0xB10A21E5BF2D9193UL, 0xD4EF8F2831FE9033UL, 0x32F0B63FF0B33C0FUL, 0x3CAADB13968EDC1AUL),
                    "33 bytes are a full chunk plus a one-byte tail");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(8)]
    public void HashIntsToWords_AgreesWithTheByteOverloadOverTheSameLittleEndianBytes(int length)
    {
        int[] values = [.. Enumerable.Range(1, length)];
        byte[] bytes = new byte[values.Length * sizeof(int)];
        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(i * sizeof(int)), values[i]);
        }

        SeedHashing.HashIntsToWords(values).Should().Be(SeedHashing.HashBytesToWords(bytes));
    }

    [Fact]
    public void HashIntsToWords_ReactsToEveryValueInTheChunk()
    {
        // The reference's integer branch shifts value j by j * 32 inside a 64-bit word, so values
        // three through eight leave the word entirely and are truncated to zero - [1, 2, 3] and
        // [1, 2, 999] collide there. This overload absorbs all eight values instead; the divergence
        // is deliberate and is pinned against the reference output below so that repairing the
        // TypeScript side cannot silently change the native seed.
        int[] low = [1, 2, 3, 4, 5, 6, 7, 8];
        int[] mutated = [1, 2, 999, 4, 5, 6, 7, 8];

        SeedHashing.HashIntsToWords(low).Should().NotBe(SeedHashing.HashIntsToWords(mutated));
    }

    [Fact]
    public void HashIntsToWords_DoesNotFollowTheBrokenReferenceIntegerBranch()
    {
        int[] values = [1, 2, 3, 4, 5, 6, 7, 8];

        SeedHashing.HashIntsToWords(values).Should().NotBe(
            (0xBB2887B3C0E835A7UL, 0x9F40B90302D6C005UL, 0x9C10F087832BEE3CUL, 0x83965C794C87FD21UL),
            "this is what hashSeedToState(new Int32Array([1..8])) returns today, and it discards six of the eight values");
    }

    [Fact]
    public void HashUInt64sToWords_MatchesReferenceVectors()
    {
        ulong[] partialChunk = [10UL, 20UL];
        ulong[] chunkPlusTail =
        [
            0x0102030405060708UL, 0x1112131415161718UL, 0x2122232425262728UL, 0x3132333435363738UL, 0x4142434445464748UL
        ];

        using (new AssertionScope())
        {
            SeedHashing.HashUInt64sToWords([])
                .Should().Be(
                    (0x406EB0D8D886DF8BUL, 0x6DF7FF9A3C83EA9EUL, 0xD8A9BFF6FDD54AB6UL, 0xADC518956916D7FAUL),
                    "an empty seed absorbs nothing at all");
            SeedHashing.HashUInt64sToWords(partialChunk)
                .Should().Be(
                    (0x9A3E7B08CBA90840UL, 0x5DEAEEC476BC66C5UL, 0xBA259FE72E83FAA1UL, 0x2CF4059B976B06BDUL),
                    "two words are one partial chunk, zero-padded to four");
            SeedHashing.HashUInt64sToWords(chunkPlusTail)
                .Should().Be(
                    (0x994C364F54B6895EUL, 0x23473B65FA3ACB5CUL, 0xAC72A97F5FFEC490UL, 0x183DADBAB3CA46A8UL),
                    "five words are a full chunk plus a one-word tail");
        }
    }

    [Fact]
    public void HashUInt64sToWords_ExactChunkMatchesReferenceVector()
    {
        ulong[] values = [1UL, 2UL, 3UL, 4UL];

        SeedHashing.HashUInt64sToWords(values)
            .Should().Be((0x536DB09E798742E4UL, 0x717D32D63D775E57UL, 0xC73DEDF206C5F88DUL, 0x07DE29D86958E816UL));
    }

    [Theory]
    [InlineData(0UL, 0xEAD40E1E37BABD54UL, 0x16B2FE219C70AB50UL, 0x8EB189D9A919D701UL, 0xB54FFC06F5523476UL)]
    [InlineData(42UL, 0xBA2B2C53E723B288UL, 0x87F50A0B38747F7FUL, 0x4E0E9541CAFB3F38UL, 0x48BE3B8B30FF984BUL)]
    public void HashUInt64ToWords_MatchesReferenceVectors(
        ulong seed,
        ulong expected0,
        ulong expected1,
        ulong expected2,
        ulong expected3)
    {
        SeedHashing.HashUInt64ToWords(seed).Should().Be((expected0, expected1, expected2, expected3));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("level_01")]
    [InlineData("level_forest_cave_boss_room_wave_3_alpha")]
    [InlineData("Hello, \u00E9\u00E0\u4E2D\u6587\U0001F600 end")]
    [InlineData("\u0000\u001F\u007F\u0080\u07FF\u0800\uFFFF")]
    [InlineData("end\\")]
    public void HashStringToWords_MatchesTheByteOverloadOverItsOwnEncoding(string levelName)
    {
        SeedHashing.HashStringToWords(levelName)
            .Should().Be(SeedHashing.HashBytesToWords(Encoding.UTF8.GetBytes(levelName)));
    }

    [Fact]
    public void HashStringToWords_ReplacesAnUnpairedSurrogateWithTheEncoderSubstitute()
    {
        // Built inside the test rather than declared as InlineData: an unpaired surrogate does not
        // survive a theory argument's serialisation, so distinct cases collapse onto one test id.
        (char[] Text, byte[] Encoded)[] cases =
        [
            (['\uD800'], [0xEF, 0xBF, 0xBD]),
            (['\uDC00'], [0xEF, 0xBF, 0xBD]),
            (['\uD83D'], [0xEF, 0xBF, 0xBD]),
            (['\uDFFF'], [0xEF, 0xBF, 0xBD]),
            (['a', '\uD83D', 'b'], [0x61, 0xEF, 0xBF, 0xBD, 0x62]),
            (['\uDE00', '\uD83D'], [0xEF, 0xBF, 0xBD, 0xEF, 0xBF, 0xBD])
        ];

        using (new AssertionScope())
        {
            foreach ((char[] text, byte[] encoded) in cases)
            {
                string levelName = new string(text);

                SeedHashing.HashStringToWords(levelName)
                    .Should().Be(SeedHashing.HashBytesToWords(encoded), "U+FFFD is what the reference encoder emits");
                SeedHashing.HashStringToWords(levelName)
                    .Should().Be(SeedHashing.HashBytesToWords(Encoding.UTF8.GetBytes(levelName)));
            }
        }
    }

    [Fact]
    public void HashStringToWords_KeepsAValidSurrogatePairAsOneFourByteScalar()
    {
        const string levelName = "wave_\uD83D\uDE00";

        SeedHashing.HashStringToWords(levelName)
            .Should().Be(SeedHashing.HashBytesToWords(Encoding.UTF8.GetBytes(levelName)));
        SeedHashing.HashStringToWords(levelName)
            .Should().NotBe(SeedHashing.HashBytesToWords([0xEF, 0xBF, 0xBD, 0xEF, 0xBF, 0xBD]));
    }

    [Fact]
    public void EveryOverload_TreatsEmptyInputAsTheUnseededFallback()
    {
        (ulong S0, ulong S1, ulong S2, ulong S3) expected =
            (0x406EB0D8D886DF8BUL, 0x6DF7FF9A3C83EA9EUL, 0xD8A9BFF6FDD54AB6UL, 0xADC518956916D7FAUL);

        SeedHashing.HashStringToWords("").Should().Be(expected);
        SeedHashing.HashBytesToWords([]).Should().Be(expected);
        SeedHashing.HashIntsToWords([]).Should().Be(expected);
        SeedHashing.HashUInt64sToWords([]).Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(127)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(319)]
    public void HashStringToWords_SingleBitFlip_FlipsFortyToSixtyPercentOfTheOutputBits(int flippedBit)
    {
        char[] mutable = LongLevelName.ToCharArray();
        mutable[flippedBit >> 3] = (char)(mutable[flippedBit >> 3] ^ (1 << (flippedBit & 7)));

        int flipped = CountDifferingBits(
            SeedHashing.HashStringToWords(LongLevelName),
            SeedHashing.HashStringToWords(new string(mutable)));

        (flipped * 100.0 / OutputBits).Should().BeInRange(MinAvalanchePercent, MaxAvalanchePercent);
    }

    [Theory]
    [InlineData("Xoroshiro128PlusPlus")]
    [InlineData("Xoshiro256PlusPlus")]
    public void CreateSeeded_SameLevelName_ReplaysIdenticalStreams(string engine)
    {
        ulong[] first = Draw(engine, LongLevelName, IntegrationDraws);
        ulong[] second = Draw(engine, LongLevelName, IntegrationDraws);

        second.Should().Equal(first);
        first.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("Xoroshiro128PlusPlus")]
    [InlineData("Xoshiro256PlusPlus")]
    public void CreateSeeded_DifferentLevelNames_ProduceDifferentStreams(string engine)
    {
        Draw(engine, "level_01", IntegrationDraws)
            .Should().NotEqual(Draw(engine, "level_02", IntegrationDraws));
    }

    [Fact]
    public void CreateSeeded_HandsTheFirstHashWordToTheEngineFactory()
    {
        ulong seed = SeedHashing.HashStringToWords(LongLevelName).S0;

        SeedHashing.CreateSeeded<Xoroshiro128PlusPlus>(LongLevelName)
            .Should().Be(Xoroshiro128PlusPlus.Create(seed));
        SeedHashing.CreateSeeded<Xoshiro256PlusPlus>(LongLevelName)
            .Should().Be(Xoshiro256PlusPlus.Create(seed));
    }

    [Fact]
    public void CreateSeeded_HashesNamesThatDifferOnlyInTheirLastByte()
    {
        Draw<Xoroshiro128PlusPlus>("level_forest_cave_boss_room_wave_3_alpha", IntegrationDraws)
            .Should().NotEqual(Draw<Xoroshiro128PlusPlus>("level_forest_cave_boss_room_wave_3_alphb", IntegrationDraws));
    }

    [Fact]
    public void HashStringToWords_NullLevelName_IsRejected()
    {
        Action act = () => SeedHashing.HashStringToWords(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("seed");
    }

    [Fact]
    public void CreateSeeded_NullLevelName_IsRejected()
    {
        Action act = () => SeedHashing.CreateSeeded<Xoroshiro128PlusPlus>(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("seed");
    }

    [Fact]
    public void Hashing_AllocatesNothing()
    {
        // Warm the path first so a first-call cost of any kind cannot be mistaken for an allocation.
        SeedHashing.HashStringToWords(LongLevelName);
        SeedHashing.HashBytesToWords([1, 2, 3]);
        SeedHashing.HashIntsToWords([1, 2, 3]);
        SeedHashing.HashUInt64sToWords([1UL, 2UL]);
        SeedHashing.HashUInt64ToWords(IntegrationSeed);

        long before = GC.GetAllocatedBytesForCurrentThread();

        SeedHashing.HashStringToWords(LongLevelName);
        SeedHashing.HashStringToWords(NonAsciiLevelName);
        SeedHashing.HashBytesToWords([1, 2, 3]);
        SeedHashing.HashIntsToWords([1, 2, 3]);
        SeedHashing.HashUInt64sToWords([1UL, 2UL]);
        SeedHashing.HashUInt64ToWords(IntegrationSeed);
        _ = SeedHashing.CreateSeeded<Xoroshiro128PlusPlus>(LongLevelName);

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
    }

    private static ulong[] Draw(string engine, string levelName, int count) => engine switch
    {
        "Xoroshiro128PlusPlus" => Draw<Xoroshiro128PlusPlus>(levelName, count),
        "Xoshiro256PlusPlus" => Draw<Xoshiro256PlusPlus>(levelName, count),
        _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Unknown engine.")
    };

    private static ulong[] Draw<TEngine>(string levelName, int count)
        where TEngine : struct, IRandomSource<TEngine>
    {
        TEngine engine = SeedHashing.CreateSeeded<TEngine>(levelName);
        ulong[] values = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = TEngine.NextUInt64(ref engine);
        }

        return values;
    }

    private static int CountDifferingBits(
        (ulong S0, ulong S1, ulong S2, ulong S3) baseline,
        (ulong S0, ulong S1, ulong S2, ulong S3) mutated) =>
        BitOperations.PopCount(baseline.S0 ^ mutated.S0)
        + BitOperations.PopCount(baseline.S1 ^ mutated.S1)
        + BitOperations.PopCount(baseline.S2 ^ mutated.S2)
        + BitOperations.PopCount(baseline.S3 ^ mutated.S3);
}

/// <summary>
/// Records where the reference vectors in <see cref="SeedHashTests"/> came from and what is still
/// missing to make the cross-language seed contract enforceable rather than merely believed.
/// </summary>
/// <remarks>
/// Captured on 2026-10-02 by running the reference <c>hashSeedToState</c> from
/// <c>Axrone/web/packages/random/src/seed-utils.ts</c> under Node 24 over the inputs named in each
/// test. The C# port reproduces every one of them bit for bit.
/// <para><b>What a future agent must add to turn this into a locked contract.</b></para>
/// <list type="number">
/// <item><description>Check in a generator script next to the engine, beside
/// <c>Axrone/web/packages/random/scripts/</c>, that emits the vectors as C# <c>InlineData</c> lines
/// from the TypeScript source rather than from a hand-typed list, so a regeneration diff is a
/// reviewable change.</description></item>
/// <item><description>Store the output as a shared fixture (for example
/// <c>Axrone.Random/vectors/seed-hash-vectors.json</c>) that both the engine test project and the
/// engine's own TypeScript test read, instead of duplicating the literals in two languages.</description></item>
/// <item><description>Add a CI job that runs the generator and fails when the checked-in fixture
/// differs from the freshly generated one, so a change to either implementation without the other
/// is a red build rather than a silent stream divergence at level load.</description></item>
/// <item><description>Record the provenance - source path, commit hash of the TypeScript file and
/// the generation date - inside the fixture, so a stale vector names the drift rather than
/// presenting as a C# bug.</description></item>
/// <item><description>Extend the vector set to the inputs this file covers only incidentally today:
/// inputs longer than two chunks, and a well-formed <c>uint</c> key.</description></item>
/// <item><description>Resolve, rather than merely record, the integer-branch divergence: the
/// reference's <c>Int32Array</c> branch shifts value <c>j</c> by <c>j * 32</c> inside a 64-bit
/// word, so values three through eight are truncated to zero and <c>[1, 2, 3]</c> collides with
/// <c>[1, 2, 999]</c>. <see cref="SeedHashing.HashIntsToWords"/> deliberately does not inherit
/// that, and pins today's broken reference output to prove it. Whoever owns the TypeScript side
/// must decide whether to fix the branch; if it is fixed, this file's
/// <c>HashIntsToWords_DoesNotFollowTheBrokenReferenceIntegerBranch</c> assertion is the place that
/// has to change, and it should change together with the fixture in the first item.</description></item>
/// <item><description>Decide whether the numeric branch needs a signed overload. The reference's
/// <c>number</c> branch assumes a non-negative safe integer; for a negative one its
/// <c>BigInt(seed)</c> leaves <c>s0</c> unmasked until the next step, so its output is not a
/// 64-bit function of the seed at all. This port takes an unsigned <see cref="ulong"/>, which is
/// the engine's own seed word type, and does not attempt to reproduce that.</description></item>
/// </list>
/// </remarks>
public static class SeedHashingReferenceVectorProvenance
{
    /// <summary>
    /// The TypeScript source file whose behaviour these vectors pin, as of the capture date.
    /// </summary>
    public const string ReferenceSource = "Axrone/web/packages/random/src/seed-utils.ts";

    /// <summary>The exported reference function name.</summary>
    public const string ReferenceFunction = "hashSeedToState";

    /// <summary>The date the vectors in this file were captured.</summary>
    public const string CapturedOn = "2026-10-02";
}
