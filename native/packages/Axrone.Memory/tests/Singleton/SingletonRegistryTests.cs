using Enterprise.Patterns.Singleton;

namespace Axrone.Memory.Tests;

public class SingletonRegistryTests
{
    [Fact]
    public void Register_WithFactory_CreatesOnGet()
    {
        var registry = new SingletonRegistry();
        registry.Register<SimpleService>(() => new SimpleService());
        var instance = registry.Get<SimpleService>();
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Register_WithFactory_ReturnsSameOnSecondGet()
    {
        var registry = new SingletonRegistry();
        registry.Register<SimpleService>(() => new SimpleService());
        var a = registry.Get<SimpleService>();
        var b = registry.Get<SimpleService>();
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void RegisterInstance_ReturnsExactInstance()
    {
        var registry = new SingletonRegistry();
        var expected = new SimpleService();
        registry.RegisterInstance(expected);
        registry.Get<SimpleService>().Should().BeSameAs(expected);
    }

    [Fact]
    public void Register_WithTypeConstraint_CreatesInstance()
    {
        var registry = new SingletonRegistry();
        registry.Register<SimpleService>();
        var instance = registry.Get<SimpleService>();
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Register_DuplicateType_ThrowsSingletonAlreadyInitializedException()
    {
        var registry = new SingletonRegistry();
        registry.Register<SimpleService>(() => new SimpleService());
        var act = () => registry.Register<SimpleService>(() => new SimpleService());
        act.Should().Throw<SingletonAlreadyInitializedException>();
    }

    [Fact]
    public void Get_UnregisteredType_ThrowsKeyNotFoundException()
    {
        var registry = new SingletonRegistry();
        var act = () => registry.Get<UntouchedService>();
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void TryGet_RegisteredType_ReturnsTrue()
    {
        var registry = new SingletonRegistry();
        registry.Register<SimpleService>(() => new SimpleService());
        registry.TryGet<SimpleService>(out var instance).Should().BeTrue();
        instance.Should().NotBeNull();
    }

    [Fact]
    public void TryGet_UnregisteredType_ReturnsFalse()
    {
        var registry = new SingletonRegistry();
        registry.TryGet<UntouchedService>(out var instance).Should().BeFalse();
        instance.Should().BeNull();
    }

    [Fact]
    public void Contains_RegisteredType_ReturnsTrue()
    {
        var registry = new SingletonRegistry();
        registry.Register<SimpleService>(() => new SimpleService());
        registry.Contains<SimpleService>().Should().BeTrue();
    }

    [Fact]
    public void Contains_UnregisteredType_ReturnsFalse()
    {
        var registry = new SingletonRegistry();
        registry.Contains<UntouchedService>().Should().BeFalse();
    }

    [Fact]
    public void Dispose_TracksAndDisposesInstances()
    {
        var service = new DisposableService();
        var registry = new SingletonRegistry();
        registry.RegisterInstance(service);
        registry.Dispose();
        service.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_TracksAndDisposesAsyncInstances()
    {
        var service = new AsyncDisposableService();
        var registry = new SingletonRegistry();
        registry.RegisterInstance(service);
        await registry.DisposeAsync();
        service.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public void Get_AfterDispose_ThrowsSingletonDisposedException()
    {
        var registry = new SingletonRegistry();
        registry.Register<SimpleService>(() => new SimpleService());
        registry.Dispose();
        var act = () => registry.Get<SimpleService>();
        act.Should().Throw<SingletonDisposedException>();
    }

    [Fact]
    public void Register_NullFactory_ThrowsArgumentNullException()
    {
        var registry = new SingletonRegistry();
        var act = () => registry.Register<SimpleService>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterInstance_NullInstance_ThrowsArgumentNullException()
    {
        var registry = new SingletonRegistry();
        var act = () => registry.RegisterInstance<SimpleService>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_WithSingletonWrapper_Works()
    {
        var registry = new SingletonRegistry();
        var lazySingleton = new LazySingleton<SimpleService>(() => new SimpleService());
        registry.Register<SimpleService>(lazySingleton);
        var instance = registry.Get<SimpleService>();
        instance.Should().BeSameAs(lazySingleton.Value);
    }
}
