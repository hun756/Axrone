using Axrone.Utility.Singleton;

namespace Axrone.Utility.Tests.Singleton;

public sealed class SimpleService
{
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    private static int _nextId;
}

public sealed class InitializableService : ISingletonLifecycle
{
    public bool WasInitialized { get; private set; }
    public void Initialize() => WasInitialized = true;
}

public sealed class CrtpService : SingletonBase<CrtpService>
{
    public int CreateCount { get; }
    private static int _createCount;

    public CrtpService()
    {
        Interlocked.Increment(ref _createCount);
        CreateCount = _createCount;
    }

    public static void ResetCounter() => Volatile.Write(ref _createCount, 0);
}

public sealed class CrtpWithOnCreated : SingletonBase<CrtpWithOnCreated>
{
    public bool OnCreatedCalled { get; private set; }
    protected override void OnCreated() => OnCreatedCalled = true;
}

public sealed class DisposableService : IDisposable
{
    public bool WasDisposed { get; private set; }
    public void Dispose() => WasDisposed = true;
}

public sealed class FactoryService;
public sealed class FactoryCountService;

public sealed class KeyedSession : KeyedSingleton<KeyedSession, string>, IKeyed<string>, ISingletonLifecycle
{
    public string Key { get; set; } = "";
    public string? InitName { get; private set; }

    protected override void OnCreated() { }

    void ISingletonLifecycle.Initialize() => InitName = Key;
}

public sealed class AsyncDisposableService : IAsyncDisposable
{
    public bool WasDisposed { get; private set; }
    public ValueTask DisposeAsync()
    {
        WasDisposed = true;
        return default;
    }
}

[Singleton(SingletonLifetime.Singleton, Monitored = true)]
public sealed class MonitoredService
{
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    private static int _nextId;
}

[Singleton(SingletonLifetime.Singleton, ThreadSafety = SingletonThreadSafety.ThreadStatic)]
public sealed class ThreadStaticService
{
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    private static int _nextId;
}

[Singleton(SingletonLifetime.Singleton, ThreadSafety = SingletonThreadSafety.Weak)]
public sealed class WeakStrategyService
{
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    private static int _nextId;
}

public sealed class ThrowingService
{
    public ThrowingService() => throw new InvalidOperationException("construction failed");
}

public sealed class UntouchedService;

public sealed class UniqueFactoryCountService;
public sealed class UniqueFactoryService;
public sealed class UniqueInitializableService : ISingletonLifecycle
{
    public bool WasInitialized { get; private set; }
    public void Initialize() => WasInitialized = true;
}
