namespace Axrone.Ecs;

using System.Runtime.ExceptionServices;

/// <summary>
/// Persistent worker pool for deterministic range-partitioned ECS work. The calling
/// thread is worker zero; background workers own the remaining contiguous ranges. A run
/// starts no threads and allocates no work items, so steady-state parallel dispatch costs
/// only events and atomics.
/// </summary>
internal sealed unsafe class EcsWorkerPool : IDisposable
{
    private readonly int _workerCount;
    private readonly Thread[] _threads;
    private readonly AutoResetEvent[] _signals;
    private readonly ManualResetEventSlim _done = new(false);
    private readonly Exception?[] _errors;
    private readonly Lock _runGate = new();
    private nint _state;
    private nint _body;
    private int _itemCount;
    private int _pending;
    private volatile bool _shutdown;

    [ThreadStatic]
    private static bool s_inWorker;

    public EcsWorkerPool(int workerCount)
    {
        if (workerCount < 1)
        {
            EcsThrowHelper.ThrowArgumentOutOfRange(nameof(workerCount));
        }

        _workerCount = workerCount;
        _threads = new Thread[workerCount - 1];
        _signals = new AutoResetEvent[workerCount - 1];
        _errors = new Exception?[workerCount];

        for (int index = 0; index < _threads.Length; index++)
        {
            int workerId = index + 1;
            _signals[index] = new AutoResetEvent(false);
            _threads[index] = new Thread(() => WorkerLoop(workerId))
            {
                IsBackground = true,
                Name = $"Axrone.Ecs.Worker{workerId}",
            };
            _threads[index].Start();
        }
    }

    /// <summary>Total workers, including the calling thread.</summary>
    public int WorkerCount => _workerCount;

    /// <summary>Whether the current thread is executing pool work.</summary>
    public static bool IsInWorker => s_inWorker;

    /// <summary>
    /// Runs <paramref name="body"/> over contiguous ranges derived from the worker id.
    /// Every range is deterministic and exactly one worker writes each range.
    /// </summary>
    public void RunPartitioned(nint state, delegate*<nint, int, int, int, void> body, int itemCount)
    {
        if (itemCount <= 0)
        {
            return;
        }

        lock (_runGate)
        {
            _state = state;
            Volatile.Write(ref _body, (nint)body);
            _itemCount = itemCount;
            _pending = _threads.Length;
            Array.Clear(_errors);
            _done.Reset();

            for (int index = 0; index < _signals.Length; index++)
            {
                _signals[index].Set();
            }

            bool previous = s_inWorker;
            s_inWorker = true;
            try
            {
                InvokeRange(0, body);
            }
            catch (Exception error)
            {
                _errors[0] = error;
            }
            finally
            {
                s_inWorker = previous;
            }

            if (_pending > 0)
            {
                _done.Wait();
            }

            for (int index = 0; index < _errors.Length; index++)
            {
                if (_errors[index] is { } error)
                {
                    ExceptionDispatchInfo.Capture(error).Throw();
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_shutdown)
        {
            return;
        }

        _shutdown = true;
        for (int index = 0; index < _signals.Length; index++)
        {
            _signals[index].Set();
        }

        for (int index = 0; index < _threads.Length; index++)
        {
            _threads[index].Join();
            _signals[index].Dispose();
        }

        _done.Dispose();
    }

    private void WorkerLoop(int workerId)
    {
        while (true)
        {
            _signals[workerId - 1].WaitOne();
            if (_shutdown)
            {
                return;
            }

            bool previous = s_inWorker;
            s_inWorker = true;
            try
            {
                var body = (delegate*<nint, int, int, int, void>)Volatile.Read(ref _body);
                InvokeRange(workerId, body);
            }
            catch (Exception error)
            {
                _errors[workerId] = error;
            }
            finally
            {
                s_inWorker = previous;
                if (Interlocked.Decrement(ref _pending) == 0)
                {
                    _done.Set();
                }
            }
        }
    }

    private void InvokeRange(int workerId, delegate*<nint, int, int, int, void> body)
    {
        int start = (int)((long)_itemCount * workerId / _workerCount);
        int end = (int)((long)_itemCount * (workerId + 1) / _workerCount);
        if (start < end)
        {
            body(_state, workerId, start, end);
        }
    }
}
