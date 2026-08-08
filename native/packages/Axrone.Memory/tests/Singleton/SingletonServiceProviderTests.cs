using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests;

public class SingletonServiceProviderTests
{
    [Fact]
    public void Register_GetService_RoundTrips()
    {
        var provider = SingletonServiceProvider.Instance;
        var service = new FactoryService();
        provider.Register(service);
        provider.GetService<FactoryService>().Should().BeSameAs(service);
    }

    [Fact]
    public void GetService_Throws_WhenNotRegistered()
    {
        var provider = SingletonServiceProvider.Instance;
        provider.Invoking(p => p.GetService<CrtpService>())
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryGetService_ReturnsFalse_WhenMissing()
    {
        var provider = SingletonServiceProvider.Instance;
        provider.TryGetService<FactoryCountService>(out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void GetOrCreateService_CreatesOnFirstCall()
    {
        var provider = SingletonServiceProvider.Instance;
        var a = provider.GetOrCreateService<SimpleService>();
        var b = provider.GetOrCreateService<SimpleService>();
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void Register_Named_GetService_RoundTrips()
    {
        var provider = SingletonServiceProvider.Instance;
        var service = new FactoryService();
        provider.Register(service, "primary");
        provider.GetService<FactoryService>("primary").Should().BeSameAs(service);
    }

    [Fact]
    public void GetService_Named_Throws_WhenNotRegistered()
    {
        var provider = SingletonServiceProvider.Instance;
        provider.Invoking(p => p.GetService<FactoryService>("nonexistent"))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryGetService_Named_ReturnsFalse_WhenMissing()
    {
        var provider = SingletonServiceProvider.Instance;
        provider.TryGetService<FactoryService>("missing", out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Register_NullService_ThrowsArgumentNullException()
    {
        var provider = SingletonServiceProvider.Instance;
        var act = () => provider.Register<FactoryService>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_Named_NullService_ThrowsArgumentNullException()
    {
        var provider = SingletonServiceProvider.Instance;
        var act = () => provider.Register<FactoryService>(null!, "name");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_Named_EmptyName_ThrowsArgumentException()
    {
        var provider = SingletonServiceProvider.Instance;
        var service = new FactoryService();
        var act = () => provider.Register(service, "");
        act.Should().Throw<ArgumentException>();
    }
}
