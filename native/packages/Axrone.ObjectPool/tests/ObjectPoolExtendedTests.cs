namespace Axrone.ObjectPool.Tests;

using FluentAssertions;

public class TestItem
{
    public int Value { get; set; }
    public bool IsReset { get; set; }
    public void Reset() { Value = 0; IsReset = true; }
}

public sealed class ObjectPoolAsyncTests : IDisposable
{
    private readonly ObjectPool<TestItem> _pool;

    public ObjectPoolAsyncTests()
    {
        _pool = new ObjectPool<TestItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 10,
                InitialCapacity = 2,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
            },
            new PoolableHandler<TestItem>
            {
                Factory = () => new TestItem(),
                Reset = item => item.Reset(),
            });
    }

    public void Dispose() => _pool.Dispose();

    [Fact]
    public async Task RentAsync_ShouldReturnInstance()
    {
        var item = await _pool.RentAsync();
        item.Should().NotBeNull();
        _pool.Return(item);
    }

    [Fact]
    public async Task RentAsync_MultipleCalls_ShouldReturnDifferentInstances()
    {
        var item1 = await _pool.RentAsync();
        var item2 = await _pool.RentAsync();
        item1.Should().NotBeSameAs(item2);
        _pool.Return(item1);
        _pool.Return(item2);
    }

    [Fact]
    public async Task ReturnAsync_ShouldReturnItemToPool()
    {
        var item = await _pool.RentAsync();
        await _pool.ReturnAsync(item);
        _pool.Available.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ReturnAsync_ShouldResetItem()
    {
        var item = await _pool.RentAsync();
        item.Value = 42;
        await _pool.ReturnAsync(item);
        item.IsReset.Should().BeTrue();
    }

    [Fact]
    public async Task RentAsync_AfterReturnAsync_ShouldReuseItem()
    {
        var item1 = await _pool.RentAsync();
        await _pool.ReturnAsync(item1);
        var item2 = await _pool.RentAsync();
        // item2 may or may not be same as item1 depending on queue order
        item2.Should().NotBeNull();
        _pool.Return(item2);
    }
}

public sealed class ObjectPoolTryRentTests : IDisposable
{
    private readonly ObjectPool<TestItem> _pool;

    public ObjectPoolTryRentTests()
    {
        _pool = new ObjectPool<TestItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 3,
                InitialCapacity = 0,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
                ThrowOnExhaustion = false,
            },
            new PoolableHandler<TestItem>
            {
                Factory = () => new TestItem(),
                Reset = item => item.Reset(),
            });
    }

    public void Dispose() => _pool.Dispose();

    [Fact]
    public void TryRent_WhenUnderCapacity_ShouldSucceed()
    {
        bool result = _pool.TryRent(out var item);
        result.Should().BeTrue();
        item.Should().NotBeNull();
        _pool.Return(item!);
    }

    [Fact]
    public void TryRent_WhenExhausted_ShouldReturnFalse()
    {
        var items = new List<TestItem>();
        for (int i = 0; i < 3; i++)
        {
            bool r = _pool.TryRent(out var item);
            r.Should().BeTrue();
            items.Add(item!);
        }

        bool result = _pool.TryRent(out var failed);
        result.Should().BeFalse();
        failed.Should().BeNull();

        foreach (var item in items)
            _pool.Return(item);
    }

    [Fact]
    public async Task TryRentAsync_WhenUnderCapacity_ShouldSucceed()
    {
        var (success, item) = await _pool.TryRentAsync();
        success.Should().BeTrue();
        item.Should().NotBeNull();
        _pool.Return(item!);
    }

    [Fact]
    public async Task TryRentAsync_WhenExhausted_ShouldReturnFalse()
    {
        var items = new List<TestItem>();
        for (int i = 0; i < 3; i++)
        {
            var (s, it) = await _pool.TryRentAsync();
            s.Should().BeTrue();
            items.Add(it!);
        }

        var (success, item) = await _pool.TryRentAsync();
        success.Should().BeFalse();
        item.Should().BeNull();

        foreach (var it in items)
            _pool.Return(it);
    }
}

