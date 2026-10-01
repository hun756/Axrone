using Axrone.Utility.Backoff.SpinPolicies;
using Axrone.Utility.Internal;

namespace Axrone.Random;

/// <summary>
/// Process-wide, thread-safe pseudo-random source built from
/// <see cref="RandomConfig.StripedSlotCount"/> cache-line padded stripes, each holding its
/// own xoshiro256++ stream.
/// </summary>
/// <remarks>
/// <para><b>Why stripes.</b> A single shared engine would serialise every draw behind one
/// cache line. Striping gives each drawing thread a private state word: a draw is a single
/// interlocked acquire on a 0/1 gate, one engine step, and a release. Uncontended stripes
/// never bounce a cache line, and two threads that land on the same stripe only collide for
/// the few nanoseconds one draw takes.</para>
/// <para><b>Padding rationale.</b> Each slot is followed by 128 bytes of filler, so the stride
/// is the payload plus 128 bytes: consecutive slots never share a cache line - not even a
/// 128-byte one - no matter where the array lands in memory. Without the filler, two threads
/// drawing from neighbouring stripes would invalidate each other's state on every draw, and
/// false sharing turns a lock-free design back into a cache-line ping-pong. The filler is a
/// fixed 128 bytes rather than a computed size so it cannot silently go stale when an engine
/// changes its field layout.</para>
/// <para><b>Reproducibility.</b> Draw order across threads is not reproducible: which stripe
/// a thread lands on depends on its managed thread id and on who wins the gate. The instance
/// is for liveness and quality, not replay. Use <see cref="Random.Create(ulong)"/> when a
/// sequence must be replayable.</para>
/// <para><b>Lifecycle.</b> <see cref="Complete"/> refuses new draws and is idempotent;
/// in-flight draws finish. <see cref="DrainAsync"/> completes and then waits for the
/// in-flight draws; <see cref="Dispose"/> and <see cref="DisposeAsync"/> do the same and then
/// stop accepting calls for good. After <see cref="Complete"/> or disposal every draw throws
/// <see cref="ObjectDisposedException"/>.</para>
/// </remarks>
public sealed class ConcurrentRandom : IDisposable, IAsyncDisposable
{
    private const int LifecycleRunning = 0;
    private const int LifecycleCompleted = 1;
    private const int LifecycleDisposed = 2;

    /// <summary>Iteration count after which <see cref="AdaptiveSpinBackoff"/> escalates to <see cref="Thread.Sleep(int)"/>.</summary>
    private const int AsyncHandoffThreshold = 30;

    /// <summary>Bounded spin budget of the synchronous dispose path, in backoff iterations.</summary>
    private const int DisposeSpinBudget = 64;

    private const ulong GoldenRatio = 0x9E3779B97F4A7C15UL;

    private readonly Slot[] _slots;
    private readonly int _slotMask;
    private int _lifecycle;
    private int _activeDraws;

    /// <summary>
    /// Initialises a striped source with the default configuration and an entropy-derived seed.
    /// </summary>
    public ConcurrentRandom()
        : this(RandomConfig.Default, CreateEntropySeed())
    {
    }

    /// <summary>
    /// Initialises a striped source with the default configuration and an explicit root seed.
    /// Every stripe derives its own seed from the root, so the whole instance is reproducible
    /// for a single-threaded consumer.
    /// </summary>
    /// <param name="seed">The root seed; identical seeds produce identical stripe states.</param>
    public ConcurrentRandom(ulong seed)
        : this(RandomConfig.Default, seed)
    {
    }

    /// <summary>
    /// Initialises a striped source with an explicit configuration and an entropy-derived seed.
    /// </summary>
    /// <param name="config">The tuning knobs; a <see langword="default"/> value is the documented default.</param>
    public ConcurrentRandom(RandomConfig config)
        : this(config, CreateEntropySeed())
    {
    }

