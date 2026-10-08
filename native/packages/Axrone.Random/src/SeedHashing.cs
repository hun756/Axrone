namespace Axrone.Random;

using System.Text;

/// <summary>
/// Deterministic string / byte / integer to engine-seed hashing: a bit-exact port of the engine's
/// TypeScript <c>hashSeedToState</c> seed utility, so a level name fed to either runtime lands
/// on the same 256-bit seed and therefore on the same gameplay stream.
/// </summary>
/// <remarks>
/// <para><b>Where the words go.</b> This is a cold setup cost - once, while a level loads - so it
/// is written for legibility rather than speed: one readable mix loop, no lookup tables, no
/// data-dependent branching. It still allocates nothing. The four words come back in a value
/// tuple, which lives in the caller's stack frame, so no <c>ulong[4]</c> is ever created and the
/// result can be returned from a generic engine factory without a box.</para>
/// <para><b>Absorption.</b> Every overload funnels into the same four-word accumulator, so the
/// overloads differ only in how they widen their input into 32-byte chunks. Bytes are chunked
/// 32 at a time and read as four little-endian 64-bit words; 32-bit integers are chunked eight at
/// a time, which is the same 32 bytes in the same order and is bit-identical to handing those
/// bytes to <see cref="HashBytesToWords"/> - unlike the reference's integer branch, whose shift
/// overflows its own word (see <see cref="HashIntsToWords"/>); 64-bit words are chunked four at a
/// time, which is the reference's own word-per-chunk width. A trailing partial chunk contributes
/// its own bytes and zero for the rest - it is never padded with stale buffer content. Each chunk
/// is xored into the accumulator and followed by one mix round.</para>
/// <para><b>Finishing.</b> Sixteen further mix rounds follow the last chunk, then the all-zero
/// result - the single state every engine in this package treats as <em>unseeded</em> - falls back
/// to the four initial constants, so a hash can never hand a caller a dead stream.</para>
/// <para><b>Bit exactness.</b> Every operation here is <c>unchecked</c> <see cref="ulong"/>
/// arithmetic and every rotate is <see cref="BitOperations"/>, which is exactly what the
/// reference does when it masks a <c>BigInt</c> down to 64 bits after each step. A checked
/// operation, a signed shift or a widening cast introduced later would silently move every seed,
/// so the word-level vectors are pinned by tests rather than by review.</para>
/// <para><b>Strings.</b> <see cref="HashStringToWords"/> encodes UTF-8 by hand into a
/// <c>stackalloc</c> buffer, because <c>Encoding.UTF8.GetBytes</c> would allocate a byte array
/// per call. It reproduces the reference's encoder semantics, not merely its bytes: an unpaired
/// surrogate becomes U+FFFD, because the WHATWG <c>TextEncoder</c> the reference runs on does the
/// same. A multi-byte scalar may straddle a 32-byte chunk boundary; the byte stream is split at
/// 32 bytes, not the scalars, so the straddle lands exactly where the reference puts it.</para>
/// </remarks>
public static class SeedHashing
{
    /// <summary>Absorb chunk width in bytes: four little-endian 64-bit words.</summary>
    private const int ChunkBytes = 32;

    /// <summary>Absorb chunk width in 32-bit integers: the same <see cref="ChunkBytes"/>.</summary>
    private const int ChunkInts = ChunkBytes / sizeof(int);

    /// <summary>Absorb chunk width in 64-bit words.</summary>
    private const int ChunkWords = 4;

    /// <summary>Rounds of the mix applied after the final chunk has been absorbed.</summary>
    private const int FinalRounds = 16;

    /// <summary>Odd-bit mask the reference xors into the second word for a scalar seed.</summary>
    private const ulong ScalarOddMask = 0x5555555555555555UL;

    /// <summary>Largest UTF-8 encoding of a single Unicode scalar.</summary>
    private const int MaxScalarBytes = 4;

    /// <summary>U+FFFD, which the WHATWG encoder substitutes for an unpaired surrogate.</summary>
    private const int ReplacementScalar = 0xFFFD;

