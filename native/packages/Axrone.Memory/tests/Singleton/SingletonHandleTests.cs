using Enterprise.Patterns.Singleton;

namespace Axrone.Memory.Tests;

public class SingletonHandleTests
{
    [Fact]
    public void Value_ReturnsSingletonValue()
    {
        var singleton = new LazySingleton<SimpleService>(() => new SimpleService());
        var handle = new SingletonHandle<SimpleService>(singleton);
        handle.Value.Should().BeSameAs(singleton.Value);
    }

    [Fact]
    public void ImplicitConversion_ReturnsValue()
    {
        var singleton = new LazySingleton<SimpleService>(() => new SimpleService());
        var handle = new SingletonHandle<SimpleService>(singleton);
        SimpleService instance = handle;
        instance.Should().BeSameAs(singleton.Value);
    }

    [Fact]
    public void IsCreated_ReflectsSingletonState()
    {
        var singleton = new LazySingleton<SimpleService>(() => new SimpleService());
        var handle = new SingletonHandle<SimpleService>(singleton);
        handle.IsCreated.Should().BeFalse();
        _ = handle.Value;
        handle.IsCreated.Should().BeTrue();
    }

    [Fact]
    public void State_ReflectsSingletonState()
    {
        var singleton = new LazySingleton<SimpleService>(() => new SimpleService());
        var handle = new SingletonHandle<SimpleService>(singleton);
        handle.State.Should().Be(SingletonLifecycleState.Uninitialized);
        _ = handle.Value;
        handle.State.Should().Be(SingletonLifecycleState.Initialized);
    }

    [Fact]
    public void Equality_SameSingleton_ReturnsTrue()
    {
        var singleton = new LazySingleton<SimpleService>(() => new SimpleService());
        var handle1 = new SingletonHandle<SimpleService>(singleton);
        var handle2 = new SingletonHandle<SimpleService>(singleton);
        (handle1 == handle2).Should().BeTrue();
        handle1.Equals(handle2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentSingletons_ReturnsFalse()
    {
        var singleton1 = new LazySingleton<SimpleService>(() => new SimpleService());
        var singleton2 = new LazySingleton<SimpleService>(() => new SimpleService());
        var handle1 = new SingletonHandle<SimpleService>(singleton1);
        var handle2 = new SingletonHandle<SimpleService>(singleton2);
        (handle1 != handle2).Should().BeTrue();
    }

    [Fact]
    public void Constructor_NullSingleton_ThrowsArgumentNullException()
    {
        var act = () => new SingletonHandle<SimpleService>(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
