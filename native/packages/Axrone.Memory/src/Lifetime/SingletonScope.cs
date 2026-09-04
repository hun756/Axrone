using System.Diagnostics.CodeAnalysis;

namespace Axrone.Memory.Lifetime;

public sealed class SingletonScope : ISingletonScope
{
    private readonly ISingletonRegistry _parent;
    private readonly SingletonRegistry _local = new();
    private int _isDisposed;

    public SingletonScope(ISingletonRegistry parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        _parent = parent;
    }

    public void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>() where T : class, new()
    {
        ThrowIfDisposed();
        _local.Register<T>();
    }

    public void Register<T>(Func<T> factory, LazyThreadSafetyMode mode = LazyThreadSafetyMode.ExecutionAndPublication) where T : class
    {
        ThrowIfDisposed();
        _local.Register(factory, mode);
    }

    public void Register<T>(ISingleton<T> singleton) where T : class
    {
        ThrowIfDisposed();
        _local.Register(singleton);
    }

    public void RegisterInstance<T>(T instance) where T : class
    {
        ThrowIfDisposed();
        _local.RegisterInstance(instance);
    }

    public T Get<T>() where T : class
    {
        ThrowIfDisposed();
        if (_local.TryGet<T>(out var localInstance))
        {
            return localInstance;
        }

        return _parent.Get<T>();
    }

    public bool TryGet<T>([NotNullWhen(true)] out T? instance) where T : class
    {
        ThrowIfDisposed();
        if (_local.TryGet<T>(out instance))
        {
            return true;
        }

        return _parent.TryGet<T>(out instance);
    }

    public bool Contains<T>() where T : class
    {
        ThrowIfDisposed();
        return _local.Contains<T>() || _parent.Contains<T>();
    }

    public ISingletonScope CreateChildScope()
    {
        ThrowIfDisposed();
        return new SingletonScope(this);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            throw new SingletonDisposedException(nameof(SingletonScope));
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _local.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return ValueTask.CompletedTask;
        }

        return _local.DisposeAsync();
    }
}
