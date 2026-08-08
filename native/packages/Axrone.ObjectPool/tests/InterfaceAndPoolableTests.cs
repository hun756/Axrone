using Axrone.ObjectPool;

namespace Axrone.ObjectPool.Tests;

public class InterfaceTests
{
    private sealed class TestItem
    {
        public int Value { get; set; }
    }

    [Fact]
    public void IObjectPool_ShouldDefineRentMethod()
    {
        typeof(IObjectPool<TestItem>).GetMethod("Rent").Should().NotBeNull();
    }

    [Fact]
    public void IObjectPool_ShouldDefineReturnMethod()
    {
        typeof(IObjectPool<TestItem>).GetMethod("Return").Should().NotBeNull();
    }

    [Fact]
    public void IObjectPool_ShouldDefineClearMethod()
    {
        typeof(IObjectPool<TestItem>).GetMethod("Clear").Should().NotBeNull();
    }

    [Fact]
    public void IObjectPool_ShouldDefineCountProperty()
    {
        typeof(IObjectPool<TestItem>).GetProperty("Count").Should().NotBeNull();
    }

    [Fact]
    public void IObjectPool_ShouldDefineCapacityProperty()
    {
        typeof(IObjectPool<TestItem>).GetProperty("Capacity").Should().NotBeNull();
    }

    [Fact]
    public void IObjectPool_ShouldDefineAvailableProperty()
    {
        typeof(IObjectPool<TestItem>).GetProperty("Available").Should().NotBeNull();
    }

    [Fact]
    public void IObjectPool_ShouldImplementIDisposable()
    {
        typeof(IObjectPool<TestItem>).Should().Implement<IDisposable>();
    }

    [Fact]
    public void IObjectPool_ShouldImplementIAsyncDisposable()
    {
        typeof(IObjectPool<TestItem>).Should().Implement<IAsyncDisposable>();
    }

    [Fact]
    public void IPoolable_ShouldDefineInstanceProperty()
    {
        typeof(IPoolable<TestItem>).GetProperty("Instance").Should().NotBeNull();
    }

    [Fact]
    public void IPoolable_ShouldDefineIsDisposedProperty()
    {
        typeof(IPoolable<TestItem>).GetProperty("IsDisposed").Should().NotBeNull();
    }

    [Fact]
    public void IPoolable_ShouldDefineDetachMethod()
    {
        typeof(IPoolable<TestItem>).GetMethod("Detach").Should().NotBeNull();
    }

    [Fact]
    public void IClearable_ShouldDefineClearMethod()
    {
        typeof(IClearable).GetMethod("Clear").Should().NotBeNull();
    }
}

public class PoolableHandlerTests
{
    [Fact]
    public void PoolableHandler_ShouldStoreFactory()
    {
        Func<List<int>> factory = () => new List<int>();
        var handler = new PoolableHandler<List<int>> { Factory = factory };
        handler.Factory.Should().BeSameAs(factory);
    }

    [Fact]
    public void PoolableHandler_ShouldStoreReset()
    {
        Action<List<int>> reset = list => list.Clear();
        var handler = new PoolableHandler<List<int>> { Reset = reset };
        handler.Reset.Should().BeSameAs(reset);
    }

    [Fact]
    public void PoolableHandler_ShouldStoreOnRent()
    {
        Action<List<int>> onRent = _ => { };
        var handler = new PoolableHandler<List<int>> { OnRent = onRent };
        handler.OnRent.Should().BeSameAs(onRent);
    }

    [Fact]
    public void PoolableHandler_ShouldStoreOnReturn()
    {
        Action<List<int>> onReturn = _ => { };
        var handler = new PoolableHandler<List<int>> { OnReturn = onReturn };
        handler.OnReturn.Should().BeSameAs(onReturn);
    }

    [Fact]
    public void PoolableHandler_ShouldStoreOnDispose()
    {
        Action<List<int>> onDispose = _ => { };
        var handler = new PoolableHandler<List<int>> { OnDispose = onDispose };
        handler.OnDispose.Should().BeSameAs(onDispose);
    }

    [Fact]
    public void PoolableHandler_ShouldStoreValidator()
    {
        Func<List<int>, bool> validator = _ => true;
        var handler = new PoolableHandler<List<int>> { Validator = validator };
        handler.Validator.Should().BeSameAs(validator);
    }

    [Fact]
    public void PoolableHandler_DefaultCallbacks_ShouldBeNull()
    {
        var handler = new PoolableHandler<List<int>> { Factory = () => new List<int>() };
        handler.Reset.Should().BeNull();
        handler.OnRent.Should().BeNull();
        handler.OnReturn.Should().BeNull();
        handler.OnDispose.Should().BeNull();
        handler.Validator.Should().BeNull();
    }
}

