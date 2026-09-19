namespace Axrone.Batching.Tests;

/// <summary>
/// Deterministic xorshift64 for reproducible test inputs.
/// </summary>
/// <remarks>
/// Deliberately not <see cref="Random"/>: these tests need a replayable sequence, not a
/// security-grade source, and a local generator keeps CA5394 off a rule that has nothing to do
/// with sorting. Every seed in the suite is pinned so a failure can be reproduced exactly.
/// </remarks>
internal struct SeededRng
{
    private const ulong DefaultSeed = 0x9E3779B97F4A7C15UL;

    private ulong _state;

    public SeededRng(ulong seed) => _state = seed == 0 ? DefaultSeed : seed;

    private ulong NextRaw()
    {
        _state ^= _state << 13;
        _state ^= _state >> 7;
        _state ^= _state << 17;
        return _state;
    }

    /// <summary>Returns the next value in <c>[minInclusive, maxExclusive)</c>.</summary>
    public int Next(int minInclusive, int maxExclusive) =>
        (int)(NextRaw() % (ulong)(maxExclusive - minInclusive)) + minInclusive;

    /// <summary>Returns the next value in <c>[0, exclusiveMax)</c>.</summary>
    public int Next(int exclusiveMax) => Next(0, exclusiveMax);

    /// <summary>Returns the next full-range <see cref="uint"/>, exercising every bit position.</summary>
    public uint NextUInt32() => (uint)NextRaw();
}