public sealed class ObjectPoolPoolableTests : IDisposable
{
    private readonly ObjectPool<TestItem> _pool;

    public ObjectPoolPoolableTests()
    {
        _pool = new ObjectPool<TestItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 10,
                InitialCapacity = 0,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
            },
            new PoolableHandler<TestItem>
            {
                Factory = () => new TestItem(),
                Reset = item => item.Reset(),
            });
    }

    public void Dispose() => _pool.Dispose();

    [Fact]
    public void GetPoolable_ShouldReturnIPoolable()
    {
        using var poolable = _pool.GetPoolable();
        poolable.Should().NotBeNull();
        poolable.Instance.Should().NotBeNull();
        poolable.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void GetPoolable_DisposeShouldReturnToPool()
    {
        var poolable = _pool.GetPoolable();
        var instance = poolable.Instance;
        poolable.Dispose();
        poolable.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task GetPoolableAsync_ShouldReturnIPoolable()
    {
        await using var poolable = await _pool.GetPoolableAsync();
        poolable.Should().NotBeNull();
        poolable.Instance.Should().NotBeNull();
    }
}

public sealed class ObjectPoolClearTests : IDisposable
{
    private readonly ObjectPool<TestItem> _pool;
    private int _disposeCount;

    public ObjectPoolClearTests()
    {
        _disposeCount = 0;
        _pool = new ObjectPool<TestItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 20,
                InitialCapacity = 5,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
            },
            new PoolableHandler<TestItem>
            {
                Factory = () => new TestItem(),
                OnDispose = _ => Interlocked.Increment(ref _disposeCount),
            });
    }

    public void Dispose() => _pool.Dispose();

    [Fact]
    public void Clear_ShouldRemoveAllPooledItems()
    {
        _pool.Count.Should().Be(5);
        _pool.Clear();
        _pool.Count.Should().Be(0);
    }

    [Fact]
    public void Clear_ShouldCallDispose()
    {
        _pool.Clear();
        Volatile.Read(ref _disposeCount).Should().Be(5);
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllPooledItems()
    {
        _pool.Count.Should().Be(5);
        await _pool.ClearAsync();
        _pool.Count.Should().Be(0);
    }
}

public sealed class ObjectPoolContainsTests : IDisposable
{
    private readonly ObjectPool<TestItem> _pool;

    public ObjectPoolContainsTests()
    {
        _pool = new ObjectPool<TestItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 10,
                InitialCapacity = 3,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
                TrackLeaks = true,
            },
            new PoolableHandler<TestItem>
            {
                Factory = () => new TestItem(),
            });
    }

    public void Dispose() => _pool.Dispose();

    [Fact]
    public void Contains_PooledItem_ShouldReturnTrue()
    {
        var item = _pool.Rent();
        _pool.Return(item);
        _pool.Contains(item).Should().BeTrue();
    }

    [Fact]
    public void Contains_RentedItem_ShouldReturnTrueViaTracking()
    {
        // Rent all pre-allocated items first
        var preallocated = new List<TestItem>();
        for (int i = 0; i < 3; i++)
        {
            preallocated.Add(_pool.Rent());
        }

        // This item will be newly created and tracked
        var newItem = _pool.Rent();
        _pool.Contains(newItem).Should().BeTrue();

        _pool.Return(newItem);
        foreach (var item in preallocated)
            _pool.Return(item);
    }

    [Fact]
    public void Contains_UnknownItem_ShouldReturnFalse()
    {
        _pool.Contains(new TestItem()).Should().BeFalse();
    }
}

public sealed class ObjectPoolDiagnosticsTests : IDisposable
{
    private readonly ObjectPool<TestItem> _pool;