    /// <summary>
    /// Hashes a UTF-8 string, the form a level name arrives in, into four seed words.
    /// </summary>
    /// <param name="seed">The text to hash; the empty string is a valid input.</param>
    /// <returns>
    /// The four 64-bit words of the hash, as a stack value tuple. The same text always produces
    /// the same four words.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="seed"/> is <see langword="null"/>.</exception>
    public static (ulong S0, ulong S1, ulong S2, ulong S3) HashStringToWords(string seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        HashState accumulator = new HashState();
        Span<byte> chunk = stackalloc byte[ChunkBytes];
        Span<byte> scalarBytes = stackalloc byte[MaxScalarBytes];
        int filled = 0;

        for (int index = 0; index < seed.Length;)
        {
            Rune scalar = DecodeScalar(seed, index, out int charsUsed);
            index += charsUsed;

            int written = WriteScalar(scalar, scalarBytes);
            for (int i = 0; i < written; i++)
            {
                chunk[filled++] = scalarBytes[i];
                if (filled == ChunkBytes)
                {
                    accumulator.AbsorbBytes(chunk);
                    filled = 0;
                }
            }
        }

        if (filled > 0)
        {
            accumulator.AbsorbBytes(chunk[..filled]);
        }

        return accumulator.Complete();
    }

    /// <summary>
    /// Hashes a byte sequence into four seed words, absorbing 32 bytes per chunk as four
    /// little-endian 64-bit words.
    /// </summary>
    /// <param name="seed">The bytes to hash; an empty sequence is a valid input and hashes like the empty string.</param>
    /// <returns>The four 64-bit words of the hash, as a stack value tuple.</returns>
    public static (ulong S0, ulong S1, ulong S2, ulong S3) HashBytesToWords(ReadOnlySpan<byte> seed)
    {
        HashState accumulator = new HashState();
        for (int offset = 0; offset < seed.Length; offset += ChunkBytes)
        {
            accumulator.AbsorbBytes(seed[offset..Math.Min(offset + ChunkBytes, seed.Length)]);
        }

        return accumulator.Complete();
    }

    /// <summary>
    /// Hashes a 32-bit integer sequence into four seed words, absorbing eight values per chunk as
    /// one 32-byte little-endian block. Negative values contribute their two's-complement bits. A
    /// <see cref="uint"/> key with the same bits hashes identically; reinterpret it with
    /// <c>MemoryMarshal.Cast</c> rather than copying it.
    /// </summary>
    /// <param name="seed">The values to hash; an empty sequence is a valid input.</param>
    /// <returns>The four 64-bit words of the hash, as a stack value tuple.</returns>
    /// <remarks>
    /// <b>This overload deliberately does not reproduce the reference's integer branch.</b> That
    /// branch builds each 64-bit word with a shift of <c>j * 32</c> while the target word is only
    /// 64 bits wide, so from the third value onwards every contribution lands above bit 63 and is
    /// truncated to zero by the mix loop's 64-bit mask. Only the first two values of each
    /// eight-value chunk ever reach the accumulator - six of the eight are silently discarded.
    /// That is a defect in the reference, not a contract worth inheriting: a cross-runtime seed
    /// has to be a function of the whole key, and a defect like this makes <c>[1, 2, 3]</c> and
    /// <c>[1, 2, 999]</c> collide. This overload therefore absorbs all eight values, which makes
    /// it bit-identical to <see cref="HashBytesToWords"/> over the same little-endian bytes, and
    /// the divergence is pinned by
    /// <c>HashIntsToWords_DoesNotFollowTheBrokenReferenceIntegerBranch</c> so it cannot be papered
    /// over silently if the TypeScript side is ever repaired.
    /// </remarks>
    public static (ulong S0, ulong S1, ulong S2, ulong S3) HashIntsToWords(ReadOnlySpan<int> seed)
    {
        HashState accumulator = new HashState();
        for (int offset = 0; offset < seed.Length; offset += ChunkInts)
        {
            accumulator.AbsorbInts(seed[offset..Math.Min(offset + ChunkInts, seed.Length)]);
        }

        return accumulator.Complete();
    }

