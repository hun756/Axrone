using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests;

/// <summary>
/// Integration tests combining multiple singleton library components.
/// Validates that Registry, Scope, Handle, and singleton variants work together correctly.
/// </summary>
public class IntegrationTests
{
    [Fact]
    public void Registry_WithLazySingleton_ProxiesValueCorrectly()
    {
        var registry = new SingletonRegistry();
        var lazy = new LazySingleton<SimpleService>(() => new SimpleService());
        registry.Register<SimpleService>(lazy);

        var fromRegistry = registry.Get<SimpleService>();
        var fromLazy = lazy.Value;

        fromRegistry.Should().BeSameAs(fromLazy);
    }

    [Fact]
    public void Scope_OverridesParentRegistration()
    {
        var parent = new SingletonRegistry();
        var parentInstance = new SimpleService();
        parent.RegisterInstance(parentInstance);

        var scope = new SingletonScope(parent);
        var scopeInstance = new SimpleService();
        scope.RegisterInstance(scopeInstance);

        scope.Get<SimpleService>().Should().BeSameAs(scopeInstance);
        parent.Get<SimpleService>().Should().BeSameAs(parentInstance);
    }

    [Fact]
    public void Scope_ChildOverridesParent()
    {
        var root = new SingletonRegistry();
        var rootInstance = new SimpleService();
        root.RegisterInstance(rootInstance);

        var child = new SingletonScope(root);
        var childInstance = new SimpleService();
        child.RegisterInstance(childInstance);

        var grandchild = child.CreateChildScope();
        var grandchildInstance = new SimpleService();
        grandchild.RegisterInstance(grandchildInstance);

        grandchild.Get<SimpleService>().Should().BeSameAs(grandchildInstance);
        child.Get<SimpleService>().Should().BeSameAs(childInstance);
        root.Get<SimpleService>().Should().BeSameAs(rootInstance);
    }

    [Fact]
    public void Scope_DisposeDoesNotAffectParent()
    {
        var parent = new SingletonRegistry();
        var parentService = new DisposableService();
        parent.RegisterInstance(parentService);

        var scope = new SingletonScope(parent);
        var scopeService = new DisposableService();
        scope.RegisterInstance(scopeService);

        scope.Dispose();

        scopeService.WasDisposed.Should().BeTrue();
        parentService.WasDisposed.Should().BeFalse();
    }

    [Fact]
    public void Handle_FromRegistry_ReflectsRegistrationState()
    {
        var registry = new SingletonRegistry();
        var handle = registry.GetHandle<SimpleService>();

        handle.IsCreated.Should().BeFalse();
        handle.State.Should().Be(SingletonLifecycleState.Uninitialized);

        registry.Register<SimpleService>(() => new SimpleService());

        _ = handle.Value;
        handle.IsCreated.Should().BeTrue();
        handle.State.Should().Be(SingletonLifecycleState.Initialized);
    }

    [Fact]
    public void Handle_ImplicitConversion_MatchesExplicitValue()
    {
        var registry = new SingletonRegistry();
        var expected = new SimpleService();
        registry.RegisterInstance(expected);

        var handle = registry.GetHandle<SimpleService>();
        SimpleService viaImplicit = handle;
        var viaExplicit = handle.Value;

        viaImplicit.Should().BeSameAs(viaExplicit);
        viaImplicit.Should().BeSameAs(expected);
    }

    [Fact]
    public void Handle_ToT_MatchesImplicitConversion()
    {
        var registry = new SingletonRegistry();
        var expected = new SimpleService();
        registry.RegisterInstance(expected);

        var handle = registry.GetHandle<SimpleService>();
        SimpleService viaMethod = handle.ToT();
        SimpleService viaImplicit = handle;

        viaMethod.Should().BeSameAs(viaImplicit);
    }

    [Fact]
    public void Registry_WithMultipleTypes_ResolvesIndependently()
    {
        var registry = new SingletonRegistry();
        var serviceA = new SimpleService();
        var serviceB = new DisposableService();

        registry.RegisterInstance(serviceA);
        registry.RegisterInstance(serviceB);

        registry.Get<SimpleService>().Should().BeSameAs(serviceA);
        registry.Get<DisposableService>().Should().BeSameAs(serviceB);
    }

    [Fact]
    public async Task Registry_DisposeAsync_DisposesAllTrackedInstances()
    {
        var registry = new SingletonRegistry();
        var syncDisposable = new DisposableService();
        var asyncDisposable = new AsyncDisposableService();

        registry.RegisterInstance(syncDisposable);
        registry.RegisterInstance(asyncDisposable);

        await registry.DisposeAsync();

        syncDisposable.WasDisposed.Should().BeTrue();
        asyncDisposable.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public void Registry_TryGet_WithLazyFactory_TriggersCreation()
    {
        var registry = new SingletonRegistry();
        var createCount = 0;
        registry.Register(() =>
        {
            Interlocked.Increment(ref createCount);
            return new SimpleService();
        });

        registry.TryGet<SimpleService>(out var instance).Should().BeTrue();
        instance.Should().NotBeNull();
        createCount.Should().Be(1);

        // Second TryGet should not re-create
        registry.TryGet<SimpleService>(out var instance2).Should().BeTrue();
        instance2.Should().BeSameAs(instance);
        createCount.Should().Be(1);
    }

    [Fact]
    public void Scope_ContainsChecksBothLocalAndParent()
    {
        var parent = new SingletonRegistry();
        parent.Register<SimpleService>(() => new SimpleService());

        var scope = new SingletonScope(parent);
        scope.Register<DisposableService>(() => new DisposableService());

        scope.Contains<SimpleService>().Should().BeTrue(); // From parent
        scope.Contains<DisposableService>().Should().BeTrue(); // From local
        scope.Contains<UntouchedService>().Should().BeFalse(); // Not registered
    }

    [Fact]
    public async Task SingletonAuto_ConcurrentAccess_ReturnsSameInstance()
    {
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => SingletonAuto<SimpleService>.Instance))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var first = results[0];

        results.Should().OnlyContain(r => ReferenceEquals(r, first));
    }
}
