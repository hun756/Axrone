using Axrone.Utility.Singleton;
using SingletonFacade = Axrone.Utility.Singleton.Singleton;

namespace Axrone.Utility.Tests.SingletonTests;

#region Test Types

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

#endregion

public class SingletonFacadeTests
{
    [Fact]
    public void GetInstance_ReturnsSameInstance()
    {
        var a = SingletonFacade.GetInstance<SimpleService>();
        var b = SingletonFacade.GetInstance<SimpleService>();
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void GetInstance_WithFactory_UsesFactory()
    {
        var expected = new FactoryService();
        var instance = SingletonFacade.GetInstance(() => expected);
        instance.Should().BeSameAs(expected);
    }

    [Fact]
    public void GetInstance_WithFactory_ReturnsSameOnSecondCall()
    {
        int callCount = 0;
        var a = SingletonFacade.GetInstance(() => { callCount++; return new FactoryCountService(); });
        var b = SingletonFacade.GetInstance(() => { callCount++; return new FactoryCountService(); });
        a.Should().BeSameAs(b);
        callCount.Should().Be(1);
    }

    [Fact]
    public void WarmUp_ForcesInitialization()
    {
        SingletonFacade.WarmUp<InitializableService>();
        SingletonFacade.IsInitialized<InitializableService>().Should().BeTrue();
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenNotCreated()
    {
        SingletonFacade.TryGet<SimpleService>(out var result).Should().BeFalse();
    }

    [Fact]
    public void Register_PreCreatedInstance()
    {
        var expected = new DisposableService();
        SingletonFacade.Register(expected);
        SingletonFacade.TryGet<DisposableService>(out var result).Should().BeTrue();
        result.Should().BeSameAs(expected);
    }
}

public class SingletonBaseTests
{
    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        var a = CrtpService.Instance;
        var b = CrtpService.Instance;
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void Instance_CreatedOnlyOnce()
    {
        CrtpService.ResetCounter();
        _ = CrtpService.Instance;
        _ = CrtpService.Instance;
        _ = CrtpService.Instance;
        CrtpService.Instance.CreateCount.Should().Be(1);
    }

    [Fact]
    public void OnCreated_CalledOnInit()
    {
        var instance = CrtpWithOnCreated.Instance;
        instance.OnCreatedCalled.Should().BeTrue();
    }

    [Fact]
    public void WarmUp_ForcesInit()
    {
        CrtpService.WarmUp();
        CrtpService.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void DirectConstruction_Throws()
    {
        var act = () => new CrtpService();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be accessed through the Instance property*");
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var instance = CrtpWithOnCreated.Instance;
        instance.Dispose();
        instance.Dispose();
    }
}

public class AsyncSingletonTests
{
    [Fact]
    public async Task GetInstanceAsync_ReturnsSameValue()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => new ValueTask<SimpleService>(new SimpleService()));

        var a = await singleton.GetInstanceAsync();
        var b = await singleton.GetInstanceAsync();
        a.Should().BeSameAs(b);
    }

    [Fact]
    public async Task IsInitialized_TrueAfterSuccess()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => new ValueTask<SimpleService>(new SimpleService()));

        singleton.IsInitialized.Should().BeFalse();
        await singleton.GetInstanceAsync();
        singleton.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task Fault_CachesException()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => throw new InvalidOperationException("fail"));

        await singleton.Invoking(s => s.GetInstanceAsync().AsTask())
            .Should().ThrowAsync<InvalidOperationException>();

        singleton.IsFaulted.Should().BeTrue();
        singleton.IsInitialized.Should().BeFalse();
    }
}