    /// <summary>
    /// Hashes a 64-bit word sequence into four seed words, absorbing four words per chunk - the
    /// reference's <c>BigInt64Array</c> width, so a chunk is xored word for word.
    /// </summary>
    /// <param name="seed">The words to hash; an empty sequence is a valid input.</param>
    /// <returns>The four 64-bit words of the hash, as a stack value tuple.</returns>
    public static (ulong S0, ulong S1, ulong S2, ulong S3) HashUInt64sToWords(ReadOnlySpan<ulong> seed)
    {
        HashState accumulator = new HashState();
        for (int offset = 0; offset < seed.Length; offset += ChunkWords)
        {
            accumulator.AbsorbWords(seed[offset..Math.Min(offset + ChunkWords, seed.Length)]);
        }

        return accumulator.Complete();
    }

    /// <summary>
    /// Hashes a single scalar seed word into four seed words. The reference folds the value into
    /// the first two accumulator words - the second through an odd-bit mask, which keeps a scalar
    /// seed from ever depending on one word alone - and mixes once.
    /// </summary>
    /// <param name="seed">The scalar seed; the engine's own 64-bit seed word type.</param>
    /// <returns>The four 64-bit words of the hash, as a stack value tuple.</returns>
    public static (ulong S0, ulong S1, ulong S2, ulong S3) HashUInt64ToWords(ulong seed)
    {
        HashState accumulator = new HashState();
        accumulator.AbsorbScalar(seed);
        return accumulator.Complete();
    }

    /// <summary>
    /// Hashes a level name into a fresh engine state, which is the call gameplay code actually
    /// makes: <c>"cave_boss_wave_3"</c> becomes the same stream on every run and every machine.
    /// </summary>
    /// <typeparam name="TEngine">
    /// The source engine to construct. Only the first hash word is handed to
    /// <see cref="IRandomSource{TSelf}.Create"/>: one word in, and every engine in this package
    /// expands it through <see cref="SplitMix64"/> into however many state words it needs - two for
    /// <c>Xoroshiro128PlusPlus</c>, four for <c>Xoshiro256PlusPlus</c>, one for
    /// <c>SplitMix64</c> itself. A multi-word engine therefore needs no extra help here, and the
    /// remaining three words are the full-strength seed for callers that do want to address the
    /// whole state themselves, such as a lane decomposition or a cross-language comparison.
    /// </typeparam>
    /// <param name="seed">The level name to hash; the empty string is a valid input.</param>
    /// <returns>A fresh, ready-to-draw engine state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="seed"/> is <see langword="null"/>.</exception>
    public static TEngine CreateSeeded<TEngine>(string seed)
        where TEngine : struct, IRandomSource<TEngine> =>
        TEngine.Create(HashStringToWords(seed).S0);

    /// <summary>
    /// Reads the Unicode scalar starting at <paramref name="index"/>, substituting U+FFFD for an
    /// unpaired surrogate. The substitution is not defensive coding: it is what the WHATWG UTF-8
    /// encoder does, and the reference hashes the output of that encoder, so this is the only
    /// behaviour that keeps the two runtimes in step on malformed input.
    /// </summary>
    /// <param name="value">The text being decoded.</param>
    /// <param name="index">Index of the first character of the scalar.</param>
    /// <param name="charsUsed">The number of characters the scalar occupied: one, or two for a pair.</param>
    /// <returns>The decoded scalar.</returns>
    private static Rune DecodeScalar(string value, int index, out int charsUsed)
    {
        char first = value[index];
        if (!char.IsSurrogate(first))
        {
            charsUsed = 1;
            return new Rune(first);
        }

        char second = (index + 1) < value.Length ? value[index + 1] : '\0';
        if (Rune.TryCreate(first, second, out Rune pair))
        {
            charsUsed = 2;
            return pair;
        }

        charsUsed = 1;
        return new Rune(ReplacementScalar);
    }

