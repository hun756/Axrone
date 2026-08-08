namespace Axrone.Utility.Singleton;

/// <summary>
/// Full service container — supports typed and named service registration with disposal.
/// Thread-safe singleton itself.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public sealed class SingletonServiceProvider : IDisposable
{
    private static SingletonServiceProvider? _instance;
    private static int _initialized;

    private readonly ConcurrentDictionary<Type, object> _services = new();
    private readonly ConcurrentDictionary<(Type, string), object> _namedServices = new();
    private int _disposed;

    private SingletonServiceProvider() { }

    /// <summary>Get the global service provider instance.</summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Long-lived static singleton — disposed via IDisposable.Dispose when application shuts down.")]
    public static SingletonServiceProvider Instance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            var inst = Volatile.Read(ref _instance);
            if (inst is not null) return inst;

            if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 0)
            {
                Volatile.Write(ref _instance, new SingletonServiceProvider());
                return Volatile.Read(ref _instance)!;
            }

            SpinWait spinner = default;
            while (Volatile.Read(ref _initialized) != 1)
                spinner.SpinOnce();

            return Volatile.Read(ref _instance)!;
        }
    }

    /// <summary>Register a service instance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Register<T>(T service) where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        ThrowIfDisposed();
        _services[typeof(T)] = service;
    }

    /// <summary>Register a named service instance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Register<T>(T service, string name) where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ThrowIfDisposed();
        _namedServices[(typeof(T), name)] = service;
    }

    /// <summary>Get a registered service. Throws if not found.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T GetService<T>() where T : class
    {
        ThrowIfDisposed();
        if (_services.TryGetValue(typeof(T), out var service))
            return (T)service;
        throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered");
    }

    /// <summary>Get a named service. Throws if not found.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T GetService<T>(string name) where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ThrowIfDisposed();
        if (_namedServices.TryGetValue((typeof(T), name), out var service))
            return (T)service;
        throw new InvalidOperationException($"Service of type {typeof(T).Name} with name '{name}' is not registered");
    }

    /// <summary>Try to get a registered service.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryGetService<T>([NotNullWhen(true)] out T? service) where T : class
    {
        ThrowIfDisposed();
        if (_services.TryGetValue(typeof(T), out var obj))
        {
            service = (T)obj;
            return true;
        }
        service = default;
        return false;
    }

    /// <summary>Try to get a named service.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryGetService<T>(string name, [NotNullWhen(true)] out T? service) where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ThrowIfDisposed();
        if (_namedServices.TryGetValue((typeof(T), name), out var obj))
        {
            service = (T)obj;
            return true;
        }
        service = default;
        return false;
    }

    /// <summary>Get or create a service using the <c>new()</c> constraint.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T GetOrCreateService<T>() where T : class, new()
    {
        ThrowIfDisposed();
        return (T)_services.GetOrAdd(typeof(T), static _ => new T());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        foreach (var service in _services.Values)
            if (service is IDisposable disp) disp.Dispose();

        foreach (var service in _namedServices.Values)
            if (service is IDisposable disp) disp.Dispose();

        _services.Clear();
        _namedServices.Clear();
    }
}
