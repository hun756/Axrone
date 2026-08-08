using Axrone.Utility.Singleton;

namespace Axrone.Utility.Tests.Singleton;

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

    [Fact]
    public async Task DisposeAsync_DisposesUnderlyingInstance()
    {
        var service = new AsyncDisposableService();
        var singleton = new AsyncSingleton<AsyncDisposableService>(
            ct => new ValueTask<AsyncDisposableService>(service));

        await singleton.GetInstanceAsync();
        await singleton.DisposeAsync();

        service.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => new ValueTask<SimpleService>(new SimpleService()));

        await singleton.GetInstanceAsync();
        await singleton.DisposeAsync();
        await singleton.DisposeAsync();
    }

    [Fact]
    public async Task GetInstanceAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => new ValueTask<SimpleService>(new SimpleService()));

        await singleton.DisposeAsync();

        await singleton.Invoking(s => s.GetInstanceAsync().AsTask())
            .Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task ConcurrentAccess_ReturnsSameInstance()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            async ct =>
            {
                await Task.Delay(10, ct).ConfigureAwait(false);
                return new SimpleService();
            });

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => singleton.GetInstanceAsync().AsTask())
            .ToArray();

        var results = await Task.WhenAll(tasks);
        foreach (var result in results)
            result.Should().BeSameAs(results[0]);
    }

    [Fact]
    public void Constructor_NullFactory_ThrowsArgumentNullException()
    {
        var act = () => new AsyncSingleton<SimpleService>(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