    /// <summary>
    /// Initialises a striped source with an explicit configuration and root seed.
    /// </summary>
    /// <param name="config">The tuning knobs; a <see langword="default"/> value is the documented default.</param>
    /// <param name="seed">The root seed; identical seeds produce identical stripe states.</param>
    public ConcurrentRandom(RandomConfig config, ulong seed)
    {
        int slotCount = config.StripedSlotCount;
        Config = config;
        _slotMask = slotCount - 1;
        _slots = new Slot[slotCount];
        _lifecycle = LifecycleRunning;

        for (int i = 0; i < slotCount; i++)
        {
            // Each stripe is seeded through SplitMix64 from the root, so neighbouring stripes
            // never start from related states.
            SplitMix64 mixer = SplitMix64.Create(seed ^ (unchecked((ulong)i * GoldenRatio)));
            _slots[i] = new Slot(new RandomEngine<Xoshiro256PlusPlus>(Xoshiro256PlusPlus.Create(SplitMix64.NextUInt64(ref mixer))));
        }
    }

    /// <summary>Gets the tuning knobs this instance was created with, after getter-side normalisation.</summary>
    public RandomConfig Config { get; }

    /// <summary>Gets the number of cache-line padded stripes.</summary>
    public int SlotCount => _slots.Length;

    /// <summary>Gets a value indicating whether the instance still accepts draws.</summary>
    public bool IsRunning => Volatile.Read(ref _lifecycle) == LifecycleRunning;

    /// <summary>Draws the next raw 64-bit word.</summary>
    /// <returns>A full-width pseudo-random word.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public ulong NextUInt64()
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextUInt64();
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws the next raw 32-bit word.</summary>
    /// <returns>A full-width pseudo-random word.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public uint NextUInt32()
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextUInt32();
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws a 32-bit value in <c>[0, max)</c>.</summary>
    /// <param name="max">The exclusive upper bound; zero yields zero.</param>
    /// <returns>A value strictly below <paramref name="max"/>.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public uint NextUInt32(uint max)
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextUInt32(max);
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws a 64-bit value in <c>[0, max)</c>.</summary>
    /// <param name="max">The exclusive upper bound; zero yields zero.</param>
    /// <returns>A value strictly below <paramref name="max"/>.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public ulong NextUInt64(ulong max)
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextUInt64(max);
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws a 32-bit signed value.</summary>
    /// <returns>A value in the full <see cref="int"/> domain.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public int NextInt32()
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextInt32();
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws a 32-bit signed value in <c>[0, max)</c>.</summary>
    /// <param name="max">The exclusive upper bound; zero yields zero.</param>
    /// <returns>A value in <c>[0, max)</c>.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is negative.</exception>
    public int NextInt32(int max)
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextInt32(max);
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws a 32-bit signed value in <c>[min, max)</c>.</summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A value in <c>[min, max)</c>.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> is greater than <paramref name="max"/>.</exception>
    public int NextInt32(int min, int max)
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextInt32(min, max);
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws a 64-bit signed value.</summary>
    /// <returns>A value in the full <see cref="long"/> domain.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public long NextInt64()
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextInt64();
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws a 64-bit signed value in <c>[0, max)</c>.</summary>
    /// <param name="max">The exclusive upper bound; zero yields zero.</param>
    /// <returns>A value in <c>[0, max)</c>.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is negative.</exception>
    public long NextInt64(long max)
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextInt64(max);
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws a 64-bit signed value in <c>[min, max)</c>.</summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A value in <c>[min, max)</c>.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> is greater than <paramref name="max"/>.</exception>
    public long NextInt64(long min, long max)
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextInt64(min, max);
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws a single-precision value in <c>[0, 1)</c>.</summary>
    /// <returns>A value in <c>[0, 1)</c>; the value 1 is never produced.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public float NextSingle()
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextSingle();
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws a single-precision value in <c>[min, max)</c>.</summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A value in <c>[min, max)</c>; the upper bound is closed by stepping one ULP down.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public float NextSingle(float min, float max)
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextSingle(min, max);
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws a double-precision value in <c>[0, 1)</c>.</summary>
    /// <returns>A value in <c>[0, 1)</c>; the value 1 is never produced.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public double NextDouble()
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextDouble();
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws a double-precision value in <c>[min, max)</c>.</summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <returns>A value in <c>[min, max)</c>; the upper bound is closed by stepping one ULP down.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public double NextDouble(double min, double max)
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextDouble(min, max);
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws a boolean with equal odds.</summary>
    /// <returns><see langword="true"/> or <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public bool NextBool()
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextBool();
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws a boolean that is <see langword="true"/> with the requested probability.</summary>
    /// <param name="probability">The probability of <see langword="true"/>, in <c>[0, 1]</c>.</param>
    /// <returns><see langword="true"/> with the requested probability.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public bool NextBool(double probability)
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextBool(probability);
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws a normally distributed double.</summary>
    /// <param name="mean">The distribution mean.</param>
    /// <param name="stdDev">The distribution standard deviation.</param>
    /// <returns>A normally distributed deviate.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public double NextNormal(double mean = 0.0, double stdDev = 1.0)
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextNormal(mean, stdDev);
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Draws an exponentially distributed double.</summary>
    /// <param name="lambda">The rate parameter; must be finite and positive.</param>
    /// <returns>An exponentially distributed deviate.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public double NextExponential(double lambda = 1.0)
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.NextExponential(lambda);
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Fills the destination with pseudo-random bytes.</summary>
    /// <param name="destination">The span to overwrite; an empty span is a no-op.</param>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public void NextBytes(Span<byte> destination)
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            slot.Engine.NextBytes(destination);
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Fills the destination with pseudo-random bytes.</summary>
    /// <param name="destination">The array to overwrite; an empty array is a no-op.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public void NextBytes(byte[] destination)
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            slot.Engine.NextBytes(destination);
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Shuffles the span in place with the Fisher-Yates algorithm.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="destination">The span to permute; spans shorter than two are a no-op.</param>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public void Shuffle<T>(Span<T> destination)
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            slot.Engine.Shuffle(destination);
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>Selects one element uniformly from the sequence.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The non-empty source sequence.</param>
    /// <returns>The selected element.</returns>
    /// <exception cref="ArgumentException"><paramref name="items"/> is empty.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been completed or disposed.</exception>
    public T Choice<T>(ReadOnlySpan<T> items)
    {
        ref Slot slot = ref AcquireSlot();
        try
        {
            return slot.Engine.Choice(items);
        }
        finally
        {
            ReleaseSlot(ref slot);
        }
    }

