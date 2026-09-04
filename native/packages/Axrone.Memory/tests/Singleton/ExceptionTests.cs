using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests;

public class ExceptionTests
{
    [Fact]
    public void SingletonException_WithMessage_HasCorrectMessage()
    {
        var ex = new SingletonException("test message");
        ex.Message.Should().Be("test message");
    }

    [Fact]
    public void SingletonException_WithInnerException_HasCorrectInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new SingletonException("outer", inner);
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void SingletonInitializationException_IsSingletonException()
    {
        var ex = new SingletonInitializationException("init failed");
        ex.Should().BeAssignableTo<SingletonException>();
    }

    [Fact]
    public void SingletonDisposedException_IsSingletonException()
    {
        var ex = new SingletonDisposedException("disposed");
        ex.Should().BeAssignableTo<SingletonException>();
    }

    [Fact]
    public void SingletonAlreadyInitializedException_IsSingletonException()
    {
        var ex = new SingletonAlreadyInitializedException("already init");
        ex.Should().BeAssignableTo<SingletonException>();
    }

    [Fact]
    public void SingletonInitializationException_WithInner_HasCorrectInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new SingletonInitializationException("init failed", inner);
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void SingletonDisposedException_WithInner_HasCorrectInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new SingletonDisposedException("disposed", inner);
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void SingletonAlreadyInitializedException_WithInner_HasCorrectInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new SingletonAlreadyInitializedException("already init", inner);
        ex.InnerException.Should().BeSameAs(inner);
    }
}
