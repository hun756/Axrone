using Axrone.Utility.Singleton.Internal;

namespace Axrone.Utility.Tests.Singleton;

public class SingletonRegistryInternalTests
{
    [Fact]
    public void Register_And_TryGet_RoundTrips()
    {
        var service = new FactoryService();
        SingletonRegistryInternal.Register(service);

        SingletonRegistryInternal.TryGet<FactoryService>(out var result).Should().BeTrue();
        result.Should().BeSameAs(service);
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenNotRegistered()
    {
        SingletonRegistryInternal.TryGet<UntouchedService>(out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Register_OverwritesPreviousInstance()
    {
        var first = new FactoryService();
        var second = new FactoryService();

        SingletonRegistryInternal.Register(first);
        SingletonRegistryInternal.Register(second);

        SingletonRegistryInternal.TryGet<FactoryService>(out var result).Should().BeTrue();
        result.Should().BeSameAs(second);
    }

    [Fact]
    public void Count_IsAtLeastOne_AfterRegistration()
    {
        var service = new DisposableService();
        SingletonRegistryInternal.Register(service);
        SingletonRegistryInternal.Count.Should().BeGreaterThanOrEqualTo(1);
    }
}
