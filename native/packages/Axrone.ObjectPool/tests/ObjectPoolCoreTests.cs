namespace Axrone.ObjectPool.Tests;

public class ObjectPoolCoreTests
{
    // ─── helpers ───────────────────────────────────────────────

    private static PoolConfiguration SimpleConfig(int initial = 0, int max = 16) =>
        new()
        {
            InitialCapacity = initial,
            MaximumCapacity = max,
            ClearOnReturn = false,
            ThrowOnExhaustion = false,
        };

    private static PoolableHandler<List<int>> SimpleHandler(
        Func<List<int>>? factory = null,
        Action<List<int>>? reset = null,
        Action<List<int>>? onRent = null,
        Action<List<int>>? onReturn = null) =>
        new()
        {
            Factory = factory ?? (() => new List<int>()),
            Reset = reset,
            OnRent = onRent,
            OnReturn = onReturn,
        };

    // ─── 1. Construction with PoolConfiguration + PoolableHandler ─

    [Fact]
    public void Constructor_WithConfigAndHandler_ShouldStartWithCountZero()
    {
        using var pool = new ObjectPool<List<int>>(SimpleConfig(), SimpleHandler());

        pool.Count.Should().Be(0);
    }

    // ─── 2. Convenience constructor (factory only) ──────────────

    [Fact]
    public void ConvenienceConstructor_FactoryOnly_ShouldCreatePool()
    {
        using var pool = new ObjectPool<List<int>>(() => new List<int>());

        pool.Count.Should().Be(0);
        pool.Available.Should().Be(0);
    }

    // ─── 3. Convenience constructor with initialCapacity > 0 ────

    [Fact]
    public void ConvenienceConstructor_WithInitialCapacity_ShouldPreallocateItems()
    {
        using var pool = new ObjectPool<List<int>>(
            () => new List<int>(),
            initialCapacity: 5);

        pool.Count.Should().Be(5);
        pool.Available.Should().Be(5);
    }

    // ─── 4. Rent() returns a new instance when pool is empty ────

    [Fact]
    public void Rent_EmptyPool_ShouldReturnNewInstance()
    {
        using var pool = new ObjectPool<List<int>>(() => new List<int>());

        var item = pool.Rent();

        item.Should().NotBeNull();
        item.Should().BeOfType<List<int>>();
    }

    // ─── 5. Rent() returns existing instance after Return() ─────

    [Fact]
    public void Rent_AfterReturn_ShouldReturnSameInstance()
    {
        using var pool = new ObjectPool<List<int>>(() => new List<int>());

        var item = pool.Rent();
        pool.Return(item);
        var rented = pool.Rent();

        rented.Should().BeSameAs(item);
    }

    // ─── 6. Return() increments Count / decrements Available ────

    [Fact]
    public void Return_ShouldIncrementCountAndDecrementAvailable()
    {
        using var pool = new ObjectPool<List<int>>(() => new List<int>());

        var item = pool.Rent();
        pool.Count.Should().Be(1);
        pool.Available.Should().Be(0);

        pool.Return(item);
        pool.Count.Should().Be(1);
        pool.Available.Should().Be(1);
    }

    // ─── 7. After Rent(), Count stays same but Available decreases

    [Fact]
    public void Rent_FromNonEmptyPool_ShouldKeepCountButDecreaseAvailable()
    {
        using var pool = new ObjectPool<List<int>>(
            () => new List<int>(),
            initialCapacity: 3);

        pool.Count.Should().Be(3);
        pool.Available.Should().Be(3);

        var item = pool.Rent();
        pool.Count.Should().Be(3);
        pool.Available.Should().Be(2);
    }

    // ─── 8. Return() calls the reset action ─────────────────────

