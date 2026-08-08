using System.Collections.Concurrent;
using System.Linq;

namespace Axrone.ObjectPool.Tests;

public class ObjectPoolStrategyTests
{
    // ─── helpers ───────────────────────────────────────────────

    private static PoolConfiguration ConfigWithStrategy(
        PoolingStrategy strategy,
        int maxCapacity = 16,
        bool allowExpansion = false,
        bool throwOnExhaustion = false,
        bool clearOnReturn = false,
        DiagnosticsLevel diagnosticsLevel = DiagnosticsLevel.None,
        bool trackLeaks = false) =>
        new()
        {
            Strategy = strategy,
            MaximumCapacity = maxCapacity,
            AllowPoolExpansion = allowExpansion,
            ThrowOnExhaustion = throwOnExhaustion,
            ClearOnReturn = clearOnReturn,
            DiagnosticsLevel = diagnosticsLevel,
            TrackLeaks = trackLeaks,
        };

    private static PoolableHandler<List<int>> SimpleListHandler(
        Func<List<int>>? factory = null,
        Action<List<int>>? reset = null,
        Func<List<int>, bool>? validator = null) =>
        new()
        {
            Factory = factory ?? (() => new List<int>()),
            Reset = reset,
            Validator = validator,
        };

    // ─── 1. CentralizedQueue strategy: basic Rent/Return ────────

    [Fact]
    public void CentralizedQueue_RentAndReturn_ShouldWorkCorrectly()
    {
        var config = ConfigWithStrategy(PoolingStrategy.CentralizedQueue);
        using var pool = new ObjectPool<List<int>>(config, SimpleListHandler());

        var item = pool.Rent();
        item.Should().NotBeNull();
        item.Should().BeOfType<List<int>>();

        pool.Return(item);

        var reused = pool.Rent();
        reused.Should().BeSameAs(item);
    }

    // ─── 2. LockFree strategy: Rent creates new, Return adds back

    [Fact]
    public void LockFree_RentCreatesNewWhenEmpty_ReturnAddsBack()
    {
        var config = ConfigWithStrategy(PoolingStrategy.LockFree);
        using var pool = new ObjectPool<List<int>>(config, SimpleListHandler());

        pool.Count.Should().Be(0);

        var item = pool.Rent();
        item.Should().NotBeNull();
        pool.Count.Should().Be(1);
        pool.Available.Should().Be(0);

        pool.Return(item);
        pool.Available.Should().Be(1);

        var reused = pool.Rent();
        reused.Should().BeSameAs(item);
    }

    // ─── 3. ThreadLocal strategy: Rent/Return ───────────────────

    [Fact]
    public void ThreadLocal_RentAndReturn_ShouldWorkCorrectly()
    {
        var config = ConfigWithStrategy(PoolingStrategy.ThreadLocal);
        using var pool = new ObjectPool<List<int>>(config, SimpleListHandler());

        var item = pool.Rent();
        item.Should().NotBeNull();

        pool.Return(item);

        var reused = pool.Rent();
        reused.Should().BeSameAs(item);
    }

    // ─── 4. ThreadLocalWithPartitioning strategy: Rent/Return ───

    [Fact]
    public void ThreadLocalWithPartitioning_RentAndReturn_ShouldWorkCorrectly()
    {
        var config = ConfigWithStrategy(PoolingStrategy.ThreadLocalWithPartitioning);
        using var pool = new ObjectPool<List<int>>(config, SimpleListHandler());

        var item = pool.Rent();
        item.Should().NotBeNull();

        pool.Return(item);

        var reused = pool.Rent();
        reused.Should().BeSameAs(item);
    }

    // ─── 5. ShardedByThread strategy: Rent/Return ───────────────

    [Fact]
    public void ShardedByThread_RentAndReturn_ShouldWorkCorrectly()
    {
        var config = ConfigWithStrategy(PoolingStrategy.ShardedByThread);
        using var pool = new ObjectPool<List<int>>(config, SimpleListHandler());

        var item = pool.Rent();
        item.Should().NotBeNull();

        pool.Return(item);

        var reused = pool.Rent();
        reused.Should().BeSameAs(item);
    }

    // ─── 6. BoundedChannel strategy: Rent/Return ────────────────

    [Fact]
    public void BoundedChannel_RentAndReturn_ShouldWorkCorrectly()
    {
        var config = ConfigWithStrategy(PoolingStrategy.BoundedChannel);
        using var pool = new ObjectPool<List<int>>(config, SimpleListHandler());

        var item = pool.Rent();
        item.Should().NotBeNull();

        pool.Return(item);

        var reused = pool.Rent();
        reused.Should().BeSameAs(item);
    }

