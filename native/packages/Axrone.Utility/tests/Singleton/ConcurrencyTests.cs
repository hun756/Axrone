using Axrone.Utility.Singleton;
using SingletonFacade = Axrone.Utility.Singleton.Singleton;

namespace Axrone.Utility.Tests.Singleton;

public class ConcurrencyTests
{
    [Fact]
    public void CAS_ConcurrentAccess_AllThreadsGetSameInstance()
    {
        const int threadCount = 16;
        var instances = new SimpleService[threadCount];
        var barrier = new ManualResetEventSlim(false);
        var threads = new Thread[threadCount];

        for (var i = 0; i < threadCount; i++)
        {
            var idx = i;
            threads[i] = new Thread(() =>
            {
                barrier.Wait();
                instances[idx] = SingletonFacade.GetInstance<SimpleService>();
            });
            threads[i].Start();
        }

        barrier.Set();
        foreach (var t in threads) t.Join();

        var first = instances[0];
        foreach (var instance in instances)
            instance.Should().BeSameAs(first);
    }

    [Fact]
    public void Scope_ConcurrentResolve_AllThreadsGetSameInstance()
    {
        using var scope = new SingletonScope("concurrent");
        scope.Register<SimpleService>("svc", () => new SimpleService());

        const int threadCount = 16;
        var instances = new SimpleService[threadCount];
        var barrier = new ManualResetEventSlim(false);
        var threads = new Thread[threadCount];

        for (var i = 0; i < threadCount; i++)
        {
            var idx = i;
            threads[i] = new Thread(() =>
            {
                barrier.Wait();
                instances[idx] = scope.Resolve<SimpleService>("svc");
            });
            threads[i].Start();
        }

        barrier.Set();
        foreach (var t in threads) t.Join();

        var first = instances[0];
        foreach (var instance in instances)
            instance.Should().BeSameAs(first);
    }

    [Fact]
    public void KeyedSingleton_ConcurrentGetOrCreate_NoDuplicateInstances()
    {
        KeyedSession.Clear();
        const int threadCount = 16;
        var instances = new KeyedSession[threadCount];
        var barrier = new ManualResetEventSlim(false);
        var threads = new Thread[threadCount];

        for (var i = 0; i < threadCount; i++)
        {
            var idx = i;
            threads[i] = new Thread(() =>
            {
                barrier.Wait();
                instances[idx] = KeyedSession.GetInstance("shared-key");
            });
            threads[i].Start();
        }

        barrier.Set();
        foreach (var t in threads) t.Join();

        var first = instances[0];
        foreach (var instance in instances)
            instance.Should().BeSameAs(first);

        KeyedSession.Count.Should().Be(1);
    }

    [Fact]
    public void ParallelFor_SingletonAccess_DoesNotThrow()
    {
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        Parallel.For(0, 100, i =>
        {
            try
            {
                _ = SingletonFacade.GetInstance<SimpleService>();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        exceptions.Should().BeEmpty();
    }

    [Fact]
    public void ParallelFor_ScopeTypedResolve_DoesNotThrow()
    {
        using var scope = new SingletonScope("parallel-typed");
        scope.Register<SimpleService>(() => new SimpleService());

        using (scope.Activate())
        {
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

            Parallel.For(0, 100, i =>
            {
                try
                {
                    _ = SingletonScope.Resolve<SimpleService>();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            exceptions.Should().BeEmpty();
        }
    }

    [Fact]
    public void ThreadStatic_ConcurrentAccess_EachThreadGetsOwnInstance()
    {
        const int threadCount = 8;
        var instances = new ThreadStaticService[threadCount];
        var barrier = new ManualResetEventSlim(false);
        var threads = new Thread[threadCount];

        for (var i = 0; i < threadCount; i++)
        {
            var idx = i;
            threads[i] = new Thread(() =>
            {
                barrier.Wait();
                instances[idx] = SingletonFacade.GetInstance<ThreadStaticService>();
            });
            threads[i].Start();
        }

        barrier.Set();
        foreach (var t in threads) t.Join();

        var uniqueIds = instances.Select(i => i.Id).Distinct().ToArray();
        uniqueIds.Length.Should().Be(threadCount);
    }
}
