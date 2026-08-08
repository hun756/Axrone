using Axrone.Memory.Lifetime;
using Axrone.Memory.Lifetime.Internal;
using SingletonFacade = Axrone.Memory.Lifetime.Singleton;

namespace Axrone.Memory.Tests;

public sealed class FactoryLifecycleService : ISingletonLifecycle
{
    public bool InitializeCalled { get; private set; }
    public void Initialize() => InitializeCalled = true;
}

public sealed class FactoryNullService;

public class FactoryProviderTests
{
    [Fact]
    public void GetOrCreateInstance_ReturnsSameInstance_OnSecondCall()
    {
        var a = SingletonFacade.GetInstance(() => new FactoryService());
        var b = SingletonFacade.GetInstance(() => new FactoryService());
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void GetOrCreateInstance_InvokesFactoryOnlyOnce()
    {
        int callCount = 0;
        var a = SingletonFacade.GetInstance(() => { callCount++; return new FactoryCountService(); });
        var b = SingletonFacade.GetInstance(() => { callCount++; return new FactoryCountService(); });
        a.Should().BeSameAs(b);
        callCount.Should().Be(1);
    }

    [Fact]
    public void GetOrCreateInstance_DispatchesLifecycleInitialize()
    {
        var instance = SingletonFacade.GetInstance(() => new FactoryLifecycleService());
        instance.InitializeCalled.Should().BeTrue();
    }

    [Fact]
    public void GetOrCreateInstance_NullFactoryResult_ThrowsSingletonCreationException()
    {
        var act = () => SingletonFacade.GetInstance<FactoryNullService>(() => null!);
        act.Should().Throw<SingletonCreationException>()
            .WithMessage("*Factory method returned null*");
    }

    [Fact]
    public void GetOrCreateInstance_FactoryThrows_ThrowsSingletonCreationException()
    {
        var act = () => SingletonFacade.GetInstance<ThrowingService>(() => throw new InvalidOperationException("boom"));
        act.Should().Throw<SingletonCreationException>()
            .WithInnerException<InvalidOperationException>()
            .WithMessage("*boom*");
    }

    [Fact]
    public void GetOrCreateInstance_RegistersInRegistry()
    {
        var instance = SingletonFacade.GetInstance(() => new FactoryService());
        SingletonRegistryInternal.TryGet<FactoryService>(out var resolved).Should().BeTrue();
        resolved.Should().BeSameAs(instance);
    }
}
