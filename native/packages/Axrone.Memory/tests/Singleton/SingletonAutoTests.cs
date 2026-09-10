using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests;

public class SingletonAutoTests
{
    [Fact]
    public void Instance_ReturnsSameOnMultipleAccesses()
    {
        var a = SingletonAuto<SimpleService>.Instance;
        var b = SingletonAuto<SimpleService>.Instance;
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void IsValueCreated_FalseBeforeAccess()
    {
        SingletonAuto<UntouchedService>.IsValueCreated.Should().BeFalse();
    }

    [Fact]
    public void IsValueCreated_TrueAfterAccess()
    {
        _ = SingletonAuto<SimpleService>.Instance;
        SingletonAuto<SimpleService>.IsValueCreated.Should().BeTrue();
    }
}
