namespace Axrone.ObjectPool;

/// <summary>
/// An asynchronous reader-writer lock that supports multiple concurrent readers, a single exclusive writer,
/// and upgradeable read locks. Optionally supports recursive lock acquisition on the same thread.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public sealed class AsyncReaderWriterLock : IDisposable, IAsyncDisposable
{
    private struct LockHandle : IDisposable
    {
        private AsyncReaderWriterLock? _lock;
        private readonly int _lockType;

        public LockHandle(AsyncReaderWriterLock lockObj, int lockType)
        {
            _lock = lockObj;
            _lockType = lockType;
        }

        public void Dispose()
        {
            var lockObj = _lock;
            _lock = null;

            if (lockObj is null)
                return;

            switch (_lockType)
            {
                case READ_LOCK:
                    lockObj.ExitReadLock();
                    break;
                case UPGRADEABLE_READ_LOCK:
                    lockObj.ExitUpgradeableReadLock();
                    break;
                case WRITE_LOCK:
                    lockObj.ExitWriteLock();
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

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncReaderWriterLock"/> class.
    /// </summary>
    /// <param name="supportRecursion">
    /// <c>true</c> to allow the same thread to re-enter read, upgradeable-read, or write locks recursively;
    /// otherwise <c>false</c>.
    /// </param>
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

    /// <summary>
    /// Acquires a read lock synchronously and returns a disposable handle that releases the lock when disposed.
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> that releases the read lock upon disposal.</returns>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDisposable ReadLock()
    {
        EnterReadLock();
        return new LockHandle(this, READ_LOCK);
    }

    /// <summary>
    /// Acquires an upgradeable read lock synchronously and returns a disposable handle that releases the lock when disposed.
    /// An upgradeable read lock allows the holder to later upgrade to a write lock.
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> that releases the upgradeable read lock upon disposal.</returns>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDisposable UpgradeableReadLock()
    {
        EnterUpgradeableReadLock();
        return new LockHandle(this, UPGRADEABLE_READ_LOCK);
    }

    /// <summary>
    /// Acquires a write lock synchronously and returns a disposable handle that releases the lock when disposed.
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> that releases the write lock upon disposal.</returns>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDisposable WriteLock()
    {
        EnterWriteLock();
        return new LockHandle(this, WRITE_LOCK);
    }

    /// <summary>
    /// Asynchronously acquires a read lock and returns a disposable handle that releases the lock when disposed.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes with an <see cref="IDisposable"/> that releases the read lock upon disposal.</returns>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<IDisposable> ReadLockAsync(CancellationToken cancellationToken = default)
    {
        await EnterReadLockAsync(cancellationToken).ConfigureAwait(false);
        return new LockHandle(this, READ_LOCK);
    }

    /// <summary>
    /// Asynchronously acquires an upgradeable read lock and returns a disposable handle that releases the lock when disposed.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes with an <see cref="IDisposable"/> that releases the upgradeable read lock upon disposal.</returns>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<IDisposable> UpgradeableReadLockAsync(
        CancellationToken cancellationToken = default)
    {
        await EnterUpgradeableReadLockAsync(cancellationToken).ConfigureAwait(false);
        return new LockHandle(this, UPGRADEABLE_READ_LOCK);
    }

    /// <summary>
    /// Asynchronously acquires a write lock and returns a disposable handle that releases the lock when disposed.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes with an <see cref="IDisposable"/> that releases the write lock upon disposal.</returns>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<IDisposable> WriteLockAsync(CancellationToken cancellationToken = default)
    {
        await EnterWriteLockAsync(cancellationToken).ConfigureAwait(false);
        return new LockHandle(this, WRITE_LOCK);
    }

    /// <summary>
    /// Asynchronously attempts to acquire a write lock within the specified timeout.
    /// Returns a disposable handle that releases the lock when disposed.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for the write lock.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes with an <see cref="IDisposable"/> that releases the write lock upon disposal.</returns>
    /// <exception cref="TimeoutException">The write lock could not be acquired within the specified timeout.</exception>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was canceled.</exception>
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

    /// <summary>
    /// Enters a read lock synchronously. The caller must call <see cref="ExitReadLock"/> to release the lock.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    /// <exception cref="LockRecursionException">
    /// Thrown when recursion is enabled and the current thread holds a read lock and attempts to upgrade.
    /// </exception>
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

    /// <summary>
    /// Asynchronously enters a read lock. The caller must call <see cref="ExitReadLock"/> to release the lock.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous lock acquisition.</returns>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was canceled.</exception>
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

    /// <summary>
    /// Enters an upgradeable read lock synchronously. The caller must call <see cref="ExitUpgradeableReadLock"/> to release the lock.
    /// An upgradeable read lock allows the holder to later upgrade to a write lock.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    /// <exception cref="LockRecursionException">
    /// Thrown when recursion is enabled and the current thread holds a read lock and attempts to upgrade.
    /// </exception>
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

    /// <summary>
    /// Asynchronously enters an upgradeable read lock. The caller must call <see cref="ExitUpgradeableReadLock"/> to release the lock.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous lock acquisition.</returns>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was canceled.</exception>
    /// <exception cref="LockRecursionException">
    /// Thrown when recursion is enabled and the current thread holds a read lock and attempts to upgrade.
    /// </exception>
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

    /// <summary>
    /// Enters a write lock synchronously. The caller must call <see cref="ExitWriteLock"/> to release the lock.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    /// <exception cref="LockRecursionException">
    /// Thrown when recursion is enabled and the current thread holds a read lock and attempts to upgrade to a write lock.
    /// </exception>
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

    /// <summary>
    /// Attempts to enter a write lock synchronously within the specified timeout.
    /// </summary>
    /// <param name="millisecondsTimeout">
    /// The number of milliseconds to wait, or 0 to attempt the lock without waiting.
    /// </param>
    /// <returns><c>true</c> if the write lock was acquired; otherwise <c>false</c>.</returns>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
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

    /// <summary>
    /// Asynchronously attempts to enter a write lock within the specified timeout.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for the write lock.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes with <c>true</c> if the write lock was acquired; otherwise <c>false</c>.</returns>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was canceled.</exception>
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

    /// <summary>
    /// Asynchronously enters a write lock. The caller must call <see cref="ExitWriteLock"/> to release the lock.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous lock acquisition.</returns>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The cancellation token was canceled.</exception>
    /// <exception cref="LockRecursionException">
    /// Thrown when recursion is enabled and the current thread holds a read lock and attempts to upgrade to a write lock.
    /// </exception>
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

    /// <summary>
    /// Exits a previously acquired read lock.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
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

    /// <summary>
    /// Exits a previously acquired upgradeable read lock.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
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

    /// <summary>
    /// Exits a previously acquired write lock.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The lock has been disposed.</exception>
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

    /// <summary>
    /// Releases all resources used by this <see cref="AsyncReaderWriterLock"/> instance.
    /// </summary>
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

    /// <summary>
    /// Asynchronously releases all resources used by this <see cref="AsyncReaderWriterLock"/> instance.
    /// </summary>
    /// <returns>A completed <see cref="ValueTask"/>.</returns>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
