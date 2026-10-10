namespace Axrone.Memory.Arena;

/// <summary>
/// Unified-arena tuning with validated floors: chunk sizes below one page are
/// lifted, and a zero alignment falls back to pointer alignment.
/// </summary>
public readonly record struct ArenaOptions
{
    /// <summary>First chunk size; floored at 4 KiB.</summary>
    public ByteSize InitialChunkSize
    {
        get;
        init => field = value.Value < 4096 ? ByteSize.FromBytes(4096) : value;
    } = ByteSize.FromBytes(65536);

    /// <summary>Chunk size ceiling; floored at 4 KiB.</summary>
    public ByteSize MaxChunkSize
    {
        get;
        init => field = value.Value < 4096 ? ByteSize.FromBytes(4096) : value;
    } = ByteSize.FromBytes(67108864);

    /// <summary>Default grant alignment; zero means pointer alignment.</summary>
    public Alignment DefaultAlignment
    {
        get;
        init => field = value.Value == 0 ? Alignment.PointerAlignment : value;
    } = Alignment.PointerAlignment;

    /// <summary>Whether reset and rewind clear reclaimed memory.</summary>
    public bool ZeroOnReset { get; init; } = false;

    /// <summary>Creates default tuning.</summary>
    public ArenaOptions() { }
}

/// <summary>Builder state before any choice is made.</summary>
public readonly struct UnconfiguredArenaState { }

/// <summary>Builder state after at least one choice is made.</summary>
public readonly struct ConfiguredArenaState { }

/// <summary>
/// Compile-time typestate builder: <c>Build</c> is only reachable after
/// configuration, so a default-tuned arena still passes through validation.
/// </summary>
/// <typeparam name="TPhase">The configuration stage marker.</typeparam>
public readonly struct ArenaBuilder<TPhase> where TPhase : struct
{
    internal readonly ByteSize _initialChunkSize;
    internal readonly ByteSize _maxChunkSize;
    internal readonly Alignment _defaultAlignment;
    internal readonly bool _zeroOnReset;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ArenaBuilder(ByteSize initial, ByteSize max, Alignment alignment, bool zeroOnReset)
    {
        _initialChunkSize = initial;
        _maxChunkSize = max;
        _defaultAlignment = alignment;
        _zeroOnReset = zeroOnReset;
    }

    /// <summary>Starts a builder with default tuning.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArenaBuilder<UnconfiguredArenaState> Create() =>
        new(ByteSize.FromBytes(65536), ByteSize.FromBytes(67108864), Alignment.PointerAlignment, false);

    /// <summary>Overrides the first chunk size.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArenaBuilder<ConfiguredArenaState> WithInitialChunkSize(ByteSize size) =>
        new(size, _maxChunkSize, _defaultAlignment, _zeroOnReset);

    /// <summary>Overrides the chunk size ceiling.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArenaBuilder<ConfiguredArenaState> WithMaxChunkSize(ByteSize size) =>
        new(_initialChunkSize, size, _defaultAlignment, _zeroOnReset);

    /// <summary>Overrides the default grant alignment.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArenaBuilder<ConfiguredArenaState> WithDefaultAlignment(Alignment alignment) =>
        new(_initialChunkSize, _maxChunkSize, alignment, _zeroOnReset);

    /// <summary>Overrides the clear-on-reset behavior.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArenaBuilder<ConfiguredArenaState> WithZeroOnReset(bool zeroOnReset) =>
        new(_initialChunkSize, _maxChunkSize, _defaultAlignment, zeroOnReset);
}

/// <summary>Build entry points enabled once the builder is configured.</summary>
public static class ConfiguredArenaBuilderExtensions
{
    /// <summary>Builds an arena with explicit policies.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Arena<TGrowth, TBackoff, TMetrics> Build<TGrowth, TBackoff, TMetrics>(
        this ArenaBuilder<ConfiguredArenaState> builder)
        where TGrowth : struct, IArenaGrowthPolicy
        where TBackoff : struct, IBackoffPolicy
        where TMetrics : struct, IArenaMetricsSink
    {
        ArenaOptions options = new()
        {
            InitialChunkSize = builder._initialChunkSize,
            MaxChunkSize = builder._maxChunkSize,
            DefaultAlignment = builder._defaultAlignment,
            ZeroOnReset = builder._zeroOnReset
        };
        return new Arena<TGrowth, TBackoff, TMetrics>(options);
    }

    /// <summary>Builds an arena with the default policies.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Arena Build(this ArenaBuilder<ConfiguredArenaState> builder) =>
        new(new ArenaOptions
        {
            InitialChunkSize = builder._initialChunkSize,
            MaxChunkSize = builder._maxChunkSize,
            DefaultAlignment = builder._defaultAlignment,
            ZeroOnReset = builder._zeroOnReset
        });
}
