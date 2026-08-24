using Text = System.Text;
using IO = System.IO;

namespace Axrone.ObjectPool;

/// <summary>
/// Provides factory methods for creating <see cref="IObjectPool{T}"/> instances with
/// various configurations and performance profiles.
/// </summary>
public static class ObjectPoolFactory
{
    /// <summary>
    /// Creates an object pool with the specified factory, initial capacity, and maximum capacity.
    /// </summary>
    /// <typeparam name="T">The pooled reference type.</typeparam>
    /// <param name="factory">Delegate that creates new instances of <typeparamref name="T"/>.</param>
    /// <param name="initialCapacity">Number of objects to pre-allocate. Defaults to 0.</param>
    /// <param name="maxCapacity">Maximum number of idle objects retained in the pool. 0 means no limit.</param>
    /// <returns>A new <see cref="IObjectPool{T}"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObjectPool<T> Create<T>(
        Func<T> factory,
        int initialCapacity = 0,
        int maxCapacity = 0)
        where T : class
    {
        return new ObjectPool<T>(factory, null, null, initialCapacity, maxCapacity);
    }

    /// <summary>
    /// Creates an object pool with a factory and a reset action invoked each time an object
    /// is returned to the pool.
    /// </summary>
    /// <typeparam name="T">The pooled reference type.</typeparam>
    /// <param name="factory">Delegate that creates new instances of <typeparamref name="T"/>.</param>
    /// <param name="reset">Delegate that resets an object's state before it is stored in the pool.</param>
    /// <param name="initialCapacity">Number of objects to pre-allocate. Defaults to 0.</param>
    /// <param name="maxCapacity">Maximum number of idle objects retained in the pool. 0 means no limit.</param>
    /// <returns>A new <see cref="IObjectPool{T}"/> instance.</returns>
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

    /// <summary>
    /// Creates an object pool optimized for high-throughput scenarios using
    /// <see cref="PoolConfiguration.HighPerformance"/> settings.
    /// </summary>
    /// <typeparam name="T">The pooled reference type.</typeparam>
    /// <param name="factory">Delegate that creates new instances of <typeparamref name="T"/>.</param>
    /// <returns>A new <see cref="IObjectPool{T}"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObjectPool<T> CreateHighPerformance<T>(Func<T> factory)
        where T : class
    {
        return new ObjectPool<T>(
            PoolConfiguration.HighPerformance,
            new PoolableHandler<T> { Factory = factory });
    }

    /// <summary>
    /// Creates an object pool optimized for Native AOT compilation using
    /// <see cref="PoolConfiguration.NativeAotOptimized"/> settings.
    /// </summary>
    /// <typeparam name="T">The pooled reference type.</typeparam>
    /// <param name="factory">Delegate that creates new instances of <typeparamref name="T"/>.</param>
    /// <returns>A new <see cref="IObjectPool{T}"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObjectPool<T> CreateNativeAot<T>(Func<T> factory)
        where T : class
    {
        return new ObjectPool<T>(
            PoolConfiguration.NativeAotOptimized,
            new PoolableHandler<T> { Factory = factory });
    }

    /// <summary>
    /// Creates a Native AOT-optimized object pool with a reset action invoked when objects
    /// are returned to the pool.
    /// </summary>
    /// <typeparam name="T">The pooled reference type.</typeparam>
    /// <param name="factory">Delegate that creates new instances of <typeparamref name="T"/>.</param>
    /// <param name="reset">Delegate that resets an object's state before it is stored in the pool.</param>
    /// <returns>A new <see cref="IObjectPool{T}"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObjectPool<T> CreateNativeAot<T>(Func<T> factory, Action<T> reset)
        where T : class
    {
        return new ObjectPool<T>(
            PoolConfiguration.NativeAotOptimized,
            new PoolableHandler<T> { Factory = factory, Reset = reset });
    }

    /// <summary>
    /// Creates an object pool configured to minimize memory usage via
    /// <see cref="PoolConfiguration.MemoryConserving"/> settings.
    /// </summary>
    /// <typeparam name="T">The pooled reference type.</typeparam>
    /// <param name="factory">Delegate that creates new instances of <typeparamref name="T"/>.</param>
    /// <returns>A new <see cref="IObjectPool{T}"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObjectPool<T> CreateMemoryOptimized<T>(Func<T> factory)
        where T : class
    {
        return new ObjectPool<T>(
            PoolConfiguration.MemoryConserving,
            new PoolableHandler<T> { Factory = factory });
    }

    /// <summary>
    /// Creates an object pool with default configuration and a custom <see cref="PoolableHandler{T}"/>
    /// that specifies factory, reset, and dispose callbacks.
    /// </summary>
    /// <typeparam name="T">The pooled reference type.</typeparam>
    /// <param name="factory">Delegate that creates new instances of <typeparamref name="T"/>.</param>
    /// <param name="reset">Optional delegate that resets an object's state on return. Defaults to <c>null</c>.</param>
    /// <param name="onDispose">Optional delegate invoked when a pooled object is permanently discarded. Defaults to <c>null</c>.</param>
    /// <returns>A new <see cref="IObjectPool{T}"/> instance.</returns>
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

    /// <summary>
    /// Creates an object pool that uses thread-local storage to reduce contention in
    /// highly concurrent scenarios. Maximum capacity defaults to
    /// <c>Environment.ProcessorCount * 32</c> when <paramref name="maxCapacity"/> is 0.
    /// </summary>
    /// <typeparam name="T">The pooled reference type.</typeparam>
    /// <param name="factory">Delegate that creates new instances of <typeparamref name="T"/>.</param>
    /// <param name="initialCapacity">Number of objects to pre-allocate per thread. Defaults to 0.</param>
    /// <param name="maxCapacity">Maximum number of idle objects retained per thread. 0 uses the default.</param>
    /// <returns>A new <see cref="IObjectPool{T}"/> instance.</returns>
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

