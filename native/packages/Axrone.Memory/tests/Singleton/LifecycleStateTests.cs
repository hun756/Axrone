using Enterprise.Patterns.Singleton;

namespace Axrone.Memory.Tests;

/// <summary>
/// Exhaustive lifecycle state machine transition tests.
/// Validates every valid and invalid state transition for all singleton variants.
/// </summary>
public class LifecycleStateTests
{
    // ── Singleton<T> state tests ──────────────────────────────────────

    [Fact]
    public void Singleton_InitialState_IsUninitialized()
    {
        Singleton<UntouchedService>.IsInitialized.Should().BeFalse();
        Singleton<UntouchedService>.Reset();
    }

    [Fact]
    public void Singleton_AfterSetFactory_BeforeAccess_IsUninitialized()
    {
        Singleton<SimpleService>.SetFactory(() => new SimpleService());
        // Factory set but not yet invoked — Lazy<T> hasn't created value
        Singleton<SimpleService>.IsInitialized.Should().BeFalse();
        Singleton<SimpleService>.Reset();
    }

    [Fact]
    public void Singleton_AfterAccess_IsInitialized()
    {
        Singleton<SimpleService>.SetFactory(() => new SimpleService());
        _ = Singleton<SimpleService>.Instance;
        Singleton<SimpleService>.IsInitialized.Should().BeTrue();
        Singleton<SimpleService>.Reset();
    }

    [Fact]
    public void Singleton_AfterReset_IsUninitialized()
    {
        Singleton<SimpleService>.SetFactory(() => new SimpleService());
        _ = Singleton<SimpleService>.Instance;
        Singleton<SimpleService>.Reset();
        Singleton<SimpleService>.IsInitialized.Should().BeFalse();
    }

    // ── LazySingleton<T> exhaustive state transitions ─────────────────

    [Fact]
    public void LazySingleton_FullLifecycle_UninitializedToDisposed()
    {
        var singleton = new LazySingleton<SimpleService>(() => new SimpleService());

        // Uninitialized
        singleton.State.Should().Be(SingletonLifecycleState.Uninitialized);
        singleton.IsValueCreated.Should().BeFalse();

        // Access → Initializing → Initialized
        _ = singleton.Value;
        singleton.State.Should().Be(SingletonLifecycleState.Initialized);
        singleton.IsValueCreated.Should().BeTrue();

        // Dispose → Disposing → Disposed
        singleton.Dispose();
        singleton.State.Should().Be(SingletonLifecycleState.Disposed);
    }

    [Fact]
    public void LazySingleton_FaultedState_OnFactoryException()
    {
        var singleton = new LazySingleton<SimpleService>(() => throw new InvalidOperationException("boom"));

        var act = () => singleton.Value;
        act.Should().Throw<SingletonInitializationException>();

        singleton.State.Should().Be(SingletonLifecycleState.Faulted);
    }

    [Fact]
    public void LazySingleton_FaultedState_IsTerminal()
    {
        var singleton = new LazySingleton<SimpleService>(() => throw new InvalidOperationException("boom"));

        // First access → Faulted
        var act1 = () => singleton.Value;
        act1.Should().Throw<SingletonInitializationException>();
        singleton.State.Should().Be(SingletonLifecycleState.Faulted);

        // Second access → still Faulted, same exception behavior
        var act2 = () => singleton.Value;
        act2.Should().Throw<SingletonInitializationException>();
        singleton.State.Should().Be(SingletonLifecycleState.Faulted);
    }

    [Fact]
    public void LazySingleton_DisposeBeforeAccess_TransitionsToDisposed()
    {
        var singleton = new LazySingleton<SimpleService>(() => new SimpleService());

        singleton.State.Should().Be(SingletonLifecycleState.Uninitialized);
        singleton.Dispose();
        singleton.State.Should().Be(SingletonLifecycleState.Disposed);
    }

    [Fact]
    public void LazySingleton_DoubleDispose_IsIdempotent()
    {
        var singleton = new LazySingleton<DisposableService>(() => new DisposableService());
        _ = singleton.Value;

        singleton.Dispose();
        singleton.State.Should().Be(SingletonLifecycleState.Disposed);

        singleton.Dispose(); // Should not throw
        singleton.State.Should().Be(SingletonLifecycleState.Disposed);
    }