    /// <summary>
    /// Refuses new draws. Idempotent, and inert once the instance has been disposed. Draws
    /// that already hold a stripe finish normally.
    /// </summary>
    public void Complete() => Interlocked.CompareExchange(ref _lifecycle, LifecycleCompleted, LifecycleRunning);

    /// <summary>
    /// Completes the instance and waits until every in-flight draw has released its stripe.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait; the instance stays completed.</param>
    /// <returns>A task that completes once no draw is in flight.</returns>
    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        Complete();

        if (Volatile.Read(ref _activeDraws) == 0)
        {
            return;
        }

        AdaptiveSpinBackoff.Initialize(out int backoffState);
        while (Volatile.Read(ref _activeDraws) != 0)
        {
            AdaptiveSpinBackoff.Advance(ref backoffState);

            if (backoffState < AsyncHandoffThreshold)
            {
                continue;
            }

            // The policy has escalated to Thread.Sleep(1): hand the wait back to the pool so a
            // long-lived holder cannot pin this thread, then restart the escalation.
            AdaptiveSpinBackoff.Reset(ref backoffState);
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    /// <summary>
    /// Completes the instance, waits a bounded spin budget for the in-flight draws and stops
    /// accepting calls for good. Idempotent.
    /// </summary>
    /// <remarks>
    /// The synchronous dispose never blocks indefinitely: a draw that is already inside the
    /// gate is allowed to finish, and if it has not, teardown proceeds anyway - the stripe
    /// state belongs to this instance, which the drawing thread keeps alive by its own
    /// reference for the duration of the call.
    /// </remarks>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Interlocked.Exchange(ref _lifecycle, LifecycleDisposed);

        AdaptiveSpinBackoff.Initialize(out int backoffState);
        for (int attempt = 0; attempt < DisposeSpinBudget && Volatile.Read(ref _activeDraws) != 0; attempt++)
        {
            AdaptiveSpinBackoff.Advance(ref backoffState);
        }
    }

