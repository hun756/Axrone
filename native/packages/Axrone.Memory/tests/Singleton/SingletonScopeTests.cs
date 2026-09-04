using Enterprise.Patterns.Singleton;

namespace Axrone.Memory.Tests;

public class SingletonScopeTests
{
    [Fact]
    public void Get_FromLocalScope_ReturnsLocalInstance()
    {
        var parent = new SingletonRegistry();
        var child = new SingletonScope(parent);
        var expected = new SimpleService();
        child.RegisterInstance(expected);
        child.Get<SimpleService>().Should().BeSameAs(expected);
    }

    [Fact]
    public void Get_NotInLocal_FallsBackToParent()
    {
        var parent = new SingletonRegistry();
        var expected = new SimpleService();
        parent.RegisterInstance(expected);
        var child = new SingletonScope(parent);
        child.Get<SimpleService>().Should().BeSameAs(expected);
    }

    [Fact]
    public void Get_NotInEither_ThrowsKeyNotFoundException()
    {
        var parent = new SingletonRegistry();
        var child = new SingletonScope(parent);
        var act = () => child.Get<UntouchedService>();
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void TryGet_InLocal_ReturnsTrue()
    {
        var parent = new SingletonRegistry();
        var child = new SingletonScope(parent);
        child.Register<SimpleService>(() => new SimpleService());
        child.TryGet<SimpleService>(out var instance).Should().BeTrue();
        instance.Should().NotBeNull();
    }

    [Fact]
    public void TryGet_InParent_ReturnsTrue()
    {
        var parent = new SingletonRegistry();
        parent.Register<SimpleService>(() => new SimpleService());
        var child = new SingletonScope(parent);
        child.TryGet<SimpleService>(out var instance).Should().BeTrue();
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Contains_InLocal_ReturnsTrue()
    {
        var parent = new SingletonRegistry();
        var child = new SingletonScope(parent);
        child.Register<SimpleService>(() => new SimpleService());
        child.Contains<SimpleService>().Should().BeTrue();
    }

    [Fact]
    public void Contains_InParent_ReturnsTrue()
    {
        var parent = new SingletonRegistry();
        parent.Register<SimpleService>(() => new SimpleService());
        var child = new SingletonScope(parent);
        child.Contains<SimpleService>().Should().BeTrue();
    }

    [Fact]
    public void CreateChildScope_ReturnsNewScope()
    {
        var parent = new SingletonRegistry();
        var child = new SingletonScope(parent);
        var grandchild = child.CreateChildScope();
        grandchild.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_DisposesLocalInstances()
    {
        var parent = new SingletonRegistry();
        var child = new SingletonScope(parent);
        var service = new DisposableService();
        child.RegisterInstance(service);
        child.Dispose();
        service.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public void Get_AfterDispose_ThrowsSingletonDisposedException()
    {
        var parent = new SingletonRegistry();
        var child = new SingletonScope(parent);
        child.Register<SimpleService>(() => new SimpleService());
        child.Dispose();
        var act = () => child.Get<SimpleService>();
        act.Should().Throw<SingletonDisposedException>();
    }

    [Fact]
    public void Register_NullParent_ThrowsArgumentNullException()
    {
        var act = () => new SingletonScope(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
