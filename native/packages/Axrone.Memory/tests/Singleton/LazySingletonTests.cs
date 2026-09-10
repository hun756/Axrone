using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests;

public class LazySingletonTests
{
    [Fact]
    public void Value_ReturnsSameOnMultipleAccesses()
    {
        var singleton = new LazySingleton<SimpleService>(() => new SimpleService());
        var a = singleton.Value;
        var b = singleton.Value;
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void State_TransitionsThroughLifecycle()
    {
        var singleton = new LazySingleton<SimpleService>(() => new SimpleService());
        singleton.State.Should().Be(SingletonLifecycleState.Uninitialized);

        _ = singleton.Value;
        singleton.State.Should().Be(SingletonLifecycleState.Initialized);
    }

    [Fact]
    public void IsValueCreated_FalseBeforeAccess()
    {
        var singleton = new LazySingleton<SimpleService>(() => new SimpleService());
        singleton.IsValueCreated.Should().BeFalse();
    }

    [Fact]
    public void Dispose_TransitionsToDisposed()
    {
        var singleton = new LazySingleton<SimpleService>(() => new SimpleService());
        _ = singleton.Value;
        singleton.Dispose();
        singleton.State.Should().Be(SingletonLifecycleState.Disposed);
    }

    [Fact]
    public void Dispose_DisposesDisposableInstance()
    {
        var service = new DisposableService();
        var singleton = new LazySingleton<DisposableService>(() => service);
        _ = singleton.Value;
        singleton.Dispose();
        service.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_DisposesAsyncDisposableInstance()
    {
        var service = new AsyncDisposableService();
        var singleton = new LazySingleton<AsyncDisposableService>(() => service);
        _ = singleton.Value;
        await singleton.DisposeAsync();
        service.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public void Value_AfterDispose_ThrowsSingletonDisposedException()
    {
        var singleton = new LazySingleton<SimpleService>(() => new SimpleService());
        _ = singleton.Value;
        singleton.Dispose();

        var act = () => singleton.Value;
        act.Should().Throw<SingletonDisposedException>();
    }

    [Fact]
    public void Constructor_NullFactory_ThrowsArgumentNullException()
    {
        var act = () => new LazySingleton<SimpleService>((Func<SimpleService>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithFactoryInterface_Works()
    {
        var factory = new TestFactory();
        var singleton = new LazySingleton<SimpleService>(factory);
        var instance = singleton.Value;
        instance.Should().NotBeNull();
        factory.CreateCount.Should().Be(1);
    }

    [Fact]
    public void Factory_Throws_TransitionsToFaulted()
    {
        var singleton = new LazySingleton<SimpleService>(() => throw new InvalidOperationException("fail"));
        var act = () => singleton.Value;
        act.Should().Throw<SingletonInitializationException>();
        singleton.State.Should().Be(SingletonLifecycleState.Faulted);
    }

    private sealed class TestFactory : ISingletonFactory<SimpleService>
    {
        public int CreateCount { get; private set; }
        public SimpleService Create()
        {
            CreateCount++;
            return new SimpleService();
        }
    }
}
