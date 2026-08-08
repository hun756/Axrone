using Axrone.Utility.Singleton.Internal;

namespace Axrone.Utility.Tests.Singleton;

public class ObjectPoolTests
{
    [Fact]
    public void Get_ReturnsNewInstance_WhenPoolEmpty()
    {
        var pool = new ObjectPool<List<int>>(() => new List<int>());
        var item = pool.Get();
        item.Should().NotBeNull();
    }

    [Fact]
    public void Return_ThenGet_ReusesInstance()
    {
        var pool = new ObjectPool<List<int>>(() => new List<int>());
        var original = pool.Get();
        pool.Return(original);
        var reused = pool.Get();
        reused.Should().BeSameAs(original);
    }

    [Fact]
    public void Initializer_CalledOnGet()
    {
        var pool = new ObjectPool<List<int>>(
            () => new List<int>(),
            initializer: list => list.Add(42));

        var item = pool.Get();
        item.Should().Contain(42);
    }

    [Fact]
    public void Reset_CalledOnReturn()
    {
        var resetCalled = false;
        var pool = new ObjectPool<List<int>>(
            () => new List<int>(),
            reset: _ => resetCalled = true);

        var item = pool.Get();
        pool.Return(item);
        resetCalled.Should().BeTrue();
    }

    [Fact]
    public void Return_NullItem_ThrowsArgumentNullException()
    {
        var pool = new ObjectPool<List<int>>(() => new List<int>());
        var act = () => pool.Return(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullFactory_ThrowsArgumentNullException()
    {
        var act = () => new ObjectPool<List<int>>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Pool_DoesNotExceedCapacity()
    {
        var pool = new ObjectPool<List<int>>(() => new List<int>(), size: 2);

        var items = Enumerable.Range(0, 5).Select(_ => pool.Get()).ToArray();
        foreach (var item in items)
            pool.Return(item);

        var reused = pool.Get();
        reused.Should().NotBeNull();
    }
}
