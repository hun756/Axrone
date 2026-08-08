using Text = System.Text;
using IO = System.IO;

namespace Axrone.ObjectPool;

public static class ObjectPoolFactory
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObjectPool<T> Create<T>(
        Func<T> factory,
        int initialCapacity = 0,
        int maxCapacity = 0)
        where T : class
    {
        return new ObjectPool<T>(factory, null, null, initialCapacity, maxCapacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObjectPool<T> Create<T>(
        Func<T> factory,
        Action<T> reset,
        int initialCapacity = 0,
        int maxCapacity = 0)
        where T : class
    {
        return new ObjectPool<T>(factory, reset, null, initialCapacity, maxCapacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObjectPool<T> CreateHighPerformance<T>(Func<T> factory)
        where T : class
    {
        return new ObjectPool<T>(
            PoolConfiguration.HighPerformance,
            new PoolableHandler<T> { Factory = factory });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObjectPool<T> CreateNativeAot<T>(Func<T> factory)
        where T : class
    {
        return new ObjectPool<T>(
            PoolConfiguration.NativeAotOptimized,
            new PoolableHandler<T> { Factory = factory });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObjectPool<T> CreateNativeAot<T>(Func<T> factory, Action<T> reset)
        where T : class
    {
        return new ObjectPool<T>(
            PoolConfiguration.NativeAotOptimized,
            new PoolableHandler<T> { Factory = factory, Reset = reset });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObjectPool<T> CreateMemoryOptimized<T>(Func<T> factory)
        where T : class
    {
        return new ObjectPool<T>(
            PoolConfiguration.MemoryConserving,
            new PoolableHandler<T> { Factory = factory });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObjectPool<T> CreateWithHandler<T>(
        Func<T> factory,
        Action<T>? reset = null,
        Action<T>? onDispose = null)
        where T : class
    {
        return new ObjectPool<T>(
            PoolConfiguration.Default,
            new PoolableHandler<T>
            {
                Factory = factory,
                Reset = reset,
                OnDispose = onDispose,
            });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObjectPool<T> CreateThreadLocal<T>(
        Func<T> factory,
        int initialCapacity = 0,
        int maxCapacity = 0)
        where T : class
    {
        var config = new PoolConfiguration
        {
            InitialCapacity = initialCapacity,
            MaximumCapacity = maxCapacity > 0 ? maxCapacity : Environment.ProcessorCount * 32,
            Strategy = PoolingStrategy.ThreadLocal,
            UseSlimSynchronization = PoolConfiguration.Default.UseSlimSynchronization,
            ConcurrencyLevel = PoolConfiguration.Default.ConcurrencyLevel,
            DiagnosticsLevel = PoolConfiguration.Default.DiagnosticsLevel,
            MetricsSamplingRate = PoolConfiguration.Default.MetricsSamplingRate,
            EvictionStrategy = PoolConfiguration.Default.EvictionStrategy,
            AllocationPolicy = PoolConfiguration.Default.AllocationPolicy,
            AllowPoolExpansion = PoolConfiguration.Default.AllowPoolExpansion,
            ThrowOnExhaustion = PoolConfiguration.Default.ThrowOnExhaustion,
            TrackLeaks = PoolConfiguration.Default.TrackLeaks,
            ClearOnReturn = PoolConfiguration.Default.ClearOnReturn,
            TargetUtilizationPercentage = PoolConfiguration.Default.TargetUtilizationPercentage,
            IdleTimeout = PoolConfiguration.Default.IdleTimeout,
            ScavengeIntervalMs = PoolConfiguration.Default.ScavengeIntervalMs,
        };

        return new ObjectPool<T>(config, new PoolableHandler<T> { Factory = factory });
    }
}

public static class StaticPool<T> where T : class, new()
{
    private static readonly Lazy<IObjectPool<T>> s_pool = new(
        () => ObjectPoolFactory.CreateHighPerformance<T>(() => new T()));

    public static IObjectPool<T> Shared => s_pool.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Get() => Shared.Rent();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(T item) => Shared.Return(item);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IPoolable<T> GetPoolable() => Shared.GetPoolable();
}

public static class ListPool<T>
{
    private static readonly Lazy<IObjectPool<List<T>>> s_pool = new(
        () => ObjectPoolFactory.CreateNativeAot<List<T>>(() => new List<T>(), list => list.Clear()));

    public static IObjectPool<List<T>> Shared => s_pool.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<T> Get() => Shared.Rent();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(List<T> list) => Shared.Return(list);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IPoolable<List<T>> GetPoolable() => Shared.GetPoolable();
}

public static class StringBuilderPool
{
    private static readonly Lazy<IObjectPool<Text.StringBuilder>> s_pool = new(
        () => ObjectPoolFactory.CreateNativeAot<Text.StringBuilder>(
            () => new Text.StringBuilder(1024),
            sb => sb.Clear()));

    public static IObjectPool<Text.StringBuilder> Shared => s_pool.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Text.StringBuilder Get() => Shared.Rent();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(Text.StringBuilder sb) => Shared.Return(sb);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetStringAndReturn(Text.StringBuilder sb)
    {
        string result = sb.ToString();
        Shared.Return(sb);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IPoolable<Text.StringBuilder> GetPoolable() => Shared.GetPoolable();
}

public static class ByteArrayPool
{
    private static readonly Lazy<ArrayPool<byte>> s_pool = new(
        () => ArrayPool<byte>.Create(1024 * 1024, 50));

    public static ArrayPool<byte> Shared => s_pool.Value;
}

public static class MemoryStreamPool
{
    private static readonly Lazy<IObjectPool<IO.MemoryStream>> s_pool = new(
        () => ObjectPoolFactory.CreateNativeAot<IO.MemoryStream>(
            () => new IO.MemoryStream(4096),
            ms => { ms.Position = 0; ms.SetLength(0); }));

    public static IObjectPool<IO.MemoryStream> Shared => s_pool.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IO.MemoryStream Get() => Shared.Rent();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(IO.MemoryStream ms) => Shared.Return(ms);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IPoolable<IO.MemoryStream> GetPoolable() => Shared.GetPoolable();
}