    /// <summary>Completes the instance, drains the in-flight draws asynchronously, then disposes.</summary>
    /// <returns>A task that completes once no draw is in flight and the instance is disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        await DrainAsync().ConfigureAwait(false);
        Dispose();
    }

    /// <summary>
    /// Acquires the calling thread's stripe. The index is a Fibonacci mix of the managed
    /// thread id, so neighbouring thread ids do not pile onto the same stripe.
    /// </summary>
    /// <remarks>
    /// No inlining hint: the method contains a retry loop, so the JIT would refuse to inline it
    /// anyway. The hot path it serves - the engine step between acquire and release - is the
    /// <see cref="RandomEngine{TEngine}"/> draw, which is inlined into its callers.
    /// </remarks>
    private ref Slot AcquireSlot()
    {
        if (Volatile.Read(ref _lifecycle) != LifecycleRunning)
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(ConcurrentRandom));
        }

        int index = (int)((unchecked((uint)Environment.CurrentManagedThreadId * 2654435761u)) & (uint)_slotMask);
        ref Slot slot = ref _slots[index];

        AdaptiveSpinBackoff.Initialize(out int backoffState);
        while (Interlocked.CompareExchange(ref slot.Gate, 1, 0) != 0)
        {
            // Another thread is inside this stripe: back off instead of hammering the line.
            AdaptiveSpinBackoff.Advance(ref backoffState);
        }

        // Counted only after the gate is held, so a drain never returns while a stripe is busy.
        Interlocked.Increment(ref _activeDraws);
        return ref slot;
    }

    /// <summary>
    /// Releases the stripe. The gate opens before the in-flight counter is decremented, so a
    /// zeroed counter implies every gate is already free.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void ReleaseSlot(ref Slot slot)
    {
        Volatile.Write(ref slot.Gate, 0);
        Interlocked.Decrement(ref _activeDraws);
    }

    private static ulong CreateEntropySeed()
    {
        SplitMix64 mixer = SplitMix64.Create(
            unchecked((ulong)Environment.TickCount64)
            ^ (unchecked((ulong)Environment.CurrentManagedThreadId) << 32));
        return SplitMix64.NextUInt64(ref mixer);
    }

    /// <summary>
    /// One cache-line padded stripe: a 0/1 acquisition gate and a private engine state,
    /// followed by 128 bytes of filler.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Slot
    {
        public int Gate;
        public RandomEngine<Xoshiro256PlusPlus> Engine;

        // Padding rationale: adjacent slots must never share a cache line, otherwise two
        // threads drawing from neighbouring stripes invalidate each other's state on every
        // draw. Sixteen longs put the stride at "payload + 128 bytes", which is independent of
        // the engine's field layout and cannot go stale when an engine is revised.
        public long Pad00;
        public long Pad01;
        public long Pad02;
        public long Pad03;
        public long Pad04;
        public long Pad05;
        public long Pad06;
        public long Pad07;
        public long Pad08;
        public long Pad09;
        public long Pad10;
        public long Pad11;
        public long Pad12;
        public long Pad13;
        public long Pad14;
        public long Pad15;

        public Slot(RandomEngine<Xoshiro256PlusPlus> engine)
        {
            Gate = 0;
            Engine = engine;
            Pad00 = 0;
            Pad01 = 0;
            Pad02 = 0;
            Pad03 = 0;
            Pad04 = 0;
            Pad05 = 0;
            Pad06 = 0;
            Pad07 = 0;
            Pad08 = 0;
            Pad09 = 0;
            Pad10 = 0;
            Pad11 = 0;
            Pad12 = 0;
            Pad13 = 0;
            Pad14 = 0;
            Pad15 = 0;
        }
    }
}
