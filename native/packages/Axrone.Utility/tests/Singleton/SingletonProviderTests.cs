using Axrone.Utility.Singleton;
using Axrone.Utility.Singleton.Internal;
using SingletonFacade = Axrone.Utility.Singleton.Singleton;

namespace Axrone.Utility.Tests.Singleton;

public class SingletonProviderTests
{
    [Fact]
    public void Standard_CAS_ReturnsSameInstance()
    {
        var a = SingletonFacade.GetInstance<SimpleService>();
        var b = SingletonFacade.GetInstance<SimpleService>();
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void ThreadStatic_ReturnsInstancePerThread()
    {
        ThreadStaticService? mainInstance = null;
        ThreadStaticService? otherInstance = null;

        mainInstance = SingletonFacade.GetInstance<ThreadStaticService>();

        var thread = new Thread(() =>
        {
            otherInstance = SingletonFacade.GetInstance<ThreadStaticService>();
        });
        thread.Start();
        thread.Join();

        mainInstance.Should().NotBeNull();
        otherInstance.Should().NotBeNull();
        mainInstance.Should().NotBeSameAs(otherInstance);
    }

    [Fact]
    public void ThreadStatic_SameInstanceOnSameThread()
    {
        var a = SingletonFacade.GetInstance<ThreadStaticService>();
        var b = SingletonFacade.GetInstance<ThreadStaticService>();
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void Weak_ReturnsInstanceWhileAlive()
    {
        var instance = SingletonFacade.GetInstance<WeakStrategyService>();
        instance.Should().NotBeNull();

        var same = SingletonFacade.GetInstance<WeakStrategyService>();
        same.Should().BeSameAs(instance);
    }

    [Fact]
    public void WarmUp_ForcesInitialization()
    {
        SingletonFacade.WarmUp<InitializableService>();
        SingletonFacade.IsInitialized<InitializableService>().Should().BeTrue();
    }

    [Fact]
    public void IsInitialized_FalseBeforeCreation()
    {
        SingletonProvider<UntouchedService>.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void IsInitialized_TrueAfterCreation()
    {
        _ = SingletonFacade.GetInstance<SimpleService>();
        SingletonProvider<SimpleService>.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void TryGet_ReturnsTrue_AfterCreation()
    {
        _ = SingletonFacade.GetInstance<SimpleService>();
        SingletonProvider<SimpleService>.TryGet(out var instance).Should().BeTrue();
        instance.Should().NotBeNull();
    }

    [Fact]
    public void ConstructionFailure_ResetsState_ForRetry()
    {
        var act = () => SingletonFacade.GetInstance<ThrowingService>();
        act.Should().Throw<SingletonCreationException>();

        SingletonProvider<ThrowingService>.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void TryRegister_PublishesInstance_TryGetReturnsIt()
    {
        var expected = new PreRegisterService();
        SingletonProvider<PreRegisterService>.TryRegister(expected).Should().BeTrue();
        SingletonProvider<PreRegisterService>.TryGet(out var actual).Should().BeTrue();
        actual.Should().BeSameAs(expected);
    }

    [Fact]
    public void TryRegister_Fails_WhenAlreadyInitialized()
    {
        _ = SingletonFacade.GetInstance<SimpleService>();
        SingletonProvider<SimpleService>.TryRegister(new SimpleService()).Should().BeFalse();
    }
}
