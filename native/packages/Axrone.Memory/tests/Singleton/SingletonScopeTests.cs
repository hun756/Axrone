using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests;

public class SingletonScopeTests
{
    [Fact]
    public void Register_Resolve_ReturnsSameInstance()
    {
        using var scope = new SingletonScope("test");
        scope.Register<SimpleService>("svc", () => new SimpleService());

        var a = scope.Resolve<SimpleService>("svc");
        var b = scope.Resolve<SimpleService>("svc");
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void Resolve_UnknownKey_ThrowsKeyNotFound()
    {
        using var scope = new SingletonScope("test");
        scope.Invoking(s => s.Resolve<SimpleService>("missing"))
            .Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Child_FallsBackToParent()
    {
        using var parent = new SingletonScope("parent");
        parent.Register<SimpleService>("svc", () => new SimpleService());

        using var child = parent.CreateChild("child");
        var instance = child.Resolve<SimpleService>("svc");
        instance.Should().NotBeNull();
    }

    [Fact]
    public void TryResolve_ReturnsFalse_WhenMissing()
    {
        using var scope = new SingletonScope("test");
        scope.TryResolve<SimpleService>("missing", out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Activate_SetsCurrent()
    {
        using var scope = new SingletonScope("ambient");

        using (scope.Activate())
        {
            SingletonScope.Current.Should().BeSameAs(scope);
        }

        SingletonScope.Current.Should().NotBeSameAs(scope);
    }

    [Fact]
    public void Disposer_CalledOnDispose()
    {
        bool disposed = false;
        var scope = new SingletonScope("test");
        scope.Register<SimpleService>("svc", () => new SimpleService(), _ => disposed = true);
        _ = scope.Resolve<SimpleService>("svc");
        scope.Dispose();
        disposed.Should().BeTrue();
    }

    [Fact]
    public void TypedRegister_Resolve_ReturnsSameInstance()
    {
        using var scope = new SingletonScope("typed-test");
        scope.Register<SimpleService>(() => new SimpleService());

        using (scope.Activate())
        {
            var a = SingletonScope.Resolve<SimpleService>();
            var b = SingletonScope.Resolve<SimpleService>();
            a.Should().BeSameAs(b);
        }
    }

    [Fact]
    public void TypedResolve_UnknownType_ThrowsKeyNotFound()
    {
        using var scope = new SingletonScope("typed-missing");
        using (scope.Activate())
        {
            scope.Invoking(_ => SingletonScope.Resolve<FactoryService>())
                .Should().Throw<KeyNotFoundException>();
        }
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var scope = new SingletonScope("idempotent");
        scope.Register<SimpleService>("svc", () => new SimpleService());
        _ = scope.Resolve<SimpleService>("svc");

        scope.Dispose();
        scope.Dispose();
    }

    [Fact]
    public void Activate_NestedScopes_RestoresPrevious()
    {
        using var outer = new SingletonScope("outer");
        using var inner = new SingletonScope("inner");

        using (outer.Activate())
        {
            SingletonScope.Current.Should().BeSameAs(outer);

            using (inner.Activate())
            {
                SingletonScope.Current.Should().BeSameAs(inner);
            }

            SingletonScope.Current.Should().BeSameAs(outer);
        }
    }

    [Fact]
    public void TryResolve_Typed_ReturnsTrue_WhenRegistered()
    {
        using var scope = new SingletonScope("typed-try");
        scope.Register<SimpleService>(() => new SimpleService());

        using (scope.Activate())
        {
            SingletonScope.TryResolve<SimpleService>(out var instance).Should().BeTrue();
            instance.Should().NotBeNull();
        }
    }

    [Fact]
    public void TryResolve_Typed_ReturnsFalse_WhenNotRegistered()
    {
        using var scope = new SingletonScope("typed-try-missing");
        using (scope.Activate())
        {
            SingletonScope.TryResolve<FactoryService>(out var instance).Should().BeFalse();
            instance.Should().BeNull();
        }
    }

    [Fact]
    public void Global_Scope_IsAlwaysAvailable()
    {
        SingletonScope.Global.Should().NotBeNull();
        SingletonScope.Global.Name.Should().Be("global");
    }

    [Fact]
    public void Register_NullKey_ThrowsArgumentNullException()
    {
        using var scope = new SingletonScope("null-key");
        var act = () => scope.Register<SimpleService>(null!, () => new SimpleService());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_NullFactory_ThrowsArgumentNullException()
    {
        using var scope = new SingletonScope("null-factory");
        var act = () => scope.Register<SimpleService>("svc", null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