public class SingletonScopeTests
{
    [Fact]
    public void Register_Resolve_ReturnsSameInstance()
    {
        using var scope = new SingletonScope("test");
        scope.Register<SimpleService>("svc", () => new SimpleService());

        var a = scope.Resolve<SimpleService>("svc");
        var b = scope.Resolve<SimpleService>("svc");
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void Resolve_UnknownKey_ThrowsKeyNotFound()
    {
        using var scope = new SingletonScope("test");
        scope.Invoking(s => s.Resolve<SimpleService>("missing"))
            .Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Child_FallsBackToParent()
    {
        using var parent = new SingletonScope("parent");
        parent.Register<SimpleService>("svc", () => new SimpleService());

        using var child = parent.CreateChild("child");
        var instance = child.Resolve<SimpleService>("svc");
        instance.Should().NotBeNull();
    }

    [Fact]
    public void TryResolve_ReturnsFalse_WhenMissing()
    {
        using var scope = new SingletonScope("test");
        scope.TryResolve<SimpleService>("missing", out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Activate_SetsCurrent()
    {
        using var scope = new SingletonScope("ambient");

        using (scope.Activate())
        {
            SingletonScope.Current.Should().BeSameAs(scope);
        }

        SingletonScope.Current.Should().NotBeSameAs(scope);
    }

    [Fact]
    public void Disposer_CalledOnDispose()
    {
        bool disposed = false;
        var scope = new SingletonScope("test");
        scope.Register<SimpleService>("svc", () => new SimpleService(), _ => disposed = true);
        _ = scope.Resolve<SimpleService>("svc");
        scope.Dispose();
        disposed.Should().BeTrue();
    }

    [Fact]
    public void TypedRegister_Resolve_ReturnsSameInstance()
    {
        using var scope = new SingletonScope("typed-test");
        scope.Register<SimpleService>(() => new SimpleService());

        using (scope.Activate())
        {
            var a = SingletonScope.Resolve<SimpleService>();
            var b = SingletonScope.Resolve<SimpleService>();
            a.Should().BeSameAs(b);
        }
    }

    [Fact]
    public void TypedResolve_UnknownType_ThrowsKeyNotFound()
    {
        using var scope = new SingletonScope("typed-missing");
        using (scope.Activate())
        {
            scope.Invoking(_ => SingletonScope.Resolve<FactoryService>())
                .Should().Throw<KeyNotFoundException>();
        }
    }
}

public class KeyedSingletonTests
{
    [Fact]
    public void GetInstance_SameKey_ReturnsSameInstance()
    {
        KeyedSession.Clear();
        var a = KeyedSession.GetInstance("primary");
        var b = KeyedSession.GetInstance("primary");
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void GetInstance_DifferentKeys_ReturnsDifferentInstances()
    {
        KeyedSession.Clear();
        var a = KeyedSession.GetInstance("primary");
        var b = KeyedSession.GetInstance("secondary");
        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void Key_IsSetOnCreation()
    {
        KeyedSession.Clear();
        var instance = KeyedSession.GetInstance("mykey");
        instance.Key.Should().Be("mykey");
    }

    [Fact]
    public void Lifecycle_Initialize_CalledWithKey()
    {
        KeyedSession.Clear();
        var instance = KeyedSession.GetInstance("testkey");
        instance.InitName.Should().Be("testkey");
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenMissing()
    {
        KeyedSession.Clear();
        KeyedSession.TryGet("missing", out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Count_ReflectsActiveInstances()
    {
        KeyedSession.Clear();
        KeyedSession.Count.Should().Be(0);
        _ = KeyedSession.GetInstance("a");
        _ = KeyedSession.GetInstance("b");
        KeyedSession.Count.Should().Be(2);
    }

    [Fact]
    public void TryRemove_DisposesInstance()
    {
        KeyedSession.Clear();
        _ = KeyedSession.GetInstance("removable");
        KeyedSession.TryRemove("removable").Should().BeTrue();
        KeyedSession.Count.Should().Be(0);
    }
}

public class WeakSingletonTests
{
    [Fact]
    public void GetInstance_ReturnsInstance()
    {
        WeakSingleton<SimpleService>.Release();
        var instance = WeakSingleton<SimpleService>.GetInstance();
        instance.Should().NotBeNull();
    }

    [Fact]
    public void GetInstance_SameInstance_WhenAlive()
    {
        WeakSingleton<SimpleService>.Release();
        var a = WeakSingleton<SimpleService>.GetInstance();
        var b = WeakSingleton<SimpleService>.GetInstance();
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void TryGet_FalseAfterRelease()
    {
        WeakSingleton<SimpleService>.Release();
        WeakSingleton<SimpleService>.TryGet(out _).Should().BeFalse();
    }
}

public class SingletonCreationExceptionTests
{
    [Fact]
    public void ContainsTargetType()
    {
        var ex = new SingletonCreationException("fail", typeof(SimpleService));
        ex.TargetType.Should().Be<SimpleService>();
        ex.Message.Should().Be("fail");
    }

    [Fact]
    public void WrapsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new SingletonCreationException("outer", typeof(SimpleService), inner);
        ex.InnerException.Should().BeSameAs(inner);
    }
}

public class SingletonServiceProviderTests
{
    [Fact]
    public void Register_GetService_RoundTrips()
    {
        var provider = SingletonServiceProvider.Instance;
        var service = new FactoryService();
        provider.Register(service);
        provider.GetService<FactoryService>().Should().BeSameAs(service);
    }

    [Fact]
    public void GetService_Throws_WhenNotRegistered()
    {
        var provider = SingletonServiceProvider.Instance;
        provider.Invoking(p => p.GetService<CrtpService>())
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryGetService_ReturnsFalse_WhenMissing()
    {
        var provider = SingletonServiceProvider.Instance;
        provider.TryGetService<FactoryCountService>(out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void GetOrCreateService_CreatesOnFirstCall()
    {
        var provider = SingletonServiceProvider.Instance;
        var a = provider.GetOrCreateService<SimpleService>();
        var b = provider.GetOrCreateService<SimpleService>();
        a.Should().BeSameAs(b);
    }
}