    // ─── 7. Pool exhaustion: ThrowOnExhaustion=true throws ──────

    [Fact]
    public void PoolExhaustion_ThrowOnExhaustionAndNoExpansion_ShouldThrowInvalidOperationException()
    {
        var config = ConfigWithStrategy(
            PoolingStrategy.CentralizedQueue,
            maxCapacity: 2,
            allowExpansion: false,
            throwOnExhaustion: true);
        using var pool = new ObjectPool<List<int>>(config, SimpleListHandler());

        var item1 = pool.Rent();
        var item2 = pool.Rent();

        // Pool is at capacity with all items rented out
        var act = () => pool.Rent();

        act.Should().Throw<InvalidOperationException>();

        // Clean up rented items
        pool.Return(item1);
        pool.Return(item2);
    }

    // ─── 8. Pool exhaustion without throw: creates beyond capacity

    [Fact]
    public void PoolExhaustion_NoThrow_ShouldCreateBeyondCapacity()
    {
        var config = ConfigWithStrategy(
            PoolingStrategy.CentralizedQueue,
            maxCapacity: 2,
            allowExpansion: false,
            throwOnExhaustion: false);
        using var pool = new ObjectPool<List<int>>(config, SimpleListHandler());

        var item1 = pool.Rent();
        var item2 = pool.Rent();

        // Should not throw — creates a third item beyond capacity
        var item3 = pool.Rent();
        item3.Should().NotBeNull();
        pool.Count.Should().Be(3);

        pool.Return(item1);
        pool.Return(item2);
        pool.Return(item3);
    }

    // ─── 9. Pool expansion: grows beyond MaximumCapacity ────────

    [Fact]
    public void PoolExpansion_AllowExpansion_ShouldGrowBeyondMaximumCapacity()
    {
        var config = ConfigWithStrategy(
            PoolingStrategy.CentralizedQueue,
            maxCapacity: 2,
            allowExpansion: true);
        using var pool = new ObjectPool<List<int>>(config, SimpleListHandler());

        var item1 = pool.Rent();
        var item2 = pool.Rent();
        var item3 = pool.Rent();

        pool.Count.Should().Be(3);
        pool.Count.Should().BeGreaterThan(config.MaximumCapacity);

        pool.Return(item1);
        pool.Return(item2);
        pool.Return(item3);
    }

    // ─── 10. DiagnosticsLevel.Full tracks metrics ───────────────

    [Fact]
    public void DiagnosticsFull_GetMetrics_ShouldReturnNonZeroTotalCreatedAfterRent()
    {
        var config = ConfigWithStrategy(
            PoolingStrategy.CentralizedQueue,
            diagnosticsLevel: DiagnosticsLevel.Full);
        using var pool = new ObjectPool<List<int>>(config, SimpleListHandler());

        var item = pool.Rent();

        var metrics = pool.GetMetrics();
        metrics.TotalCreated.Should().BeGreaterThan(0);

        pool.Return(item);
    }

    // ─── 11. TrackLeaks=true: pool does not crash ───────────────

    [Fact]
    public void TrackLeaks_Enabled_ShouldNotCrashDuringRentAndReturn()
    {
        var config = ConfigWithStrategy(
            PoolingStrategy.CentralizedQueue,
            trackLeaks: true);
        using var pool = new ObjectPool<List<int>>(config, SimpleListHandler());

        var item1 = pool.Rent();
        var item2 = pool.Rent();
        pool.Return(item1);
        pool.Return(item2);

        var item3 = pool.Rent();
        item3.Should().NotBeNull();
        pool.Count.Should().BeGreaterThanOrEqualTo(1);

        pool.Return(item3);
    }

    // ─── 12. Concurrent access with different strategies ────────