    /// <summary>
    /// Writes one Unicode scalar as UTF-8, the same encoding the reference produces through the
    /// WHATWG encoder. <see cref="Rune.Value"/> is never a surrogate and never exceeds U+10FFFF, so
    /// the four shapes below are exhaustive and no replacement path exists to be wrong.
    /// </summary>
    /// <param name="scalar">The scalar to encode.</param>
    /// <param name="destination">A buffer of at least <see cref="MaxScalarBytes"/> bytes.</param>
    /// <returns>The number of bytes written: one to four.</returns>
    private static int WriteScalar(Rune scalar, Span<byte> destination)
    {
        int value = scalar.Value;
        if (value <= 0x7F)
        {
            destination[0] = (byte)value;
            return 1;
        }

        if (value <= 0x7FF)
        {
            destination[0] = (byte)(0xC0 | (value >> 6));
            destination[1] = (byte)(0x80 | (value & 0x3F));
            return 2;
        }

        if (value <= 0xFFFF)
        {
            destination[0] = (byte)(0xE0 | (value >> 12));
            destination[1] = (byte)(0x80 | ((value >> 6) & 0x3F));
            destination[2] = (byte)(0x80 | (value & 0x3F));
            return 3;
        }

        destination[0] = (byte)(0xF0 | (value >> 18));
        destination[1] = (byte)(0x80 | ((value >> 12) & 0x3F));
        destination[2] = (byte)(0x80 | ((value >> 6) & 0x3F));
        destination[3] = (byte)(0x80 | (value & 0x3F));
        return 4;
    }

    /// <summary>
    /// Reads chunk word <paramref name="wordIndex"/> out of a byte chunk as a little-endian
    /// 64-bit word. Bytes the chunk does not reach read as zero, which is how the reference treats
    /// a trailing partial chunk.
    /// </summary>
    private static ulong ReadWord(ReadOnlySpan<byte> chunk, int wordIndex)
    {
        unchecked
        {
            ulong word = 0UL;
            int start = wordIndex * sizeof(ulong);
            int available = Math.Clamp(chunk.Length - start, 0, sizeof(ulong));
            for (int i = 0; i < available; i++)
            {
                word |= (ulong)chunk[start + i] << (8 * i);
            }

            return word;
        }
    }

    /// <summary>
    /// Reads chunk word <paramref name="wordIndex"/> out of a 32-bit integer chunk: two values per
    /// word, low value in the low half, each taken as its raw 32 bits.
    /// </summary>
    private static ulong ReadWord(ReadOnlySpan<int> chunk, int wordIndex)
    {
        unchecked
        {
            ulong word = 0UL;
            int first = wordIndex * 2;
            if ((uint)first < (uint)chunk.Length)
            {
                word |= (uint)chunk[first];
            }

            if ((uint)(first + 1) < (uint)chunk.Length)
            {
                word |= (ulong)(uint)chunk[first + 1] << 32;
            }

            return word;
        }
    }

    /// <summary>Reads chunk word <paramref name="index"/>, or zero where the chunk falls short.</summary>
    private static ulong ReadWord(ReadOnlySpan<ulong> chunk, int index) =>
        (uint)index < (uint)chunk.Length ? chunk[index] : 0UL;

    /// <summary>
    /// The four accumulator words of the reference hash, in the reference's own order. Every
    /// overload starts from the same constants, so the input is absorbed as a sequence of
    /// <c>(a, b, c, d)</c> quadruples that are xored in and mixed.
    /// </summary>
    private struct HashState
    {
        /// <summary>First initial constant, also the all-zero fallback value.</summary>
        private const ulong Init0 = 0x6A09E667F3BCC908UL;

        /// <summary>Second initial constant, also the all-zero fallback value.</summary>
        private const ulong Init1 = 0xBB67AE8584CAA73BUL;

        /// <summary>Third initial constant, also the all-zero fallback value.</summary>
        private const ulong Init2 = 0x3C6EF372FE94F82BUL;

