using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests;

public class SingletonExceptionTests
{
    [Fact]
    public void ContainsTargetType()
    {
        var ex = new SingletonCreationException("fail", typeof(SimpleService));
        ex.TargetType.Should().Be<SimpleService>();
        ex.Message.Should().Be("fail");
    }

    [Fact]
    public void WrapsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new SingletonCreationException("outer", typeof(SimpleService), inner);
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void DefaultConstructor()
    {
        var ex = new SingletonCreationException();
        ex.TargetType.Should().BeNull();
    }

    [Fact]
    public void MessageOnlyConstructor()
    {
        var ex = new SingletonCreationException("message");
        ex.Message.Should().Be("message");
        ex.TargetType.Should().BeNull();
    }

    [Fact]
    public void MessageAndInnerExceptionConstructor()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new SingletonCreationException("msg", inner);
        ex.InnerException.Should().BeSameAs(inner);
        ex.TargetType.Should().BeNull();
    }
}