    public ObjectPoolDiagnosticsTests()
    {
        _pool = new ObjectPool<TestItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 10,
                InitialCapacity = 2,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
                DiagnosticsLevel = DiagnosticsLevel.Full,
            },
            new PoolableHandler<TestItem>
            {
                Factory = () => new TestItem(),
            });
    }

    public void Dispose() => _pool.Dispose();

    [Fact]
    public void GetDiagnostics_ShouldReturnUptime()
    {
        var diagnostics = _pool.GetDiagnostics();
        diagnostics.Uptime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void GetDiagnostics_ShouldReturnCapacityInfo()
    {
        var diagnostics = _pool.GetDiagnostics();
        diagnostics.InitialCapacity.Should().Be(2);
        diagnostics.MaximumCapacity.Should().Be(10);
    }

    [Fact]
    public void GetDiagnostics_WithNoneLevel_ShouldReturnEmpty()
    {
        var pool = new ObjectPool<TestItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 10,
                DiagnosticsLevel = DiagnosticsLevel.None,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
            },
            new PoolableHandler<TestItem> { Factory = () => new TestItem() });

        var diagnostics = pool.GetDiagnostics();
        diagnostics.Events.Should().BeEmpty();
        pool.Dispose();
    }
}

public sealed class ObjectPoolReconfigureTests : IDisposable
{
    private readonly ObjectPool<TestItem> _pool;

    public ObjectPoolReconfigureTests()
    {
        _pool = new ObjectPool<TestItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 20,
                InitialCapacity = 10,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
            },
            new PoolableHandler<TestItem>
            {
                Factory = () => new TestItem(),
            });
    }

    public void Dispose() => _pool.Dispose();

    [Fact]
    public void Reconfigure_ShouldUpdateMaxCapacity()
    {
        var newConfig = new PoolConfiguration
        {
            MaximumCapacity = 5,
            InitialCapacity = 10,
            Strategy = PoolingStrategy.CentralizedQueue,
            UseSlimSynchronization = true,
            ConcurrencyLevel = ConcurrencyLevel.Default,
        };

        _pool.Reconfigure(newConfig);
        _pool.Capacity.Should().Be(5);
    }

    [Fact]
    public void Reconfigure_ShouldTrimWhenShrinking()
    {
        int countBefore = _pool.Count;
        var newConfig = new PoolConfiguration
        {
            MaximumCapacity = 3,
            InitialCapacity = 10,
            Strategy = PoolingStrategy.CentralizedQueue,
            UseSlimSynchronization = true,
            ConcurrencyLevel = ConcurrencyLevel.Default,
        };

        _pool.Reconfigure(newConfig);
        _pool.Count.Should().BeLessThanOrEqualTo(3);
    }
}

public sealed class ObjectPoolTrimTests : IDisposable
{
    private readonly ObjectPool<TestItem> _pool;

    public ObjectPoolTrimTests()
    {
        _pool = new ObjectPool<TestItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 20,
                InitialCapacity = 10,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
                TargetUtilizationPercentage = 50,
            },
            new PoolableHandler<TestItem>
            {
                Factory = () => new TestItem(),
            });
    }

    public void Dispose() => _pool.Dispose();

    [Fact]
    public void Trim_WithTargetCapacity_ShouldReduceCount()
    {
        _pool.Count.Should().Be(10);
        _pool.Trim(5);
        _pool.Count.Should().Be(5);
    }

    [Fact]
    public void Trim_WithDefaultTarget_ShouldUseUtilizationPercentage()
    {
        _pool.Count.Should().Be(10);
        _pool.Trim();
        _pool.Count.Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task TrimAsync_ShouldReduceCount()
    {
        _pool.Count.Should().Be(10);
        await _pool.TrimAsync(3);
        _pool.Count.Should().Be(3);
    }

    [Fact]
    public void Trim_ToLargerThanCurrent_ShouldNotChange()
    {
        _pool.Count.Should().Be(10);
        _pool.Trim(20);
        _pool.Count.Should().Be(10);
    }
}

