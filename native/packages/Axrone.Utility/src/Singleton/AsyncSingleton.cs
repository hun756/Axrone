namespace Axrone.Utility.Singleton;

/// <summary>
/// Async singleton — lazily initializes via an async factory with <see cref="SemaphoreSlim"/> gating.
/// Supports fault caching: if the factory throws, subsequent calls re-throw the original exception.
/// </summary>
/// <typeparam name="T">The singleton value type.</typeparam>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public sealed class AsyncSingleton<T> : IAsyncDisposable where T : class
{
    private readonly Func<CancellationToken, ValueTask<T>> _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private T? _value;
    private volatile bool _initialized;
    private volatile bool _faulted;
    private Exception? _exception;

    public AsyncSingleton(Func<CancellationToken, ValueTask<T>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    /// <summary>Whether the async initialization completed successfully.</summary>
    public bool IsInitialized => _initialized;

    /// <summary>Whether the async initialization failed.</summary>
    public bool IsFaulted => _faulted;

    /// <summary>Get or create the singleton instance asynchronously.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public async ValueTask<T> GetInstanceAsync(CancellationToken ct = default)
    {
        if (_initialized) return _value!;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized) return _value!;
            if (_faulted) throw _exception!;

            _value = await _factory(ct).ConfigureAwait(false);
            _initialized = true;
            return _value;
        }
        catch (Exception ex)
        {
            _faulted = true;
            _exception = ex;
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Dispose the underlying instance if it implements <see cref="IAsyncDisposable"/> or <see cref="IDisposable"/>.</summary>
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_value is IAsyncDisposable asyncDisp)
                await asyncDisp.DisposeAsync().ConfigureAwait(false);
            else if (_value is IDisposable disp)
                disp.Dispose();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
