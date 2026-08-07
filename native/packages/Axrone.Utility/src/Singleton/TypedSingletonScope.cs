using Axrone.Utility.Singleton.Internal;

namespace Axrone.Utility.Singleton;

/// <summary>
/// Generic typed scope — manages a single <typeparamref name="T"/> instance per async context
/// via <see cref="AsyncLocal{T}"/>. Supports parent-child nesting with automatic restore on dispose.
/// </summary>
/// <typeparam name="T">The scoped singleton type.</typeparam>
/// <remarks>
/// <para>Falls back to a global instance when no scope is active.</para>
/// <para>Unlike the non-generic <see cref="SingletonScope"/>, this type manages a single typed value
/// rather than a named-key registry.</para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public abstract class TypedSingletonScope<T> : IDisposable where T : class, new()
{
    private static readonly AsyncLocal<T?> _currentInstance = new();
    private static T? _globalInstance;
    private static int _globalInitialized;

    private readonly T? _previousInstance;
    private bool _disposed;

    /// <summary>
    /// Creates a new scope. Saves the previous instance and installs a fresh one.
    /// </summary>
    protected TypedSingletonScope()
    {
        _previousInstance = _currentInstance.Value;
        _currentInstance.Value = CreateInstance();
    }

    /// <summary>Get the current scoped instance, or the global fallback if no scope is active.</summary>
    public static T Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _currentInstance.Value ?? GetGlobalInstance();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T GetGlobalInstance()
    {
        var instance = Volatile.Read(ref _globalInstance);
        if (instance is not null) return instance;

        if (Interlocked.CompareExchange(ref _globalInitialized, 1, 0) == 0)
        {
            Volatile.Write(ref _globalInstance, CreateInstance());
            return Volatile.Read(ref _globalInstance)!;
        }

        SpinWait spinner = default;
        while (Volatile.Read(ref _globalInitialized) != 1)
            spinner.SpinOnce();

        return Volatile.Read(ref _globalInstance)!;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T CreateInstance()
    {
        try
        {
            using (NestingContext.EnterScope())
            {
                var instance = new T();
                if (instance is IInitializable initializable)
                    initializable.Initialize();
                return instance;
            }
        }
        catch (Exception ex) when (ex is not SingletonCreationException)
        {
            throw new SingletonCreationException(
                $"Failed to create scoped instance of type {typeof(T).Name}", typeof(T), ex);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Release managed resources and restore the previous scope.</summary>
    /// <param name="disposing"><c>true</c> from Dispose; <c>false</c> from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing && _currentInstance.Value is IDisposable disposable)
            disposable.Dispose();

        _currentInstance.Value = _previousInstance;
    }
}
