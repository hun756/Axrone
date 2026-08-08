using Axrone.Memory.Lifetime.Internal;

namespace Axrone.Memory.Tests;

public class SingletonShutdownRegistryTests
{
    [Fact]
    public void Register_IDisposable_IncreasesCount()
    {
        var initial = SingletonShutdownRegistry.Count;
        var service = new DisposableService();
        SingletonShutdownRegistry.Register(service);
        SingletonShutdownRegistry.Count.Should().BeGreaterThanOrEqualTo(initial + 1);
    }

    [Fact]
    public void Register_SameInstanceTwice_IsIdempotent()
    {
        var service = new DisposableService();
        SingletonShutdownRegistry.Register(service);
        var countAfterFirst = SingletonShutdownRegistry.Count;
        SingletonShutdownRegistry.Register(service);
        SingletonShutdownRegistry.Count.Should().Be(countAfterFirst);
    }

    [Fact]
    public void Register_NonDisposable_IsIgnored()
    {
        var count = SingletonShutdownRegistry.Count;
        SingletonShutdownRegistry.Register(new FactoryService());
        SingletonShutdownRegistry.Count.Should().Be(count);
    }

    [Fact]
    public void Register_IAsyncDisposable_IsTracked()
    {
        var service = new AsyncDisposableService();
        var initial = SingletonShutdownRegistry.Count;
        SingletonShutdownRegistry.Register(service);
        SingletonShutdownRegistry.Count.Should().BeGreaterThanOrEqualTo(initial + 1);
    }
}
