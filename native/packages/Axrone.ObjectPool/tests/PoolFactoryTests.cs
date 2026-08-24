namespace Axrone.ObjectPool.Tests;

using FluentAssertions;
using Text = System.Text;
using IO = System.IO;

public sealed class ObjectPoolFactoryTests
{
    [Fact]
    public void Create_ShouldReturnPool()
    {
        using var pool = ObjectPoolFactory.Create(() => new TestItem());
        pool.Should().NotBeNull();
        var item = pool.Rent();
        item.Should().NotBeNull();
        pool.Return(item);
    }

    [Fact]
    public void Create_WithReset_ShouldReturnPool()
    {
        using var pool = ObjectPoolFactory.Create(
            () => new TestItem(),
            item => item.Reset());
        var item = pool.Rent();
        item.Value = 42;
        pool.Return(item);
        item.IsReset.Should().BeTrue();
    }

    [Fact]
    public void CreateHighPerformance_ShouldReturnPool()
    {
        using var pool = ObjectPoolFactory.CreateHighPerformance(() => new TestItem());
        pool.Should().NotBeNull();
        var item = pool.Rent();
        item.Should().NotBeNull();
        pool.Return(item);
    }

    [Fact]
    public void CreateNativeAot_ShouldReturnPool()
    {
        using var pool = ObjectPoolFactory.CreateNativeAot(() => new TestItem());
        pool.Should().NotBeNull();
        var item = pool.Rent();
        item.Should().NotBeNull();
        pool.Return(item);
    }

    [Fact]
    public void CreateNativeAot_WithReset_ShouldReturnPool()
    {
        using var pool = ObjectPoolFactory.CreateNativeAot(
            () => new TestItem(),
            item => item.Reset());
        var item = pool.Rent();
        item.Value = 42;
        pool.Return(item);
        item.IsReset.Should().BeTrue();
    }

    [Fact]
    public void CreateMemoryOptimized_ShouldReturnPool()
    {
        using var pool = ObjectPoolFactory.CreateMemoryOptimized(() => new TestItem());
        pool.Should().NotBeNull();
        var item = pool.Rent();
        item.Should().NotBeNull();
        pool.Return(item);
    }

    [Fact]
    public void CreateWithHandler_ShouldReturnPool()
    {
        using var pool = ObjectPoolFactory.CreateWithHandler(
            () => new TestItem(),
            reset: item => item.Reset());
        var item = pool.Rent();
        item.Should().NotBeNull();
        pool.Return(item);
    }

    [Fact]
    public void CreateThreadLocal_ShouldReturnPool()
    {
        using var pool = ObjectPoolFactory.CreateThreadLocal(() => new TestItem());
        pool.Should().NotBeNull();
        var item = pool.Rent();
        item.Should().NotBeNull();
        pool.Return(item);
    }
}

public sealed class StaticPoolTests
{
    [Fact]
    public void Shared_ShouldReturnPool()
    {
        StaticPool<TestItem>.Shared.Should().NotBeNull();
    }

    [Fact]
    public void Get_ShouldReturnInstance()
    {
        var item = StaticPool<TestItem>.Get();
        item.Should().NotBeNull();
        StaticPool<TestItem>.Return(item);
    }

    [Fact]
    public void GetPoolable_ShouldReturnPoolable()
    {
        using var poolable = StaticPool<TestItem>.GetPoolable();
        poolable.Instance.Should().NotBeNull();
    }
}

public sealed class ListPoolTests
{
    [Fact]
    public void Get_ShouldReturnList()
    {
        var list = ListPool<int>.Get();
        list.Should().NotBeNull();
        list.Count.Should().Be(0);
        ListPool<int>.Return(list);
    }

    [Fact]
    public void GetPoolable_ShouldReturnPoolable()
    {
        using var poolable = ListPool<string>.GetPoolable();
        poolable.Instance.Should().NotBeNull();
    }
}

public sealed class StringBuilderPoolTests
{
    [Fact]
    public void Get_ShouldReturnStringBuilder()
    {
        var sb = StringBuilderPool.Get();
        sb.Should().NotBeNull();
        StringBuilderPool.Return(sb);
    }

    [Fact]
    public void GetStringAndReturn_ShouldReturnStringAndRecycle()
    {
        var sb = StringBuilderPool.Get();
        sb.Append("hello");
        string result = StringBuilderPool.GetStringAndReturn(sb);
        result.Should().Be("hello");
    }

    [Fact]
    public void GetPoolable_ShouldReturnPoolable()
    {
        using var poolable = StringBuilderPool.GetPoolable();
        poolable.Instance.Should().NotBeNull();
    }
}

public sealed class ByteArrayPoolTests
{
    [Fact]
    public void Shared_ShouldReturnArrayPool()
    {
        ByteArrayPool.Shared.Should().NotBeNull();
    }

    [Fact]
    public void Shared_RentAndReturn_ShouldWork()
    {
        var array = ByteArrayPool.Shared.Rent(1024);
        array.Should().NotBeNull();
        array.Length.Should().BeGreaterThanOrEqualTo(1024);
        ByteArrayPool.Shared.Return(array);
    }
}

public sealed class MemoryStreamPoolTests
{
    [Fact]
    public void Get_ShouldReturnMemoryStream()
    {
        var ms = MemoryStreamPool.Get();
        ms.Should().NotBeNull();
        MemoryStreamPool.Return(ms);
    }

    [Fact]
    public void GetPoolable_ShouldReturnPoolable()
    {
        using var poolable = MemoryStreamPool.GetPoolable();
        poolable.Instance.Should().NotBeNull();
    }
}
