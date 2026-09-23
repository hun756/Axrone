namespace Axrone.Tween;

using System.Runtime.ExceptionServices;
using Axrone.Utility.Backoff.SpinPolicies;

/// <summary>
/// Tween orchestrator over a <see cref="TweenStore"/>: scheduling, lifecycle, clocked pump.
/// </summary>
public sealed class TweenEngine : IDisposable
{
    private const int StateRunning = 0;
    private const int StateCompleting = 1;
    private const int StateTerminated = 2;
    private const int StateFaulted = 3;

    private readonly TweenStore _store;
    private readonly ITweenClock _clock;
    private float _timeScale = 1.0f;
    private int _state;
    private ExceptionDispatchInfo? _fault;
    private int _disposed;

    /// <summary>Creates an engine.</summary>
    /// <param name="capacity">Tween slots.</param>
    /// <param name="clock">Time source; wall clock by default.</param>
    public TweenEngine(int capacity = 1024, ITweenClock? clock = null)
    {
        _store = new TweenStore(capacity);
        _clock = clock ?? new StopwatchTweenClock();
    }

    /// <summary>Global playback rate multiplier.</summary>
    public float TimeScale
    {
        get => Volatile.Read(ref _timeScale);
        set => Volatile.Write(ref _timeScale, value);
    }

    /// <summary>Live tweens.</summary>
    public int ActiveCount => _store.ActiveCount;

    /// <summary>Schedules a spec; false when full or not running (backpressure, not an error).</summary>
    public bool TryPlay(TweenSpec spec, out TweenHandle handle)
    {
        ThrowIfFaulted();
        if (Volatile.Read(ref _state) != StateRunning)
        {
            handle = default;
            return false;
        }

        if (!_store.TryAllocate(out uint index))
        {
            handle = default;
            return false;
        }

        _store.Initialize(
            index,
            spec.Start,
            spec.End,
            spec.Duration,
            spec.Delay,
            spec.Easing,
            spec.CustomEasing,
            spec.Mode,
            spec.LoopCount,
            spec.TimeScale,
            spec.ChannelCount,
            spec.OnUpdateFloat,
            spec.OnUpdateVector2,
            spec.OnUpdateVector3,
            spec.OnUpdateVector4,
            spec.OnStart,
            spec.OnComplete);

        uint generation = _store.GenerationOf(index);
        handle = new TweenHandle(this, new TweenId(index, generation));
        return true;
    }

    /// <summary>Schedules a spec, spinning while full. Throws once completed.</summary>
    public TweenHandle Play(TweenSpec spec)
    {
        ProgressiveSpinBackoff.Initialize(out int backoff);
        TweenHandle handle = default;
        while (!TryPlay(spec, out handle))
        {
            ThrowIfFaulted();
            if (Volatile.Read(ref _state) != StateRunning)
            {
                ThrowHelper.ThrowInvalidOperation("The tween engine has completed and no longer accepts schedules.");
            }

            ProgressiveSpinBackoff.Advance(ref backoff);
        }

        return handle;
    }

    /// <summary>Pumps one clock step.</summary>
    public void Update() => Update(_clock.Tick());

    /// <summary>Pumps an explicit delta (deterministic stepping).</summary>
    public void Update(DurationNs delta)
    {
        ThrowIfFaulted();
        if (Volatile.Read(ref _state) == StateTerminated)
        {
            return;
        }

        _store.Update(delta, TimeScale);
    }

    /// <summary>Cancels a tween; false for stale identities.</summary>
    public bool Cancel(TweenId id)
    {
        if (!_store.Validate(id))
        {
            return false;
        }

        _store.Free((uint)id.Index);
        return true;
    }

    /// <summary>Pauses a playing tween.</summary>
    public bool Pause(TweenId id)
    {
        if (!_store.Validate(id))
        {
            return false;
        }

        int index = (int)id.Index;
        if (_store.GetState(index) != TweenState.Playing)
        {
            return false;
        }

        _store.SetState(index, TweenState.Paused);
        return true;
    }

    /// <summary>Resumes a paused tween.</summary>
    public bool Resume(TweenId id)
    {
        if (!_store.Validate(id))
        {
            return false;
        }

        int index = (int)id.Index;
        if (_store.GetState(index) != TweenState.Paused)
        {
            return false;
        }

        _store.SetState(index, TweenState.Playing);
        return true;
    }

    /// <summary>Queries tween state; stale identities report inactive.</summary>
    public TweenState QueryState(TweenId id)
    {
        if (!_store.Validate(id))
        {
            return TweenState.Inactive;
        }

        return _store.GetState((int)id.Index);
    }

    /// <summary>Stops accepting schedules; in-flight tweens keep ticking until terminated.</summary>
    public void Complete(Exception? error = null)
    {
        if (error is not null)
        {
            _fault = ExceptionDispatchInfo.Capture(error);
            Volatile.Write(ref _state, StateFaulted);
        }
        else
        {
            Interlocked.CompareExchange(ref _state, StateCompleting, StateRunning);
        }
    }

    /// <summary>Rethrows the terminal fault captured by <see cref="Complete(Exception?)"/>.</summary>
    public void ThrowIfFaulted() => _fault?.Throw();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Complete();
        _store.Dispose();
    }
}
