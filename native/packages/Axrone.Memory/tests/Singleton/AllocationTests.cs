using Axrone.Memory.Lifetime;
using Axrone.Memory.Lifetime.Internal;
using SingletonFacade = Axrone.Memory.Lifetime.Singleton;

namespace Axrone.Memory.Tests;

public class AllocationTests
{
    [Fact]
    public void SingletonFacade_GetInstance_ZeroAllocationsOnHotPath()
    {
        _ = SingletonFacade.GetInstance<SimpleService>();

        for (var w = 0; w < 100; w++)
            _ = SingletonFacade.GetInstance<SimpleService>();

        var before = GC.GetTotalAllocatedBytes(true);

        for (var i = 0; i < 10_000; i++)
            _ = SingletonFacade.GetInstance<SimpleService>();

        var after = GC.GetTotalAllocatedBytes(true);
        (after - before).Should().Be(0);
    }

    [Fact]
    public void SingletonBase_Instance_ZeroAllocationsOnHotPath()
    {
        _ = CrtpService.Instance;

        for (var w = 0; w < 100; w++)
            _ = CrtpService.Instance;

        var before = GC.GetTotalAllocatedBytes(true);

        for (var i = 0; i < 10_000; i++)
            _ = CrtpService.Instance;

        var after = GC.GetTotalAllocatedBytes(true);
        (after - before).Should().Be(0);
    }

    [Fact]
    public void SingletonScope_Resolve_ZeroAllocationsOnHotPath()
    {
        using var scope = new SingletonScope("alloc-test");
        scope.Register<SimpleService>("svc", () => new SimpleService());
        _ = scope.Resolve<SimpleService>("svc");

        for (var w = 0; w < 100; w++)
            _ = scope.Resolve<SimpleService>("svc");

        var before = GC.GetTotalAllocatedBytes(true);

        for (var i = 0; i < 10_000; i++)
            _ = scope.Resolve<SimpleService>("svc");

        var after = GC.GetTotalAllocatedBytes(true);
        (after - before).Should().Be(0);
    }

    [Fact]
    public void SingletonScope_TypedResolve_ZeroAllocationsOnHotPath()
    {
        using var scope = new SingletonScope("alloc-typed");
        scope.Register<SimpleService>(() => new SimpleService());

        using (scope.Activate())
        {
            _ = SingletonScope.Resolve<SimpleService>();

            for (var w = 0; w < 100; w++)
                _ = SingletonScope.Resolve<SimpleService>();

            var before = GC.GetTotalAllocatedBytes(true);

            for (var i = 0; i < 10_000; i++)
                _ = SingletonScope.Resolve<SimpleService>();

            var after = GC.GetTotalAllocatedBytes(true);
            (after - before).Should().Be(0);
        }
    }

    [Fact]
    public void SingletonRegistryInternal_TryGet_ZeroAllocationsOnHotPath()
    {
        _ = SingletonFacade.GetInstance<SimpleService>();

        for (var w = 0; w < 100; w++)
            SingletonRegistryInternal.TryGet<SimpleService>(out _);

        var before = GC.GetTotalAllocatedBytes(true);

        for (var i = 0; i < 10_000; i++)
            SingletonRegistryInternal.TryGet<SimpleService>(out _);

        var after = GC.GetTotalAllocatedBytes(true);
        (after - before).Should().Be(0);
    }

    [Fact]
    public void Diagnostics_RecordAccess_ZeroAllocationsForUnmonitoredType()
    {
        _ = SingletonAttributeCache<SimpleService>.Monitored;

        for (var w = 0; w < 100; w++)
            SingletonDiagnostics.RecordAccess<SimpleService>();

        var before = GC.GetTotalAllocatedBytes(true);

        for (var i = 0; i < 10_000; i++)
            SingletonDiagnostics.RecordAccess<SimpleService>();

        var after = GC.GetTotalAllocatedBytes(true);
        (after - before).Should().Be(0);
    }

    [Fact]
    public void SingletonBase_IsInitialized_ZeroAllocations()
    {
        _ = CrtpService.Instance;

        for (var w = 0; w < 100; w++)
            _ = CrtpService.IsInitialized;

        var before = GC.GetTotalAllocatedBytes(true);

        for (var i = 0; i < 10_000; i++)
            _ = CrtpService.IsInitialized;

        var after = GC.GetTotalAllocatedBytes(true);
        (after - before).Should().Be(0);
    }

    [Fact]
    public void WeakSingleton_GetInstance_ZeroAllocationsWhenAlive()
    {
        WeakSingleton<SimpleService>.Release();
        _ = WeakSingleton<SimpleService>.GetInstance();

        for (var w = 0; w < 100; w++)
            _ = WeakSingleton<SimpleService>.GetInstance();

        var before = GC.GetTotalAllocatedBytes(true);

        for (var i = 0; i < 10_000; i++)
            _ = WeakSingleton<SimpleService>.GetInstance();

        var after = GC.GetTotalAllocatedBytes(true);
        (after - before).Should().Be(0);
    }
}
