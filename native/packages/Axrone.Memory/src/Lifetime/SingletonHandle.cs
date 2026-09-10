namespace Axrone.Memory.Lifetime;

public readonly struct SingletonHandle<T> : IEquatable<SingletonHandle<T>> where T : class
{
    private readonly ISingleton<T> _singleton;

    public SingletonHandle(ISingleton<T> singleton)
    {
        ArgumentNullException.ThrowIfNull(singleton);
        _singleton = singleton;
    }

    public T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _singleton.Value;
    }

    public bool IsCreated => _singleton.IsValueCreated;

    public SingletonLifecycleState State => _singleton.State;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator T(SingletonHandle<T> handle) => handle.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T ToT() => Value;

    public bool Equals(SingletonHandle<T> other) => ReferenceEquals(_singleton, other._singleton);

    public override bool Equals(object? obj) => obj is SingletonHandle<T> other && Equals(other);

    public override int GetHashCode() => _singleton?.GetHashCode() ?? 0;

    public static bool operator ==(SingletonHandle<T> left, SingletonHandle<T> right) => left.Equals(right);

    public static bool operator !=(SingletonHandle<T> left, SingletonHandle<T> right) => !left.Equals(right);
}
