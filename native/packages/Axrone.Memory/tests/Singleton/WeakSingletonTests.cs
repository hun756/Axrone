using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests;

public class WeakSingletonTests
{
    [Fact]
    public void GetInstance_ReturnsInstance()
    {
        WeakSingleton<SimpleService>.Release();
        var instance = WeakSingleton<SimpleService>.GetInstance();
        instance.Should().NotBeNull();
    }

    [Fact]
    public void GetInstance_SameInstance_WhenAlive()
    {
        WeakSingleton<SimpleService>.Release();
        var a = WeakSingleton<SimpleService>.GetInstance();
        var b = WeakSingleton<SimpleService>.GetInstance();
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void TryGet_FalseAfterRelease()
    {
        WeakSingleton<SimpleService>.Release();
        WeakSingleton<SimpleService>.TryGet(out _).Should().BeFalse();
    }

    [Fact]
    public void GetInstance_AfterRelease_CreatesNewInstance()
    {
        WeakSingleton<SimpleService>.Release();
        var first = WeakSingleton<SimpleService>.GetInstance();
        WeakSingleton<SimpleService>.Release();
        var second = WeakSingleton<SimpleService>.GetInstance();
        first.Should().NotBeSameAs(second);
    }

    [Fact]
    public void TryGet_TrueWhenAlive()
    {
        WeakSingleton<SimpleService>.Release();
        _ = WeakSingleton<SimpleService>.GetInstance();
        WeakSingleton<SimpleService>.TryGet(out var instance).Should().BeTrue();
        instance.Should().NotBeNull();
    }
}
