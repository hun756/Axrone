using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests;

/// <summary>
/// Edge case and boundary tests for the singleton library.
/// Covers null factories, factory returning null, double-init, and other boundary conditions.
/// </summary>
public class EdgeCaseTests
{
    // ── Factory returning null ────────────────────────────────────────

    [Fact]
    public void LazySingleton_FactoryReturnsNull_ThrowsInitializationException()
    {
        var singleton = new LazySingleton<SimpleService>(() => null!);
        var act = () => singleton.Value;
        act.Should().Throw<SingletonInitializationException>();
        singleton.State.Should().Be(SingletonLifecycleState.Faulted);
    }

    [Fact]
    public async Task AsyncSingleton_FactoryReturnsNull_ThrowsInitializationException()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => new ValueTask<SimpleService>((SimpleService)null!));

        await singleton.Invoking(s => s.GetValueAsync().AsTask())
            .Should().ThrowAsync<SingletonInitializationException>();

        singleton.State.Should().Be(SingletonLifecycleState.Faulted);
    }

    [Fact]
    public void Singleton_FactoryReturnsNull_ThrowsInitializationException()
    {
        Singleton<SimpleService>.SetFactory(() => null!);
        var act = () => Singleton<SimpleService>.Instance;
        act.Should().Throw<SingletonInitializationException>();
        Singleton<SimpleService>.Reset();
    }

    // ── Registry edge cases ──────────────────────────────────────────

    [Fact]
    public void Registry_RegisterInstance_NullInstance_ThrowsArgumentNullException()
    {
        var registry = new SingletonRegistry();
        var act = () => registry.RegisterInstance<SimpleService>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Registry_Register_NullSingleton_ThrowsArgumentNullException()
    {
        var registry = new SingletonRegistry();
        var act = () => registry.Register<SimpleService>((ISingleton<SimpleService>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Registry_Get_AfterDispose_ThrowsSingletonDisposedException()
    {
        var registry = new SingletonRegistry();
        registry.Register<SimpleService>(() => new SimpleService());
        registry.Dispose();

        var act = () => registry.Get<SimpleService>();
        act.Should().Throw<SingletonDisposedException>();
    }

    [Fact]
    public void Registry_TryGet_AfterDispose_ReturnsFalse()
    {
        var registry = new SingletonRegistry();
        registry.Register<SimpleService>(() => new SimpleService());
        registry.Dispose();

        registry.TryGet<SimpleService>(out _).Should().BeFalse();
    }

    [Fact]
    public void Registry_Contains_AfterDispose_ThrowsSingletonDisposedException()
    {
        var registry = new SingletonRegistry();
        registry.Register<SimpleService>(() => new SimpleService());
        registry.Dispose();

        var act = () => registry.Contains<SimpleService>();
        act.Should().Throw<SingletonDisposedException>();
    }

    [Fact]
    public void Registry_DoubleDispose_IsIdempotent()
    {
        var registry = new SingletonRegistry();
        registry.Register<SimpleService>(() => new SimpleService());
        _ = registry.Get<SimpleService>();

        registry.Dispose();
        registry.Dispose(); // Should not throw
    }

    // ── Scope edge cases ─────────────────────────────────────────────

    [Fact]
    public void Scope_RegisterAfterDispose_ThrowsSingletonDisposedException()
    {
        var parent = new SingletonRegistry();
        var scope = new SingletonScope(parent);
        scope.Dispose();

        var act = () => scope.Register<SimpleService>(() => new SimpleService());
        act.Should().Throw<SingletonDisposedException>();
    }

    [Fact]
    public void Scope_CreateChildScope_AfterDispose_ThrowsSingletonDisposedException()
    {
        var parent = new SingletonRegistry();
        var scope = new SingletonScope(parent);
        scope.Dispose();

        var act = () => scope.CreateChildScope();
        act.Should().Throw<SingletonDisposedException>();
    }

    [Fact]
    public async Task Scope_DisposeAsync_AfterSyncDispose_IsIdempotent()
    {
        var parent = new SingletonRegistry();
        var scope = new SingletonScope(parent);
        scope.Register<SimpleService>(() => new SimpleService());
        _ = scope.Get<SimpleService>();

#pragma warning disable CA1849 // Intentionally testing sync dispose path
        scope.Dispose();
#pragma warning restore CA1849
        await scope.DisposeAsync(); // Should not throw
    }

    // ── LazyThreadSafetyMode variants ────────────────────────────────

    [Fact]
    public void LazySingleton_WithPublicationOnly_Works()
    {
        var singleton = new LazySingleton<SimpleService>(
            () => new SimpleService(),
            LazyThreadSafetyMode.PublicationOnly);

        var instance = singleton.Value;
        instance.Should().NotBeNull();
        singleton.State.Should().Be(SingletonLifecycleState.Initialized);
    }

    [Fact]
    public void LazySingleton_WithNoThreadSafety_Works()
    {
        var singleton = new LazySingleton<SimpleService>(
            () => new SimpleService(),
            LazyThreadSafetyMode.None);

        var instance = singleton.Value;
        instance.Should().NotBeNull();
    }

    // ── Singleton<T> SetInstance edge cases ──────────────────────────

    [Fact]
    public void Singleton_SetInstance_AfterSetFactory_ThrowsAlreadyInitialized()
    {
        Singleton<SimpleService>.SetFactory(() => new SimpleService());
        _ = Singleton<SimpleService>.Instance;

        var act = () => Singleton<SimpleService>.SetInstance(new SimpleService());
        act.Should().Throw<SingletonAlreadyInitializedException>();

        Singleton<SimpleService>.Reset();
    }

    [Fact]
    public void Singleton_SetFactory_AfterSetInstance_ThrowsAlreadyInitialized()
    {
        Singleton<SimpleService>.SetInstance(new SimpleService());

        var act = () => Singleton<SimpleService>.SetFactory(() => new SimpleService());
        act.Should().Throw<SingletonAlreadyInitializedException>();

        Singleton<SimpleService>.Reset();
    }

    [Fact]
    public void Singleton_Reset_BeforeInit_DoesNotThrow()
    {
        var act = () => Singleton<UntouchedService>.Reset();
        act.Should().NotThrow();
    }

    [Fact]
    public void Singleton_Reset_DisposesAsyncDisposableInstance()
    {
        var service = new AsyncDisposableService();
        Singleton<AsyncDisposableService>.SetInstance(service);
        Singleton<AsyncDisposableService>.Reset();
        service.WasDisposed.Should().BeTrue();
    }

    // ── Exception hierarchy completeness ─────────────────────────────

    [Fact]
    public void AllExceptions_AreSerializable()
    {
        // All exceptions should be constructible with message + inner
        var ex1 = new SingletonException("msg", new InvalidOperationException());
        var ex2 = new SingletonInitializationException("msg", new InvalidOperationException());
        var ex3 = new SingletonDisposedException("msg", new InvalidOperationException());
        var ex4 = new SingletonAlreadyInitializedException("msg", new InvalidOperationException());

        ex1.InnerException.Should().BeOfType<InvalidOperationException>();
        ex2.InnerException.Should().BeOfType<InvalidOperationException>();
        ex3.InnerException.Should().BeOfType<InvalidOperationException>();
        ex4.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void ExceptionHierarchy_AllInheritFromSingletonException()
    {
        var ex1 = new SingletonInitializationException("test");
        var ex2 = new SingletonDisposedException("test");
        var ex3 = new SingletonAlreadyInitializedException("test");

        ex1.Should().BeAssignableTo<SingletonException>();
        ex2.Should().BeAssignableTo<SingletonException>();
        ex3.Should().BeAssignableTo<SingletonException>();

        ex1.Should().BeAssignableTo<Exception>();
        ex2.Should().BeAssignableTo<Exception>();
        ex3.Should().BeAssignableTo<Exception>();
    }
}