/// <summary>
/// A process-wide shared object pool for types with a parameterless constructor.
/// Lazily initialized on first access using <see cref="ObjectPoolFactory.CreateHighPerformance{T}"/>.
/// </summary>
/// <typeparam name="T">The pooled reference type, which must have a public parameterless constructor.</typeparam>
public static class StaticPool<T> where T : class, new()
{
    private static readonly Lazy<IObjectPool<T>> s_pool = new(
        () => ObjectPoolFactory.CreateHighPerformance<T>(() => new T()));

    /// <summary>
    /// Gets the shared <see cref="IObjectPool{T}"/> instance for <typeparamref name="T"/>.
    /// </summary>
    public static IObjectPool<T> Shared => s_pool.Value;

    /// <summary>
    /// Rents an instance of <typeparamref name="T"/> from the shared pool.
    /// </summary>
    /// <returns>A pooled object of type <typeparamref name="T"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Get() => Shared.Rent();

    /// <summary>
    /// Returns a previously rented instance to the shared pool.
    /// </summary>
    /// <param name="item">The object to return.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(T item) => Shared.Return(item);

    /// <summary>
    /// Rents a <see cref="IPoolable{T}"/> wrapper from the shared pool, enabling automatic
    /// return via <c>using</c> or <c>await using</c>.
    /// </summary>
    /// <returns>An <see cref="IPoolable{T}"/> wrapping the rented object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IPoolable<T> GetPoolable() => Shared.GetPoolable();
}

/// <summary>
/// A process-wide shared pool of <see cref="List{T}"/> instances. Lists are cleared
/// automatically when returned to the pool.
/// </summary>
/// <typeparam name="T">The element type of the pooled lists.</typeparam>
public static class ListPool<T>
{
    private static readonly Lazy<IObjectPool<List<T>>> s_pool = new(
        () => ObjectPoolFactory.CreateNativeAot<List<T>>(() => new List<T>(), list => list.Clear()));

    /// <summary>
    /// Gets the shared <see cref="IObjectPool{T}"/> for <see cref="List{T}"/> instances.
    /// </summary>
    public static IObjectPool<List<T>> Shared => s_pool.Value;

    /// <summary>
    /// Rents a <see cref="List{T}"/> from the shared pool. The list is empty.
    /// </summary>
    /// <returns>A cleared, pooled list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<T> Get() => Shared.Rent();

    /// <summary>
    /// Returns a previously rented list to the shared pool.
    /// </summary>
    /// <param name="list">The list to return.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(List<T> list) => Shared.Return(list);

    /// <summary>
    /// Rents a <see cref="IPoolable{T}"/> wrapper for a <see cref="List{T}"/>, enabling automatic
    /// return via <c>using</c> or <c>await using</c>.
    /// </summary>
    /// <returns>An <see cref="IPoolable{T}"/> wrapping the rented list.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IPoolable<List<T>> GetPoolable() => Shared.GetPoolable();
}

