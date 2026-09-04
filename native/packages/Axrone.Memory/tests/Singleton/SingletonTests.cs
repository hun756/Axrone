using Enterprise.Patterns.Singleton;

namespace Axrone.Memory.Tests;

public sealed class SingletonTests : IDisposable
{
    public void Dispose()
    {
        Singleton<SimpleService>.Reset();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void SetFactory_ThenAccess_ReturnsSameInstance()
    {
        Singleton<SimpleService>.SetFactory(() => new SimpleService());
        var a = Singleton<SimpleService>.Instance;
        var b = Singleton<SimpleService>.Instance;
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void SetInstance_ThenAccess_ReturnsExactInstance()
    {
        var expected = new SimpleService();
        Singleton<SimpleService>.SetInstance(expected);
        Singleton<SimpleService>.Instance.Should().BeSameAs(expected);
    }

    [Fact]
    public void Instance_BeforeConfiguration_ThrowsSingletonInitializationException()
    {
        var act = () => Singleton<UntouchedService>.Instance;
        act.Should().Throw<SingletonInitializationException>();
    }

    [Fact]
    public void SetFactory_AfterInitialized_ThrowsSingletonAlreadyInitializedException()
    {
        Singleton<SimpleService>.SetFactory(() => new SimpleService());
        _ = Singleton<SimpleService>.Instance;

        var act = () => Singleton<SimpleService>.SetFactory(() => new SimpleService());
        act.Should().Throw<SingletonAlreadyInitializedException>();
    }

    [Fact]
    public void IsInitialized_TrueAfterAccess()
    {
        Singleton<SimpleService>.SetFactory(() => new SimpleService());
        Singleton<SimpleService>.IsInitialized.Should().BeFalse();
        _ = Singleton<SimpleService>.Instance;
        Singleton<SimpleService>.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void Reset_AllowsReconfiguration()
    {
        Singleton<SimpleService>.SetFactory(() => new SimpleService());
        var first = Singleton<SimpleService>.Instance;
        Singleton<SimpleService>.Reset();

        Singleton<SimpleService>.SetFactory(() => new SimpleService());
        var second = Singleton<SimpleService>.Instance;
        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public void Reset_DisposesDisposableInstance()
    {
        var service = new DisposableService();
        Singleton<DisposableService>.SetInstance(service);
        Singleton<DisposableService>.Reset();
        service.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public void SetFactory_NullFactory_ThrowsArgumentNullException()
    {
        var act = () => Singleton<SimpleService>.SetFactory(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetInstance_NullInstance_ThrowsArgumentNullException()
    {
        var act = () => Singleton<SimpleService>.SetInstance(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Factory_Throws_TransitionsToFaultedState()
    {
        Singleton<ThrowingService>.SetFactory(() => throw new InvalidOperationException("fail"));
        var act = () => Singleton<ThrowingService>.Instance;
        act.Should().Throw<SingletonInitializationException>();
    }
}
