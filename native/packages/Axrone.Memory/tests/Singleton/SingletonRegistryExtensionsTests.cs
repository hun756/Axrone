using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests;

public class SingletonRegistryExtensionsTests
{
    [Fact]
    public void GetHandle_ReturnsHandleWithValue()
    {
        var registry = new SingletonRegistry();
        var expected = new SimpleService();
        registry.RegisterInstance(expected);

        var handle = registry.GetHandle<SimpleService>();
        handle.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public void GetHandle_ImplicitConversion_ReturnsValue()
    {
        var registry = new SingletonRegistry();
        var expected = new SimpleService();
        registry.RegisterInstance(expected);

        var handle = registry.GetHandle<SimpleService>();
        SimpleService instance = handle;
        instance.Should().BeSameAs(expected);
    }

    [Fact]
    public void GetHandle_NullRegistry_ThrowsArgumentNullException()
    {
        ISingletonRegistry registry = null!;
        var act = () => registry.GetHandle<SimpleService>();
        act.Should().Throw<ArgumentNullException>();
    }
}