/// <summary>
/// A process-wide shared pool of <see cref="Text.StringBuilder"/> instances.
/// Builders are cleared automatically when returned to the pool.
/// </summary>
public static class StringBuilderPool
{
    private static readonly Lazy<IObjectPool<Text.StringBuilder>> s_pool = new(
        () => ObjectPoolFactory.CreateNativeAot<Text.StringBuilder>(
            () => new Text.StringBuilder(1024),
            sb => sb.Clear()));

    /// <summary>
    /// Gets the shared <see cref="IObjectPool{T}"/> for <see cref="Text.StringBuilder"/> instances.
    /// </summary>
    public static IObjectPool<Text.StringBuilder> Shared => s_pool.Value;

    /// <summary>
    /// Rents a <see cref="Text.StringBuilder"/> from the shared pool. The builder is empty.
    /// </summary>
    /// <returns>A cleared, pooled <see cref="Text.StringBuilder"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Text.StringBuilder Get() => Shared.Rent();

    /// <summary>
    /// Returns a previously rented <see cref="Text.StringBuilder"/> to the shared pool.
    /// </summary>
    /// <param name="sb">The <see cref="Text.StringBuilder"/> to return.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(Text.StringBuilder sb) => Shared.Return(sb);

    /// <summary>
    /// Converts the <see cref="Text.StringBuilder"/> to a <see cref="string"/> and returns
    /// the builder to the pool in a single operation.
    /// </summary>
    /// <param name="sb">The <see cref="Text.StringBuilder"/> to convert and return.</param>
    /// <returns>The string representation of the builder's contents.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetStringAndReturn(Text.StringBuilder sb)
    {
        string result = sb.ToString();
        Shared.Return(sb);
        return result;
    }

    /// <summary>
    /// Rents a <see cref="IPoolable{T}"/> wrapper for a <see cref="Text.StringBuilder"/>,
    /// enabling automatic return via <c>using</c> or <c>await using</c>.
    /// </summary>
    /// <returns>An <see cref="IPoolable{T}"/> wrapping the rented <see cref="Text.StringBuilder"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IPoolable<Text.StringBuilder> GetPoolable() => Shared.GetPoolable();
}

/// <summary>
/// A process-wide shared <see cref="ArrayPool{T}"/> for <see cref="byte"/> arrays.
/// Backed by <see cref="ArrayPool{T}.Create(int, int)"/> with a max array length of 1 MiB
/// and a max arrays-per-bucket count of 50.
/// </summary>
public static class ByteArrayPool
{
    private static readonly Lazy<ArrayPool<byte>> s_pool = new(
        () => ArrayPool<byte>.Create(1024 * 1024, 50));

    /// <summary>
    /// Gets the shared <see cref="ArrayPool{T}"/> for <see cref="byte"/> arrays.
    /// </summary>
    public static ArrayPool<byte> Shared => s_pool.Value;
}

/// <summary>
/// A process-wide shared pool of <see cref="IO.MemoryStream"/> instances.
/// Streams are reset (position and length set to 0) when returned to the pool.
/// </summary>
public static class MemoryStreamPool
{
    private static readonly Lazy<IObjectPool<IO.MemoryStream>> s_pool = new(
        () => ObjectPoolFactory.CreateNativeAot<IO.MemoryStream>(
            () => new IO.MemoryStream(4096),
            ms => { ms.Position = 0; ms.SetLength(0); }));

    /// <summary>
    /// Gets the shared <see cref="IObjectPool{T}"/> for <see cref="IO.MemoryStream"/> instances.
    /// </summary>
    public static IObjectPool<IO.MemoryStream> Shared => s_pool.Value;

    /// <summary>
    /// Rents a <see cref="IO.MemoryStream"/> from the shared pool. The stream is empty.
    /// </summary>
    /// <returns>A reset, pooled <see cref="IO.MemoryStream"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IO.MemoryStream Get() => Shared.Rent();

    /// <summary>
    /// Returns a previously rented <see cref="IO.MemoryStream"/> to the shared pool.
    /// </summary>
    /// <param name="ms">The <see cref="IO.MemoryStream"/> to return.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Return(IO.MemoryStream ms) => Shared.Return(ms);

    /// <summary>
    /// Rents a <see cref="IPoolable{T}"/> wrapper for a <see cref="IO.MemoryStream"/>,
    /// enabling automatic return via <c>using</c> or <c>await using</c>.
    /// </summary>
    /// <returns>An <see cref="IPoolable{T}"/> wrapping the rented <see cref="IO.MemoryStream"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IPoolable<IO.MemoryStream> GetPoolable() => Shared.GetPoolable();
}