    [Fact]
    public void Return_WithResetAction_ShouldInvokeReset()
    {
        bool resetCalled = false;
        using var pool = new ObjectPool<List<int>>(
            SimpleHandler(
                reset: list =>
                {
                    resetCalled = true;
                    list.Clear();
                }));

        var item = pool.Rent();
        item.Add(42);
        pool.Return(item);

        resetCalled.Should().BeTrue();
        item.Should().BeEmpty();
    }

    // ─── 9. Rent() throws ObjectDisposedException after Dispose ──

    [Fact]
    public void Rent_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var pool = new ObjectPool<List<int>>(() => new List<int>());
        pool.Dispose();

        var act = () => pool.Rent();

        act.Should().Throw<ObjectDisposedException>();
    }

    // ─── 10. Return() throws ObjectDisposedException after Dispose

    [Fact]
    public void Return_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var pool = new ObjectPool<List<int>>(() => new List<int>());
        var item = pool.Rent();
        pool.Dispose();

        var act = () => pool.Return(item);

        act.Should().Throw<ObjectDisposedException>();
    }

    // ─── 11. Return(null) throws ArgumentNullException ───────────

    [Fact]
    public void Return_Null_ShouldThrowArgumentNullException()
    {
        using var pool = new ObjectPool<List<int>>(() => new List<int>());

        var act = () => pool.Return(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ─── 12. Dispose() is idempotent ────────────────────────────

    [Fact]
    public void Dispose_CalledTwice_ShouldNotThrow()
    {
        var pool = new ObjectPool<List<int>>(() => new List<int>());

        pool.Dispose();
        var act = () => pool.Dispose();

        act.Should().NotThrow();
    }

    // ─── 13. Capacity returns MaximumCapacity from configuration ─

    [Fact]
    public void Capacity_ShouldReturnMaximumCapacityFromConfig()
    {
        using var pool = new ObjectPool<List<int>>(SimpleConfig(max: 64), SimpleHandler());

        pool.Capacity.Should().Be(64);
    }

    // ─── 14. Available = Count - rented count ───────────────────

    [Fact]
    public void Available_ShouldEqualCountMinusRented()
    {
        using var pool = new ObjectPool<List<int>>(
            () => new List<int>(),
            initialCapacity: 4);

        pool.Available.Should().Be(4);

        var a = pool.Rent();
        var b = pool.Rent();
        pool.Available.Should().Be(2);

        pool.Return(a);
        pool.Available.Should().Be(3);

        pool.Return(b);
        pool.Available.Should().Be(4);
    }

    // ─── 15. Multiple Rent/Return cycles work correctly ─────────

    [Fact]
    public void MultipleRentReturnCycles_ShouldBehaveCorrectly()
    {
        using var pool = new ObjectPool<List<int>>(() => new List<int>());

        for (int i = 0; i < 10; i++)
        {
            var item = pool.Rent();
            item.Should().NotBeNull();
            pool.Count.Should().Be(1);
            pool.Available.Should().Be(0);

            pool.Return(item);
            pool.Count.Should().Be(1);
            pool.Available.Should().Be(1);
        }
    }

    // ─── 16. Rent() calls the OnRent handler ────────────────────

    [Fact]
    public void Rent_WithOnRentHandler_ShouldInvokeHandler()
    {
        List<int>? rentedItem = null;
        using var pool = new ObjectPool<List<int>>(
            SimpleHandler(onRent: item => rentedItem = item));

        var item = pool.Rent();

        rentedItem.Should().NotBeNull();
        rentedItem.Should().BeSameAs(item);
    }

    // ─── 17. Return() calls the OnReturn handler ────────────────

    [Fact]
    public void Return_WithOnReturnHandler_ShouldInvokeHandler()
    {
        List<int>? returnedItem = null;
        using var pool = new ObjectPool<List<int>>(
            SimpleHandler(onReturn: item => returnedItem = item));

        var item = pool.Rent();
        pool.Return(item);

        returnedItem.Should().NotBeNull();
        returnedItem.Should().BeSameAs(item);
    }
}