    [Fact]
    public async Task LazySingleton_DisposeAsync_AfterSyncDispose_IsIdempotent()
    {
        var singleton = new LazySingleton<DisposableService>(() => new DisposableService());
        _ = singleton.Value;

#pragma warning disable CA1849 // Intentionally testing sync dispose path
        singleton.Dispose();
#pragma warning restore CA1849
        await singleton.DisposeAsync(); // Should not throw
        singleton.State.Should().Be(SingletonLifecycleState.Disposed);
    }

    // ── AsyncSingleton<T> exhaustive state transitions ────────────────

    [Fact]
    public async Task AsyncSingleton_FullLifecycle_UninitializedToDisposed()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => new ValueTask<SimpleService>(new SimpleService()));

        singleton.State.Should().Be(SingletonLifecycleState.Uninitialized);
        singleton.IsValueCreated.Should().BeFalse();

        _ = await singleton.GetValueAsync();
        singleton.State.Should().Be(SingletonLifecycleState.Initialized);
        singleton.IsValueCreated.Should().BeTrue();

        await singleton.DisposeAsync();
        singleton.State.Should().Be(SingletonLifecycleState.Disposed);
    }

    [Fact]
    public async Task AsyncSingleton_FaultedState_OnFactoryException()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => throw new InvalidOperationException("boom"));

        await singleton.Invoking(s => s.GetValueAsync().AsTask())
            .Should().ThrowAsync<SingletonInitializationException>();

        singleton.State.Should().Be(SingletonLifecycleState.Faulted);
        singleton.IsValueCreated.Should().BeFalse();
    }

    [Fact]
    public async Task AsyncSingleton_DisposeBeforeAccess_TransitionsToDisposed()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => new ValueTask<SimpleService>(new SimpleService()));

        singleton.State.Should().Be(SingletonLifecycleState.Uninitialized);
        await singleton.DisposeAsync();
        singleton.State.Should().Be(SingletonLifecycleState.Disposed);
    }

    [Fact]
    public async Task AsyncSingleton_DoubleDisposeAsync_IsIdempotent()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => new ValueTask<SimpleService>(new SimpleService()));

        _ = await singleton.GetValueAsync();
        await singleton.DisposeAsync();
        await singleton.DisposeAsync(); // Should not throw
        singleton.State.Should().Be(SingletonLifecycleState.Disposed);
    }

    [Fact]
    public async Task AsyncSingleton_SyncDispose_AfterAccess_TransitionsToDisposed()
    {
        var singleton = new AsyncSingleton<SimpleService>(
            ct => new ValueTask<SimpleService>(new SimpleService()));

        _ = await singleton.GetValueAsync();
#pragma warning disable CA1849 // Intentionally testing sync dispose path
        singleton.Dispose();
#pragma warning restore CA1849
        singleton.State.Should().Be(SingletonLifecycleState.Disposed);
    }

    // ── SingletonLifecycleState enum completeness ─────────────────────

    [Fact]
    public void LifecycleState_HasAllExpectedValues()
    {
        var values = Enum.GetValues<SingletonLifecycleState>();
        values.Should().HaveCount(6);
        values.Should().Contain(SingletonLifecycleState.Uninitialized);
        values.Should().Contain(SingletonLifecycleState.Initializing);
        values.Should().Contain(SingletonLifecycleState.Initialized);
        values.Should().Contain(SingletonLifecycleState.Faulted);
        values.Should().Contain(SingletonLifecycleState.Disposing);
        values.Should().Contain(SingletonLifecycleState.Disposed);
    }

    [Fact]
    public void LifecycleState_IntegerValues_AreSequential()
    {
        ((int)SingletonLifecycleState.Uninitialized).Should().Be(0);
        ((int)SingletonLifecycleState.Initializing).Should().Be(1);
        ((int)SingletonLifecycleState.Initialized).Should().Be(2);
        ((int)SingletonLifecycleState.Faulted).Should().Be(3);
        ((int)SingletonLifecycleState.Disposing).Should().Be(4);
        ((int)SingletonLifecycleState.Disposed).Should().Be(5);
    }
}