public class PoolableObjectInfoTests
{
    [Fact]
    public void PoolObjectInfo_ShouldStoreAllFields()
    {
        var now = DateTime.UtcNow;
        var info = new PoolObjectInfo<List<int>>
        {
            Instance = new List<int> { 1, 2, 3 },
            CreationTime = now,
            Generation = 1,
            RentCount = 5,
            TotalRentTime = TimeSpan.FromMilliseconds(100),
            IsPoolable = false,
            IsActive = true,
            Id = Guid.NewGuid(),
        };

        info.Instance.Should().HaveCount(3);
        info.CreationTime.Should().Be(now);
        info.Generation.Should().Be(1);
        info.RentCount.Should().Be(5);
        info.TotalRentTime.Should().Be(TimeSpan.FromMilliseconds(100));
        info.IsPoolable.Should().BeFalse();
        info.IsActive.Should().BeTrue();
        info.Id.Should().NotBe(Guid.Empty);
    }
}

public class PoolableTests
{
    [Fact]
    public void Poolable_Instance_ShouldReturnObject()
    {
        var item = new List<int> { 1, 2, 3 };
        var poolable = new Poolable<List<int>>(item, null);

        poolable.Instance.Should().BeSameAs(item);
    }

    [Fact]
    public void Poolable_IsDisposed_ShouldBeFalseInitially()
    {
        var item = new List<int>();
        var poolable = new Poolable<List<int>>(item, null);

        poolable.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void Poolable_Dispose_ShouldSetIsDisposed()
    {
        var item = new List<int>();
        var poolable = new Poolable<List<int>>(item, null);

        poolable.Dispose();

        poolable.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Poolable_Dispose_ShouldReturnToPool()
    {
        var item = new List<int>();
        List<int>? returnedItem = null;

        var mockPool = new MockPool<List<int>>(onReturn: i => returnedItem = i);
        var poolable = new Poolable<List<int>>(item, mockPool);

        poolable.Dispose();

        returnedItem.Should().BeSameAs(item);
    }

    [Fact]
    public void Poolable_Detach_ShouldPreventReturnToPool()
    {
        var item = new List<int>();
        List<int>? returnedItem = null;

        var mockPool = new MockPool<List<int>>(onReturn: i => returnedItem = i);
        var poolable = new Poolable<List<int>>(item, mockPool);

        poolable.Detach();
        poolable.Dispose();

        returnedItem.Should().BeNull();
    }

    [Fact]
    public void Poolable_DoubleDispose_ShouldNotReturnTwice()
    {
        var item = new List<int>();
        int returnCount = 0;

        var mockPool = new MockPool<List<int>>(onReturn: _ => returnCount++);
        var poolable = new Poolable<List<int>>(item, mockPool);

        poolable.Dispose();
        poolable.Dispose();

        returnCount.Should().Be(1);
    }

    [Fact]
    public void Poolable_Age_ShouldReturnTimeSinceRent()
    {
        var item = new List<int>();
        var poolable = new Poolable<List<int>>(item, null);

        poolable.Age.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void Poolable_RentTime_ShouldBeRecent()
    {
        var item = new List<int>();
        var poolable = new Poolable<List<int>>(item, null);

        poolable.RentTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    private sealed class MockPool<T> : IObjectPool<T> where T : class
    {
        private readonly Action<T>? _onReturn;

        public MockPool(Action<T>? onReturn = null) => _onReturn = onReturn;

        public T Rent() => throw new NotImplementedException();
        public ValueTask<T> RentAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public void Return(T item) => _onReturn?.Invoke(item);
        public ValueTask ReturnAsync(T item, CancellationToken ct = default) { Return(item); return ValueTask.CompletedTask; }
        public void ReturnPoolable(int poolableId, T item) => Return(item);
        public ValueTask ReturnPoolableAsync(int poolableId, T item) { Return(item); return ValueTask.CompletedTask; }
        public bool TryRent([NotNullWhen(true)] out T? item) { item = default; return false; }
        public ValueTask<(bool, T?)> TryRentAsync(CancellationToken ct = default) => new((false, default(T)));
        public IPoolable<T> GetPoolable() => throw new NotImplementedException();
        public ValueTask<IPoolable<T>> GetPoolableAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public void Clear() { }
        public ValueTask ClearAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public bool Contains(T item) => false;
        public int Count => 0;
        public int Capacity => 0;
        public int Available => 0;
        public PoolMetrics GetMetrics() => default;
        public PoolDiagnostics GetDiagnostics() => default;
        public void Reconfigure(PoolConfiguration config) { }
        public void Trim(int targetCapacity = -1) { }
        public ValueTask TrimAsync(int targetCapacity = -1, CancellationToken ct = default) => ValueTask.CompletedTask;
        public void ResetMetrics() { }
        public IPoolableFactory<T, TResult> CreateFactory<TResult>() => throw new NotImplementedException();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