        /// <summary>Fourth initial constant, also the all-zero fallback value.</summary>
        private const ulong Init3 = 0xA54FF53A5F1D36F1UL;

        private ulong _s0;
        private ulong _s1;
        private ulong _s2;
        private ulong _s3;

        /// <summary>Starts a hash at the four initial constants.</summary>
        public HashState()
        {
            _s0 = Init0;
            _s1 = Init1;
            _s2 = Init2;
            _s3 = Init3;
        }

        /// <summary>
        /// Xors one byte chunk - up to 32 bytes - into the accumulator and mixes once.
        /// </summary>
        /// <param name="chunk">The bytes to absorb; a partial chunk reads as zero-padded to 32 bytes.</param>
        internal void AbsorbBytes(ReadOnlySpan<byte> chunk)
        {
            _s0 ^= ReadWord(chunk, 0);
            _s1 ^= ReadWord(chunk, 1);
            _s2 ^= ReadWord(chunk, 2);
            _s3 ^= ReadWord(chunk, 3);
            Mix();
        }

        /// <summary>
        /// Xors one 32-bit integer chunk - up to eight values - into the accumulator and mixes once.
        /// </summary>
        /// <param name="chunk">The values to absorb; a partial chunk reads as zero-padded to eight values.</param>
        internal void AbsorbInts(ReadOnlySpan<int> chunk)
        {
            _s0 ^= ReadWord(chunk, 0);
            _s1 ^= ReadWord(chunk, 1);
            _s2 ^= ReadWord(chunk, 2);
            _s3 ^= ReadWord(chunk, 3);
            Mix();
        }

        /// <summary>
        /// Xors one 64-bit word chunk - up to four words - into the accumulator and mixes once.
        /// </summary>
        /// <param name="chunk">The words to absorb; a partial chunk reads as zero-padded to four words.</param>
        internal void AbsorbWords(ReadOnlySpan<ulong> chunk)
        {
            _s0 ^= ReadWord(chunk, 0);
            _s1 ^= ReadWord(chunk, 1);
            _s2 ^= ReadWord(chunk, 2);
            _s3 ^= ReadWord(chunk, 3);
            Mix();
        }

        /// <summary>
        /// Xors one scalar seed into the first two words and mixes once.
        /// </summary>
        /// <param name="value">The scalar seed.</param>
        internal void AbsorbScalar(ulong value)
        {
            _s0 ^= value;
            _s1 ^= value ^ ScalarOddMask;
            Mix();
        }

        /// <summary>
        /// Runs the sixteen closing mix rounds and substitutes the initial constants for an
        /// all-zero result.
        /// </summary>
        /// <returns>The four hashed words.</returns>
        internal (ulong S0, ulong S1, ulong S2, ulong S3) Complete()
        {
            for (int round = 0; round < FinalRounds; round++)
            {
                Mix();
            }

            if ((_s0 | _s1 | _s2 | _s3) == 0UL)
            {
                _s0 = Init0;
                _s1 = Init1;
                _s2 = Init2;
                _s3 = Init3;
            }

            return (_s0, _s1, _s2, _s3);
        }

        /// <summary>
        /// One round of the reference mix: three rotations fold the four words together, then the
        /// words are xored into each other and <c>s3</c> is rotated once more. The order is the
        /// reference's and is load-bearing - the final <c>s3</c> rotation reads a word that three
        /// of the lines above have already changed.
        /// </summary>
        private void Mix()
        {
            unchecked
            {
                _s0 ^= _s1 ^ _s2 ^ _s3;
                _s1 = BitOperations.RotateLeft(_s1, 11);
                _s2 = BitOperations.RotateLeft(_s2, 23);
                _s3 = BitOperations.RotateLeft(_s3, 7);
                ulong shifted = _s1 << 29;
                _s2 ^= _s0;
                _s3 ^= _s1;
                _s1 ^= _s2;
                _s0 ^= _s3;
                _s2 ^= shifted;
                _s3 = BitOperations.RotateLeft(_s3, 25);
            }
        }
    }
}
