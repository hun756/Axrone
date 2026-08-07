using System.Runtime.CompilerServices;
using Axrone.Utility.Singleton;
using SingletonFacade = Axrone.Utility.Singleton.Singleton;

namespace Axrone.Utility.Tests.SingletonTests;

#region Test Types

public sealed class SimpleService
{
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    private static int _nextId;
}

public sealed class InitializableService : IInitializable
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

public struct GameConfig : IValueInitializable, IEquatable<GameConfig>
{
    public int MaxPlayers { get; private set; }
    public void Initialize() => MaxPlayers = 4;
    public bool Equals(GameConfig other) => MaxPlayers == other.MaxPlayers;
    public override bool Equals(object? obj) => obj is GameConfig g && Equals(g);
    public override int GetHashCode() => MaxPlayers;
}

public struct PlainStruct : IEquatable<PlainStruct>
{
    public int Value { get; set; }
    public bool Equals(PlainStruct other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is PlainStruct p && Equals(p);
    public override int GetHashCode() => Value;
}

public sealed class DisposableService : IDisposable
{
    private Action _onDispose;
    public DisposableService() => _onDispose = () => { };
    public DisposableService(Action onDispose) => _onDispose = onDispose;
    public void Dispose() => _onDispose();
}

public sealed class FactoryService;
public sealed class FactoryCountService;

public sealed class NamedService : NamedSingleton<NamedService>
{
    public string? InitName { get; private set; }
    protected override void Initialize(string name) => InitName = name;
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

        // After deactivation, Current should be restored to whatever it was before
        // (may not be null if another test set it via AsyncLocal)
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
}

public class SingletonRegistryTests
{
    [Fact]
    public void GetOrAdd_ReturnsSameInstance()
    {
        SingletonRegistry.Clear();
        var a = SingletonRegistry.GetOrAdd<SimpleService>();
        var b = SingletonRegistry.GetOrAdd<SimpleService>();
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void GetOrAdd_WithFactory_UsesFactory()
    {
        SingletonRegistry.Clear();
        var expected = new SimpleService();
        var actual = SingletonRegistry.GetOrAdd(() => expected);
        actual.Should().BeSameAs(expected);
    }

    [Fact]
    public void Register_PreCreatedInstance()
    {
        SingletonRegistry.Clear();
        var expected = new SimpleService();
        SingletonRegistry.Register(expected);
        SingletonRegistry.GetOrAdd<SimpleService>().Should().BeSameAs(expected);
    }

    [Fact]
    public void IsRegistered_TrueAfterAccess()
    {
        SingletonRegistry.Clear();
        SingletonRegistry.IsRegistered<SimpleService>().Should().BeFalse();
        _ = SingletonRegistry.GetOrAdd<SimpleService>();
        SingletonRegistry.IsRegistered<SimpleService>().Should().BeTrue();
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenNotCreated()
    {
        SingletonRegistry.Clear();
        SingletonRegistry.TryGet<SimpleService>(out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Clear_RemovesInstances()
    {
        SingletonRegistry.Clear();
        _ = SingletonRegistry.GetOrAdd<SimpleService>();
        SingletonRegistry.IsRegistered<SimpleService>().Should().BeTrue();
        SingletonRegistry.Clear();
        SingletonRegistry.IsRegistered<SimpleService>().Should().BeFalse();
    }
}

public class ValueSingletonTests
{
    [Fact]
    public void Value_ReturnsInitializedStruct()
    {
        ref readonly var config = ref ValueSingleton<GameConfig>.Value;
        config.MaxPlayers.Should().Be(4);
    }

    [Fact]
    public void Value_ReturnsSameRef()
    {
        ref readonly var a = ref ValueSingleton<GameConfig>.Value;
        ref readonly var b = ref ValueSingleton<GameConfig>.Value;
        Unsafe.AreSame(ref Unsafe.AsRef(in a), ref Unsafe.AsRef(in b)).Should().BeTrue();
    }

    [Fact]
    public void IsInitialized_TrueAfterAccess()
    {
        _ = ValueSingleton<GameConfig>.Value;
        ValueSingleton<GameConfig>.IsInitialized.Should().BeTrue();
    }
}

public class NamedSingletonTests
{
    [Fact]
    public void GetInstance_SameKey_ReturnsSameInstance()
    {
        NamedService.Clear();
        var a = NamedService.GetInstance("primary");
        var b = NamedService.GetInstance("primary");
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void GetInstance_DifferentKeys_ReturnsDifferentInstances()
    {
        NamedService.Clear();
        var a = NamedService.GetInstance("primary");
        var b = NamedService.GetInstance("secondary");
        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void Contains_TrueAfterCreation()
    {
        NamedService.Clear();
        NamedService.Contains("test").Should().BeFalse();
        _ = NamedService.GetInstance("test");
        NamedService.Contains("test").Should().BeTrue();
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenMissing()
    {
        NamedService.Clear();
        NamedService.TryGet("missing", out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void NullName_Throws()
    {
        var act = () => NamedService.GetInstance(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EmptyName_Throws()
    {
        var act = () => NamedService.GetInstance("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Initialize_CalledWithName()
    {
        NamedService.Clear();
        var instance = NamedService.GetInstance("mydb");
        instance.InitName.Should().Be("mydb");
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
