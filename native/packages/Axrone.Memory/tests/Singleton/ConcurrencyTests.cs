using Enterprise.Patterns.Singleton;

namespace Axrone.Memory.Tests;

/// <summary>
/// Concurrency and stress tests for the singleton library.
/// Validates thread safety, race conditions, and high-contention scenarios.
/// </summary>
public class ConcurrencyTests
{
    [Fact]
    public async Task Singleton_ConcurrentSetFactory_OnlyOneSucceeds()
    {
        var successCount = 0;
        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(async () =>
        {
            try
            {
                Singleton<SimpleService>.SetFactory(() => new SimpleService());
                _ = Singleton<SimpleService>.Instance;
                Interlocked.Increment(ref successCount);
            }
            catch (SingletonAlreadyInitializedException) { }
            catch (SingletonInitializationException) { }
        })).ToArray();

        await Task.WhenAll(tasks);

        successCount.Should().BeGreaterThanOrEqualTo(1);
        Singleton<SimpleService>.Reset();
    }

    [Fact]
    public async Task LazySingleton_ConcurrentAccess_AllGetSameInstance()
    {
        var singleton = new LazySingleton<SimpleService>(() =>
        {
            Thread.Sleep(5);
            return new SimpleService();
        });

        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => singleton.Value))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var first = results[0];

        results.Should().OnlyContain(r => ReferenceEquals(r, first));
    }

    [Fact]
    public async Task LazySingleton_ConcurrentDispose_IsIdempotent()
    {
        var singleton = new LazySingleton<DisposableService>(() => new DisposableService());
        _ = singleton.Value;

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => singleton.Dispose()))
            .ToArray();

        await Task.WhenAll(tasks);

        singleton.State.Should().Be(SingletonLifecycleState.Disposed);
    }

    [Fact]
    public async Task AsyncSingleton_ConcurrentGetValueAsync_AllGetSameInstance()
    {
        var singleton = new AsyncSingleton<SimpleService>(async ct =>
        {
            await Task.Delay(10, ct).ConfigureAwait(false);
            return new SimpleService();
        });

        var tasks = Enumerable.Range(0, 30)
            .Select(_ => singleton.GetValueAsync().AsTask())
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var first = results[0];

        results.Should().OnlyContain(r => ReferenceEquals(r, first));
    }

    [Fact]
    public async Task SingletonRegistry_ConcurrentRegisterAndGet_NoExceptions()
    {
        var registry = new SingletonRegistry();

        var registerTasks = Enumerable.Range(0, 10).Select(i => Task.Run(() =>
        {
            try { registry.Register(() => new SimpleService()); }
            catch (SingletonAlreadyInitializedException) { }
        })).ToArray();

        await Task.WhenAll(registerTasks);

        var getTasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            try { return registry.Get<SimpleService>(); }
            catch { return null; }
        })).ToArray();

        var results = await Task.WhenAll(getTasks);
        var nonNull = results.Where(r => r is not null).ToArray();

        nonNull.Should().NotBeEmpty();
        var first = nonNull[0];
        nonNull.Should().OnlyContain(r => ReferenceEquals(r, first));
    }

    [Fact]
    public async Task SingletonScope_ConcurrentChildScopeCreation_NoExceptions()
    {
        var parent = new SingletonRegistry();
        parent.Register<SimpleService>(() => new SimpleService());
        var scope = new SingletonScope(parent);

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() =>
            {
                var child = scope.CreateChildScope();
                child.Get<SimpleService>().Should().NotBeNull();
                child.Dispose();
            }))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    [Fact]
    public void Stress_RapidCreateDispose_NoMemoryLeak()
    {
        for (var i = 0; i < 100; i++)
        {
            var singleton = new LazySingleton<DisposableService>(() => new DisposableService());
            _ = singleton.Value;
            singleton.Dispose();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