    [Theory]
    [InlineData(PoolingStrategy.CentralizedQueue)]
    [InlineData(PoolingStrategy.LockFree)]
    [InlineData(PoolingStrategy.ThreadLocal)]
    public void ConcurrentAccess_MultipleThreads_ShouldNotCorruptState(PoolingStrategy strategy)
    {
        var config = ConfigWithStrategy(
            strategy,
            maxCapacity: 200,
            allowExpansion: true);
        using var pool = new ObjectPool<List<int>>(config, SimpleListHandler());

        const int threadCount = 8;
        const int iterationsPerThread = 100;
        var barrier = new Barrier(threadCount);
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            try
            {
                barrier.SignalAndWait();

                for (int i = 0; i < iterationsPerThread; i++)
                {
                    var item = pool.Rent();
                    item.Add(i);
                    pool.Return(item);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        Task.WaitAll(tasks);

        exceptions.Should().BeEmpty("concurrent Rent/Return should not throw");
        pool.Count.Should().BeGreaterThan(0);
        pool.Available.Should().BeGreaterThanOrEqualTo(0);
    }

    // ─── 13. Validator: invalid items discarded on Return ───────

    [Fact]
    public void Validator_ItemFailsValidation_ShouldDiscardOnReturn()
    {
        var config = ConfigWithStrategy(PoolingStrategy.CentralizedQueue);
        var handler = new PoolableHandler<List<int>>
        {
            Factory = () => new List<int>(),
            Validator = item => item.Count > 0, // only valid if non-empty
        };
        using var pool = new ObjectPool<List<int>>(config, handler);

        var item = pool.Rent();
        // item is empty — validator returns false
        pool.Return(item);

        // Item should have been discarded, not returned to the pool
        pool.Available.Should().Be(0);
    }

    [Fact]
    public void Validator_ItemPassesValidation_ShouldReturnToPool()
    {
        var config = ConfigWithStrategy(PoolingStrategy.CentralizedQueue);
        var handler = new PoolableHandler<List<int>>
        {
            Factory = () => new List<int>(),
            Validator = item => item.Count > 0,
        };
        using var pool = new ObjectPool<List<int>>(config, handler);

        var item = pool.Rent();
        item.Add(42); // make it non-empty so validation passes
        pool.Return(item);

        // Item should be back in the pool
        pool.Available.Should().Be(1);
    }

    // ─── 14. ClearOnReturn: IClearable items get cleared ────────

    private sealed class ClearableItem : IClearable
    {
        public bool WasCleared { get; private set; }

        public List<int> Data { get; } = new();

        public void Clear()
        {
            Data.Clear();
            WasCleared = true;
        }
    }

    [Fact]
    public void ClearOnReturn_WithClearableItem_ShouldCallClearOnReturn()
    {
        var config = ConfigWithStrategy(
            PoolingStrategy.CentralizedQueue,
            clearOnReturn: true);
        var handler = new PoolableHandler<ClearableItem>
        {
            Factory = () => new ClearableItem(),
        };
        using var pool = new ObjectPool<ClearableItem>(config, handler);

        var item = pool.Rent();
        item.Data.Add(1);
        item.Data.Add(2);
        item.Data.Count.Should().Be(2);

        pool.Return(item);

        item.WasCleared.Should().BeTrue();
        item.Data.Should().BeEmpty();
    }

    [Fact]
    public void ClearOnReturn_Disabled_ShouldNotCallClear()
    {
        var config = ConfigWithStrategy(
            PoolingStrategy.CentralizedQueue,
            clearOnReturn: false);
        var handler = new PoolableHandler<ClearableItem>
        {
            Factory = () => new ClearableItem(),
        };
        using var pool = new ObjectPool<ClearableItem>(config, handler);

        var item = pool.Rent();
        item.Data.Add(1);
        pool.Return(item);

        item.WasCleared.Should().BeFalse();
        item.Data.Should().HaveCount(1);
    }

    // ─── 15. IPoolable: Reset() called when no Reset handler ────

    private sealed class PoolableItem : IPoolable
    {
        public bool WasReset { get; private set; }

        public int Value { get; set; }

        public void Reset()
        {
            WasReset = true;
            Value = 0;
        }

        public ValueTask ResetAsync()
        {
            Reset();
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public void IPoolable_WithNoResetHandler_ShouldCallResetOnReturn()
    {
        var config = ConfigWithStrategy(PoolingStrategy.CentralizedQueue);
        var handler = new PoolableHandler<PoolableItem>
        {
            Factory = () => new PoolableItem(),
            // No Reset handler set — IPoolable.Reset() should be called
        };
        using var pool = new ObjectPool<PoolableItem>(config, handler);

        var item = pool.Rent();
        item.Value = 42;
        pool.Return(item);

        item.WasReset.Should().BeTrue();
        item.Value.Should().Be(0);
    }

    [Fact]
    public void IPoolable_WithExplicitResetHandler_ShouldCallHandlerNotPoolableReset()
    {
        bool handlerCalled = false;
        var config = ConfigWithStrategy(PoolingStrategy.CentralizedQueue);
        var handler = new PoolableHandler<PoolableItem>
        {
            Factory = () => new PoolableItem(),
            Reset = item =>
            {
                handlerCalled = true;
                item.Value = -1;
            },
        };
        using var pool = new ObjectPool<PoolableItem>(config, handler);

        var item = pool.Rent();
        item.Value = 42;
        pool.Return(item);

        handlerCalled.Should().BeTrue();
        item.Value.Should().Be(-1);
        // IPoolable.Reset() should NOT have been called because explicit handler was used
        item.WasReset.Should().BeFalse();
    }
}