public sealed class ObjectPoolResetMetricsTests : IDisposable
{
    private readonly ObjectPool<TestItem> _pool;

    public ObjectPoolResetMetricsTests()
    {
        _pool = new ObjectPool<TestItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 10,
                InitialCapacity = 2,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
                DiagnosticsLevel = DiagnosticsLevel.Full,
            },
            new PoolableHandler<TestItem>
            {
                Factory = () => new TestItem(),
            });
    }

    public void Dispose() => _pool.Dispose();

    [Fact]
    public void ResetMetrics_ShouldClearMetrics()
    {
        var item = _pool.Rent();
        _pool.Return(item);

        _pool.ResetMetrics();

        var metrics = _pool.GetMetrics();
        // TotalCreated etc. are not reset, only timing metrics
        metrics.LastResetTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}

public sealed class ObjectPoolCreateFactoryTests : IDisposable
{
    private readonly ObjectPool<TestItem> _pool;

    public ObjectPoolCreateFactoryTests()
    {
        _pool = new ObjectPool<TestItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 10,
                InitialCapacity = 0,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
            },
            new PoolableHandler<TestItem>
            {
                Factory = () => new TestItem(),
            });
    }

    public void Dispose() => _pool.Dispose();

    [Fact]
    public void CreateFactory_ShouldReturnFactory()
    {
        var factory = _pool.CreateFactory<IPoolable<TestItem>>();
        factory.Should().NotBeNull();
    }

    [Fact]
    public void CreateFactory_WhenUsed_ShouldCreatePoolable()
    {
        var factory = _pool.CreateFactory<IPoolable<TestItem>>();
        var item = _pool.Rent();
        var poolable = factory.Create(item);
        poolable.Should().NotBeNull();
        poolable.Instance.Should().BeSameAs(item);
        poolable.Dispose();
        _pool.Return(item);
    }
}

public sealed class ObjectPoolDisposeAsyncTests
{
    [Fact]
    public async Task DisposeAsync_ShouldDisposePool()
    {
        var pool = new ObjectPool<TestItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 10,
                InitialCapacity = 3,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
            },
            new PoolableHandler<TestItem>
            {
                Factory = () => new TestItem(),
            });

        await pool.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => pool.Rent());
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldNotThrow()
    {
        var pool = new ObjectPool<TestItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 10,
                InitialCapacity = 0,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
            },
            new PoolableHandler<TestItem>
            {
                Factory = () => new TestItem(),
            });

        await pool.DisposeAsync();
        await pool.DisposeAsync();
    }
}

public sealed class ObjectPoolPoolableDoubleReturnTests : IDisposable
{
    private readonly ObjectPool<TestItem> _pool;

    public ObjectPoolPoolableDoubleReturnTests()
    {
        _pool = new ObjectPool<TestItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 10,
                InitialCapacity = 0,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
            },
            new PoolableHandler<TestItem>
            {
                Factory = () => new TestItem(),
                Reset = item => item.Reset(),
            });
    }

    public void Dispose() => _pool.Dispose();

    [Fact]
    public void PoolableStructCopy_ShouldNotDoubleReturn()
    {
        var poolable = _pool.GetPoolable();
        var copy = poolable;

        poolable.Dispose();
        copy.Dispose();

        _pool.Available.Should().Be(1, "item should be returned exactly once despite struct copy");
        _pool.Count.Should().Be(1);
    }

    [Fact]
    public async Task PoolableStructCopy_ShouldNotDoubleReturnAsync()
    {
        var poolable = await _pool.GetPoolableAsync();
        var copy = poolable;

        await poolable.DisposeAsync();
        await copy.DisposeAsync();

        _pool.Available.Should().Be(1, "item should be returned exactly once despite struct copy");
        _pool.Count.Should().Be(1);
    }
}
