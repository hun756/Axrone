using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests;

public class AsyncSingletonTests
{
    [Fact]
    public async Task GetValueAsync_ReturnsSameValue()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => new ValueTask<SimpleService>(new SimpleService()));

        var a = await singleton.GetValueAsync();
        var b = await singleton.GetValueAsync();
        a.Should().BeSameAs(b);
    }

    [Fact]
    public async Task IsValueCreated_TrueAfterSuccess()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => new ValueTask<SimpleService>(new SimpleService()));

        singleton.IsValueCreated.Should().BeFalse();
        await singleton.GetValueAsync();
        singleton.IsValueCreated.Should().BeTrue();
    }

    [Fact]
    public async Task State_TransitionsToInitialized()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => new ValueTask<SimpleService>(new SimpleService()));

        singleton.State.Should().Be(SingletonLifecycleState.Uninitialized);
        await singleton.GetValueAsync();
        singleton.State.Should().Be(SingletonLifecycleState.Initialized);
    }

    [Fact]
    public async Task Factory_Throws_TransitionsToFaulted()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => throw new InvalidOperationException("fail"));

        await singleton.Invoking(s => s.GetValueAsync().AsTask())
            .Should().ThrowAsync<SingletonInitializationException>();

        singleton.State.Should().Be(SingletonLifecycleState.Faulted);
    }

    [Fact]
    public async Task DisposeAsync_DisposesUnderlyingInstance()
    {
        var service = new AsyncDisposableService();
        var singleton = new AsyncSingleton<AsyncDisposableService>(
            ct => new ValueTask<AsyncDisposableService>(service));

        await singleton.GetValueAsync();
        await singleton.DisposeAsync();

        service.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => new ValueTask<SimpleService>(new SimpleService()));

        await singleton.GetValueAsync();
        await singleton.DisposeAsync();
        await singleton.DisposeAsync();
    }

    [Fact]
    public async Task GetValueAsync_AfterDispose_ThrowsSingletonDisposedException()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => new ValueTask<SimpleService>(new SimpleService()));

        await singleton.DisposeAsync();

        await singleton.Invoking(s => s.GetValueAsync().AsTask())
            .Should().ThrowAsync<SingletonDisposedException>();
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
            .Select(_ => singleton.GetValueAsync().AsTask())
            .ToArray();

        var results = await Task.WhenAll(tasks);
        foreach (var result in results)
            result.Should().BeSameAs(results[0]);
    }

    [Fact]
    public void Constructor_NullFactory_ThrowsArgumentNullException()
    {
        var act = () => new AsyncSingleton<SimpleService>((Func<CancellationToken, ValueTask<SimpleService>>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Constructor_WithSyncFactory_Works()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            () => Task.FromResult(new SimpleService()));

        var instance = await singleton.GetValueAsync();
        instance.Should().NotBeNull();
    }

    [Fact]
    public async Task Constructor_WithAsyncFactoryInterface_Works()
    {
        var factory = new TestAsyncFactory();
        var singleton = new AsyncSingleton<SimpleService>(factory);

        var instance = await singleton.GetValueAsync();
        instance.Should().NotBeNull();
        factory.CreateCount.Should().Be(1);
    }

    private sealed class TestAsyncFactory : IAsyncSingletonFactory<SimpleService>
    {
        public int CreateCount { get; private set; }
        public ValueTask<SimpleService> CreateAsync(CancellationToken cancellationToken = default)
        {
            CreateCount++;
            return new ValueTask<SimpleService>(new SimpleService());
        }
    }
}
