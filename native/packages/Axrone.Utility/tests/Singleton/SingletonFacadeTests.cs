using Axrone.Utility.Singleton;
using SingletonFacade = Axrone.Utility.Singleton.Singleton;

namespace Axrone.Utility.Tests.Singleton;

public class SingletonFacadeTests
{
    [Fact]
    public void GetInstance_ReturnsSameInstance()
    {
        var a = SingletonFacade.GetInstance<SimpleService>();
        var b = SingletonFacade.GetInstance<SimpleService>();
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void GetInstance_WithFactory_UsesFactory()
    {
        var expected = new UniqueFactoryService();
        var instance = SingletonFacade.GetInstance(() => expected);
        instance.Should().BeSameAs(expected);
    }

    [Fact]
    public void GetInstance_WithFactory_ReturnsSameOnSecondCall()
    {
        int callCount = 0;
        var a = SingletonFacade.GetInstance(() => { callCount++; return new UniqueFactoryCountService(); });
        var b = SingletonFacade.GetInstance(() => { callCount++; return new UniqueFactoryCountService(); });
        a.Should().BeSameAs(b);
        callCount.Should().Be(1);
    }

    [Fact]
    public void WarmUp_ForcesInitialization()
    {
        SingletonFacade.WarmUp<InitializableService>();
        SingletonFacade.IsInitialized<InitializableService>().Should().BeTrue();
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenNotCreated()
    {
        SingletonFacade.TryGet<UntouchedService>(out var result).Should().BeFalse();
    }

    [Fact]
    public void Register_PreCreatedInstance()
    {
        var expected = new DisposableService();
        SingletonFacade.Register(expected);
        SingletonFacade.TryGet<DisposableService>(out var result).Should().BeTrue();
        result.Should().BeSameAs(expected);
    }

    [Fact]
    public void Register_NullInstance_ThrowsArgumentNullException()
    {
        var act = () => SingletonFacade.Register<DisposableService>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsInitialized_ReturnsFalse_WhenNotYetCreated()
    {
        SingletonFacade.IsInitialized<UniqueFactoryCountService>().Should().BeFalse();
    }

    [Fact]
    public void GetMetrics_ReturnsDefault_WhenNotMonitored()
    {
        var metrics = SingletonFacade.GetMetrics<SimpleService>();
        metrics.Should().Be(default(SingletonMetrics));
    }
}
