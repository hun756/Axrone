namespace Axrone.Random;

/// <summary>
/// Tuning knobs shared by the <see cref="RandomEngine{TEngine}"/> wrapper, the
/// static <see cref="Random"/> facade and the striped <see cref="ConcurrentRandom"/>
/// facade.
/// </summary>
/// <remarks>
/// <para>Every knob is normalised on the <em>getter</em>, never in a constructor and
/// never in a setter, so a <see langword="default"/> struct — which bypasses every
/// constructor — behaves exactly like <c>new RandomConfig()</c> instead of
/// degenerating into an all-zero configuration. A zeroed struct is therefore not a
/// hazard: <see cref="MaxHoistableStateBytes"/> reports
/// <see cref="DefaultMaxHoistableStateBytes"/>, <see cref="StripedSlotCount"/> reports
/// <see cref="DefaultStripedSlotCount"/> and <see cref="CacheNormalPair"/> reports
/// <see langword="true"/>.</para>
/// <para>Explicitly constructed values are additionally clamped: the hoist budget is
/// clamped into <c>[0, <see cref="MaximumHoistableStateBytes"/>]</c>, and the stripe
/// count is rounded up to a power of two inside
/// <c>[<see cref="MinimumStripedSlotCount"/>, <see cref="MaximumStripedSlotCount"/>]</c>
/// so the concurrent facade can mask instead of dividing on every draw.</para>
/// <para>The struct is immutable and safe to share; a single value can back any number
/// of engines, threads and stripes.</para>
/// </remarks>
public readonly struct RandomConfig : IEquatable<RandomConfig>
{
    /// <summary>Default hoist budget, in state bytes, used by a <see langword="default"/> config.</summary>
    public const int DefaultMaxHoistableStateBytes = 32;

    /// <summary>Upper bound of the hoist budget. Anything larger would grow the stack frame of a bulk fill for no gain.</summary>
    public const int MaximumHoistableStateBytes = 64;

    /// <summary>Default number of cache-line padded stripes, used by a <see langword="default"/> config.</summary>
    public const int DefaultStripedSlotCount = 16;

    /// <summary>Lower bound of the stripe count.</summary>
    public const int MinimumStripedSlotCount = 1;

    /// <summary>Upper bound of the stripe count.</summary>
    public const int MaximumStripedSlotCount = 4096;

    private const byte CacheNormalPairMask = 1;
    private const byte ConfiguredMask = 2;

    private readonly int _maxHoistableStateBytes;
    private readonly int _stripedSlotCount;
    private readonly byte _flags;

    /// <summary>
    /// Initialises a configuration with every knob at its documented default.
    /// </summary>
    public RandomConfig()
        : this(DefaultMaxHoistableStateBytes, DefaultStripedSlotCount, cacheNormalPair: true)
    {
    }

    /// <summary>
    /// Initialises a configuration with an explicit hoist budget, stripe count and
    /// Box-Muller pair cache state.
    /// </summary>
    /// <param name="maxHoistableStateBytes">
    /// The largest engine state, in bytes, that a bulk fill may copy into a local for
    /// the whole loop. Zero disables hoisting; the value is clamped to
    /// <see cref="MaximumHoistableStateBytes"/>.
    /// </param>
    /// <param name="stripedSlotCount">
    /// The number of stripes <see cref="ConcurrentRandom"/> allocates. Rounded up to a
    /// power of two and clamped to
    /// <c>[<see cref="MinimumStripedSlotCount"/>, <see cref="MaximumStripedSlotCount"/>]</c>.
    /// </param>
    /// <param name="cacheNormalPair">
    /// <see langword="true"/> to retain the second Box-Muller deviate and serve it from
    /// the cache on the next <c>NextNormal</c> call, halving the transcendental cost.
    /// </param>
    public RandomConfig(int maxHoistableStateBytes, int stripedSlotCount, bool cacheNormalPair)
    {
        _maxHoistableStateBytes = maxHoistableStateBytes;
        _stripedSlotCount = stripedSlotCount;
        _flags = (byte)(ConfiguredMask | (cacheNormalPair ? CacheNormalPairMask : (byte)0));
    }

    /// <summary>Gets the documented default configuration.</summary>
    public static RandomConfig Default => default;

    /// <summary>Gets a configuration with every bulk-fill optimisation disabled.</summary>
    public static RandomConfig Minimal => new(0, MinimumStripedSlotCount, cacheNormalPair: false);

    /// <summary>
    /// Gets the largest engine state, in bytes, that a bulk fill may copy into a local
    /// for the whole loop instead of advancing the state through its field.
    /// </summary>
    /// <remarks>
    /// A default struct reports <see cref="DefaultMaxHoistableStateBytes"/>; an explicit
    /// value is clamped into <c>[0, <see cref="MaximumHoistableStateBytes"/>]</c>, where
    /// zero means "never hoist".
    /// </remarks>
    public int MaxHoistableStateBytes =>
        _flags == 0
            ? DefaultMaxHoistableStateBytes
            : Math.Clamp(_maxHoistableStateBytes, 0, MaximumHoistableStateBytes);

    /// <summary>
    /// Gets the number of cache-line padded stripes a <see cref="ConcurrentRandom"/>
    /// allocates, always a power of two.
    /// </summary>
    /// <remarks>
    /// A default struct reports <see cref="DefaultStripedSlotCount"/>. An explicit value
    /// is clamped into <c>[<see cref="MinimumStripedSlotCount"/>,
    /// <see cref="MaximumStripedSlotCount"/>]</c> and rounded up to the next power of two,
    /// so the concurrent facade can mask the stripe index instead of dividing.
    /// </remarks>
    public int StripedSlotCount
    {
        get
        {
            if (_flags == 0)
            {
                return DefaultStripedSlotCount;
            }

            int clamped = Math.Clamp(_stripedSlotCount, MinimumStripedSlotCount, MaximumStripedSlotCount);
            return (int)BitOperations.RoundUpToPowerOf2((uint)clamped);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the second Box-Muller deviate is retained and
    /// served from the cache on the next normal draw. Defaults to <see langword="true"/>
    /// for a <see langword="default"/> config.
    /// </summary>
    public bool CacheNormalPair => _flags == 0 || (_flags & CacheNormalPairMask) != 0;

    /// <summary>Compares two configurations for value equality after getter-side normalisation.</summary>
    /// <param name="other">The configuration to compare with.</param>
    /// <returns><see langword="true"/> when every normalised knob matches.</returns>
    public bool Equals(RandomConfig other) =>
        MaxHoistableStateBytes == other.MaxHoistableStateBytes
        && StripedSlotCount == other.StripedSlotCount
        && CacheNormalPair == other.CacheNormalPair;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RandomConfig other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(MaxHoistableStateBytes, StripedSlotCount, CacheNormalPair);

    /// <summary>Compares two configurations for value equality after getter-side normalisation.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both configurations normalise identically.</returns>
    public static bool operator ==(RandomConfig left, RandomConfig right) => left.Equals(right);

    /// <summary>Compares two configurations for value inequality after getter-side normalisation.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when the normalised knobs differ.</returns>
    public static bool operator !=(RandomConfig left, RandomConfig right) => !left.Equals(right);
}
