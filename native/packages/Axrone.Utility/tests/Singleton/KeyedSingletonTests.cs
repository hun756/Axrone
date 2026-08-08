using Axrone.Utility.Singleton;

namespace Axrone.Utility.Tests.Singleton;

public class KeyedSingletonTests
{
    [Fact]
    public void GetInstance_SameKey_ReturnsSameInstance()
    {
        KeyedSession.Clear();
        var a = KeyedSession.GetInstance("primary");
        var b = KeyedSession.GetInstance("primary");
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void GetInstance_DifferentKeys_ReturnsDifferentInstances()
    {
        KeyedSession.Clear();
        var a = KeyedSession.GetInstance("primary");
        var b = KeyedSession.GetInstance("secondary");
        a.Should().NotBeSameAs(b);
    }

    [Fact]
    public void Key_IsSetOnCreation()
    {
        KeyedSession.Clear();
        var instance = KeyedSession.GetInstance("mykey");
        instance.Key.Should().Be("mykey");
    }

    [Fact]
    public void Lifecycle_Initialize_CalledWithKey()
    {
        KeyedSession.Clear();
        var instance = KeyedSession.GetInstance("testkey");
        instance.InitName.Should().Be("testkey");
    }

    [Fact]
    public void TryGet_ReturnsFalse_WhenMissing()
    {
        KeyedSession.Clear();
        KeyedSession.TryGet("missing", out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Count_ReflectsActiveInstances()
    {
        KeyedSession.Clear();
        KeyedSession.Count.Should().Be(0);
        _ = KeyedSession.GetInstance("a");
        _ = KeyedSession.GetInstance("b");
        KeyedSession.Count.Should().Be(2);
    }

    [Fact]
    public void TryRemove_DisposesInstance()
    {
        KeyedSession.Clear();
        _ = KeyedSession.GetInstance("removable");
        KeyedSession.TryRemove("removable").Should().BeTrue();
        KeyedSession.Count.Should().Be(0);
    }

    [Fact]
    public void TryRemove_ReturnsFalse_WhenKeyMissing()
    {
        KeyedSession.Clear();
        KeyedSession.TryRemove("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesAllInstances()
    {
        KeyedSession.Clear();
        _ = KeyedSession.GetInstance("x");
        _ = KeyedSession.GetInstance("y");
        _ = KeyedSession.GetInstance("z");
        KeyedSession.Count.Should().Be(3);

        KeyedSession.Clear();
        KeyedSession.Count.Should().Be(0);
    }

    [Fact]
    public void TryGet_ReturnsTrue_WhenExists()
    {
        KeyedSession.Clear();
        var expected = KeyedSession.GetInstance("findable");
        KeyedSession.TryGet("findable", out var result).Should().BeTrue();
        result.Should().BeSameAs(expected);
    }
}
