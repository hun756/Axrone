namespace Axrone.Reactive.Tests;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = new();
    private long _timestamp;

    public override long TimestampFrequency => 1000;

    public override long GetTimestamp() => Volatile.Read(ref _timestamp);

    public void Advance(int milliseconds)
    {
        Interlocked.Add(ref _timestamp, milliseconds);
        long now = Volatile.Read(ref _timestamp);
        ManualTimer[] snapshot;
        lock (_timers)
        {
            snapshot = _timers.ToArray();
        }

        for (int index = 0; index < snapshot.Length; index++)
        {
            snapshot[index].FireIfDue(now);
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    internal void Add(ManualTimer timer)
    {
        lock (_timers)
        {
            _timers.Add(timer);
        }
    }

    internal void Remove(ManualTimer timer)
    {
        lock (_timers)
        {
            _timers.Remove(timer);
        }
    }

    internal sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private readonly Lock _gate = new();
        private long _due;
        private bool _active;
        private bool _disposed;

        public ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
            _owner.Add(this);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _ = period;
            if (_disposed)
            {
                return false;
            }

            if (dueTime == Timeout.InfiniteTimeSpan)
            {
                _active = false;
                return true;
            }

            _due = _owner.GetTimestamp() +
                (long)(dueTime.TotalMilliseconds * _owner.TimestampFrequency / 1000.0);
            _active = true;
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _active = false;
            _owner.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        internal void FireIfDue(long now)
        {
            bool fire;
            lock (_gate)
            {
                fire = _active && !_disposed && now >= _due;
                if (fire)
                {
                    _active = false;
                }
            }

            if (fire)
            {
                _callback(_state);
            }
        }
    }
}
