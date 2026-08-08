namespace Axrone.ObjectPool;

[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public sealed class AsyncReaderWriterLock : IDisposable, IAsyncDisposable
{
    private readonly struct LockHandle : IDisposable
    {
        private readonly AsyncReaderWriterLock _lock;
        private readonly int _lockType;

        public LockHandle(AsyncReaderWriterLock lockObj, int lockType)
        {
            _lock = lockObj;
            _lockType = lockType;
        }

        public void Dispose()
        {
            switch (_lockType)
            {
                case READ_LOCK:
                    _lock.ExitReadLock();
                    break;
                case UPGRADEABLE_READ_LOCK:
                    _lock.ExitUpgradeableReadLock();
                    break;
                case WRITE_LOCK:
                    _lock.ExitWriteLock();
                    break;
            }
        }
    }

    private const int READ_LOCK = 1;
    private const int UPGRADEABLE_READ_LOCK = 2;
    private const int WRITE_LOCK = 3;

    private readonly SemaphoreSlim _readSemaphore;
    private readonly SemaphoreSlim _upgradeSemaphore;
    private readonly SemaphoreSlim _writeSemaphore;
    private readonly bool _supportRecursion;
    private readonly ThreadLocal<(int LockType, int Count)>? _recursiveData;
    private int _readerCount;
    private int _upgraderCount;
    private int _writerCount;
    private volatile bool _disposed;

    public AsyncReaderWriterLock(bool supportRecursion = false)
    {
        _readSemaphore = new SemaphoreSlim(int.MaxValue, int.MaxValue);
        _upgradeSemaphore = new SemaphoreSlim(1, 1);
        _writeSemaphore = new SemaphoreSlim(1, 1);
        _supportRecursion = supportRecursion;

        if (supportRecursion)
        {
            _recursiveData = new ThreadLocal<(int, int)>(() => (0, 0));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDisposable ReadLock()
    {
        EnterReadLock();
        return new LockHandle(this, READ_LOCK);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDisposable UpgradeableReadLock()
    {
        EnterUpgradeableReadLock();
        return new LockHandle(this, UPGRADEABLE_READ_LOCK);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDisposable WriteLock()
    {
        EnterWriteLock();
        return new LockHandle(this, WRITE_LOCK);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<IDisposable> ReadLockAsync(CancellationToken cancellationToken = default)
    {
        await EnterReadLockAsync(cancellationToken).ConfigureAwait(false);
        return new LockHandle(this, READ_LOCK);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<IDisposable> UpgradeableReadLockAsync(
        CancellationToken cancellationToken = default)
    {
        await EnterUpgradeableReadLockAsync(cancellationToken).ConfigureAwait(false);
        return new LockHandle(this, UPGRADEABLE_READ_LOCK);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<IDisposable> WriteLockAsync(CancellationToken cancellationToken = default)
    {
        await EnterWriteLockAsync(cancellationToken).ConfigureAwait(false);
        return new LockHandle(this, WRITE_LOCK);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<IDisposable> TryWriteLockAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        bool acquired = await TryEnterWriteLockAsync(timeout, cancellationToken)
            .ConfigureAwait(false);
        return acquired
            ? new LockHandle(this, WRITE_LOCK)
            : throw new TimeoutException("Failed to acquire write lock");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnterReadLock()
    {
        ThrowIfDisposed();

        if (_supportRecursion && _recursiveData is not null)
        {
            var current = _recursiveData.Value;

            if (current.LockType == WRITE_LOCK || current.LockType == UPGRADEABLE_READ_LOCK)
            {
                _recursiveData.Value = (current.LockType, current.Count + 1);
                return;
            }

            if (current.LockType == READ_LOCK)
            {
                _recursiveData.Value = (READ_LOCK, current.Count + 1);
                return;
            }
        }

        _writeSemaphore.Wait();
        try
        {
            Interlocked.Increment(ref _readerCount);
        }
        finally
        {
            _writeSemaphore.Release();
        }

        if (_supportRecursion && _recursiveData is not null)
        {
            _recursiveData.Value = (READ_LOCK, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task EnterReadLockAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_supportRecursion && _recursiveData is not null)
        {
            var current = _recursiveData.Value;

            if (current.LockType == WRITE_LOCK || current.LockType == UPGRADEABLE_READ_LOCK)
            {
                _recursiveData.Value = (current.LockType, current.Count + 1);
                return;
            }

            if (current.LockType == READ_LOCK)
            {
                _recursiveData.Value = (READ_LOCK, current.Count + 1);
                return;
            }
        }

        await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Interlocked.Increment(ref _readerCount);
        }
        finally
        {
            _writeSemaphore.Release();
        }

        if (_supportRecursion && _recursiveData is not null)
        {
            _recursiveData.Value = (READ_LOCK, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnterUpgradeableReadLock()
    {
        ThrowIfDisposed();

        if (_supportRecursion && _recursiveData is not null)
        {
            var current = _recursiveData.Value;

            if (current.LockType == WRITE_LOCK)
            {
                _recursiveData.Value = (current.LockType, current.Count + 1);
                return;
            }

            if (current.LockType == UPGRADEABLE_READ_LOCK)
            {
                _recursiveData.Value = (UPGRADEABLE_READ_LOCK, current.Count + 1);
                return;
            }

            if (current.LockType == READ_LOCK)
            {
                throw new LockRecursionException(
                    "Cannot upgrade from read lock to upgradeable read lock");
            }
        }

        _upgradeSemaphore.Wait();
        try
        {
            _writeSemaphore.Wait();
            try
            {
                Interlocked.Increment(ref _upgraderCount);
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }
        catch
        {
            _upgradeSemaphore.Release();
            throw;
        }

        if (_supportRecursion && _recursiveData is not null)
        {
            _recursiveData.Value = (UPGRADEABLE_READ_LOCK, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task EnterUpgradeableReadLockAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_supportRecursion && _recursiveData is not null)
        {
            var current = _recursiveData.Value;

            if (current.LockType == WRITE_LOCK)
            {
                _recursiveData.Value = (current.LockType, current.Count + 1);
                return;
            }

            if (current.LockType == UPGRADEABLE_READ_LOCK)
            {
                _recursiveData.Value = (UPGRADEABLE_READ_LOCK, current.Count + 1);
                return;
            }

            if (current.LockType == READ_LOCK)
            {
                throw new LockRecursionException(
                    "Cannot upgrade from read lock to upgradeable read lock");
            }
        }

        await _upgradeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Interlocked.Increment(ref _upgraderCount);
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }
        catch
        {
            _upgradeSemaphore.Release();
            throw;
        }

        if (_supportRecursion && _recursiveData is not null)
        {
            _recursiveData.Value = (UPGRADEABLE_READ_LOCK, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnterWriteLock()
    {
        ThrowIfDisposed();

        if (_supportRecursion && _recursiveData is not null)
        {
            var current = _recursiveData.Value;

            if (current.LockType == WRITE_LOCK)
            {
                _recursiveData.Value = (WRITE_LOCK, current.Count + 1);
                return;
            }

            if (current.LockType == READ_LOCK)
            {
                throw new LockRecursionException("Cannot upgrade from read lock to write lock");
            }

            if (current.LockType == UPGRADEABLE_READ_LOCK)
            {
                _writeSemaphore.Wait();
                try
                {
                    Interlocked.Increment(ref _writerCount);
                }
                catch
                {
                    _writeSemaphore.Release();
                    throw;
                }

                _recursiveData.Value = (WRITE_LOCK, current.Count + 1);
                return;
            }
        }

        _writeSemaphore.Wait();
        try
        {
            Interlocked.Increment(ref _writerCount);
        }
        catch
        {
            _writeSemaphore.Release();
            throw;
        }

        if (_supportRecursion && _recursiveData is not null)
        {
            _recursiveData.Value = (WRITE_LOCK, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnterWriteLock(int millisecondsTimeout = 0)
    {
        ThrowIfDisposed();

        if (_supportRecursion && _recursiveData is not null)
        {
            var current = _recursiveData.Value;

            if (current.LockType == WRITE_LOCK)
            {
                _recursiveData.Value = (WRITE_LOCK, current.Count + 1);
                return true;
            }

            if (current.LockType == READ_LOCK)
            {
                return false;
            }

            if (current.LockType == UPGRADEABLE_READ_LOCK)
            {
                if (!_writeSemaphore.Wait(millisecondsTimeout))
                {
                    return false;
                }

                try
                {
                    Interlocked.Increment(ref _writerCount);
                }
                catch
                {
                    _writeSemaphore.Release();
                    throw;
                }

                _recursiveData.Value = (WRITE_LOCK, current.Count + 1);
                return true;
            }
        }

        if (!_writeSemaphore.Wait(millisecondsTimeout))
        {
            return false;
        }

        try
        {
            Interlocked.Increment(ref _writerCount);
        }
        catch
        {
            _writeSemaphore.Release();
            throw;
        }

        if (_supportRecursion && _recursiveData is not null)
        {
            _recursiveData.Value = (WRITE_LOCK, 1);
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<bool> TryEnterWriteLockAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_supportRecursion && _recursiveData is not null)
        {
            var current = _recursiveData.Value;

            if (current.LockType == WRITE_LOCK)
            {
                _recursiveData.Value = (WRITE_LOCK, current.Count + 1);
                return true;
            }

            if (current.LockType == READ_LOCK)
            {
                return false;
            }

            if (current.LockType == UPGRADEABLE_READ_LOCK)
            {
                if (!await _writeSemaphore
                    .WaitAsync(timeout, cancellationToken)
                    .ConfigureAwait(false))
                {
                    return false;
                }

                try
                {
                    Interlocked.Increment(ref _writerCount);
                }
                catch
                {
                    _writeSemaphore.Release();
                    throw;
                }

                _recursiveData.Value = (WRITE_LOCK, current.Count + 1);
                return true;
            }
        }

        if (!await _writeSemaphore
            .WaitAsync(timeout, cancellationToken)
            .ConfigureAwait(false))
        {
            return false;
        }

        try
        {
            Interlocked.Increment(ref _writerCount);
        }
        catch
        {
            _writeSemaphore.Release();
            throw;
        }

        if (_supportRecursion && _recursiveData is not null)
        {
            _recursiveData.Value = (WRITE_LOCK, 1);
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task EnterWriteLockAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_supportRecursion && _recursiveData is not null)
        {
            var current = _recursiveData.Value;

            if (current.LockType == WRITE_LOCK)
            {
                _recursiveData.Value = (WRITE_LOCK, current.Count + 1);
                return;
            }

            if (current.LockType == READ_LOCK)
            {
                throw new LockRecursionException("Cannot upgrade from read lock to write lock");
            }

            if (current.LockType == UPGRADEABLE_READ_LOCK)
            {
                await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    Interlocked.Increment(ref _writerCount);
                }
                catch
                {
                    _writeSemaphore.Release();
                    throw;
                }

                _recursiveData.Value = (WRITE_LOCK, current.Count + 1);
                return;
            }
        }

        await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Interlocked.Increment(ref _writerCount);
        }
        catch
        {
            _writeSemaphore.Release();
            throw;
        }

        if (_supportRecursion && _recursiveData is not null)
        {
            _recursiveData.Value = (WRITE_LOCK, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitReadLock()
    {
        ThrowIfDisposed();

        if (_supportRecursion && _recursiveData is not null)
        {
            var current = _recursiveData.Value;

            if ((current.LockType == WRITE_LOCK || current.LockType == UPGRADEABLE_READ_LOCK)
                && current.Count > 0)
            {
                _recursiveData.Value = (current.LockType, current.Count - 1);
                return;
            }

            if (current.LockType == READ_LOCK && current.Count > 1)
            {
                _recursiveData.Value = (READ_LOCK, current.Count - 1);
                return;
            }

            if (current.LockType == READ_LOCK && current.Count == 1)
            {
                _recursiveData.Value = (0, 0);
            }
        }

        Interlocked.Decrement(ref _readerCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitUpgradeableReadLock()
    {
        ThrowIfDisposed();

        if (_supportRecursion && _recursiveData is not null)
        {
            var current = _recursiveData.Value;

            if (current.LockType == WRITE_LOCK && current.Count > 0)
            {
                _recursiveData.Value = (WRITE_LOCK, current.Count - 1);
                return;
            }

            if (current.LockType == UPGRADEABLE_READ_LOCK && current.Count > 1)
            {
                _recursiveData.Value = (UPGRADEABLE_READ_LOCK, current.Count - 1);
                return;
            }

            if (current.LockType == UPGRADEABLE_READ_LOCK && current.Count == 1)
            {
                _recursiveData.Value = (0, 0);
            }
        }

        Interlocked.Decrement(ref _upgraderCount);
        _upgradeSemaphore.Release();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitWriteLock()
    {
        ThrowIfDisposed();

        if (_supportRecursion && _recursiveData is not null)
        {
            var current = _recursiveData.Value;

            if (current.LockType == WRITE_LOCK && current.Count > 1)
            {
                _recursiveData.Value = (WRITE_LOCK, current.Count - 1);
                return;
            }

            if (current.LockType == WRITE_LOCK && current.Count == 1)
            {
                _recursiveData.Value = (0, 0);
            }
        }

        Interlocked.Decrement(ref _writerCount);
        _writeSemaphore.Release();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(AsyncReaderWriterLock));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _readSemaphore.Dispose();
        _upgradeSemaphore.Dispose();
        _writeSemaphore.Dispose();
        _recursiveData?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
