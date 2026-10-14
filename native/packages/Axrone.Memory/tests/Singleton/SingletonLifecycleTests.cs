using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests.Singleton;

public sealed class SingletonLifecycleTests
{
    [Fact]
    public async Task Complete_DrainAsync_CompletesAfterLeasesReleased()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();
        var instance = engine.Instance;

        var lease = engine.AcquireLease();
        engine.Complete();

        var drainTask = engine.DrainAsync();
        drainTask.IsCompleted.Should().BeFalse();

        lease.Dispose();

        var result = await drainTask.ConfigureAwait(false);
        result.Status.Should().Be(DrainStatus.Completed);
        result.RemainingLeases.Value.Should().Be(0);
        instance.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DrainAsync_WithoutComplete_StillCompletes()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();
        _ = engine.Instance;

        var result = await engine.DrainAsync().ConfigureAwait(false);

        result.Status.Should().Be(DrainStatus.Completed);
        engine.GetMetadata().Status.Should().Be(LifecycleStatus.Drained);
    }

    [Fact]
    public void TryDrain_WithoutHeldLeases_CompletesSynchronously()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();
        _ = engine.Instance;

        engine.Complete();

        var ok = engine.TryDrain(TimeSpan.FromSeconds(5), out var result);

        ok.Should().BeTrue();
        result.Status.Should().Be(DrainStatus.Completed);
    }

    [Fact]
    public void TryDrain_WithHeldLease_TimesOut()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();
        _ = engine.Instance;

        var lease = engine.AcquireLease();
        engine.Complete();

        var ok = engine.TryDrain(TimeSpan.FromMilliseconds(50), out var result);

        ok.Should().BeFalse();
        result.Status.Should().Be(DrainStatus.TimedOut);
        result.RemainingLeases.Value.Should().Be(1);

        lease.Dispose();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var engine = new Singleton<SimpleService, CountingFactory>();
        var instance = engine.Instance;

        engine.Dispose();
        engine.Dispose();

        instance.IsDisposed.Should().BeTrue();
        Assert.Throws<ObjectDisposedException>(() => _ = engine.Instance);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var engine = new Singleton<SimpleService, CountingFactory>();
        var instance = engine.Instance;

        await engine.DisposeAsync().ConfigureAwait(false);
        await engine.DisposeAsync().ConfigureAwait(false);

        instance.IsDisposed.Should().BeTrue();
        Assert.Throws<ObjectDisposedException>(() => _ = engine.Instance);
    }

    [Fact]
    public async Task DisposeAsync_DisposesAsyncDisposableInstance()
    {
        var engine = new Singleton<AsyncService, AsyncFactory>();
        var instance = engine.Instance;

        await engine.DisposeAsync().ConfigureAwait(false);

        instance.DisposeCount.Should().Be(1);
    }

    [Fact]
    public void FaultedFactory_InstanceThrowsWithPreservedStack()
    {
        using var engine = new Singleton<SimpleService, ThrowingFactory>();

        var ex = Assert.Throws<InvalidOperationException>(() => _ = engine.Instance);

        ex.Message.Should().Contain("factory boom");
        ex.StackTrace.Should().Contain(nameof(ThrowingFactory.Create));
    }

    [Fact]
    public void FaultedFactory_TryAcquireLeaseReturnsFaultedStatus()
    {
        using var engine = new Singleton<SimpleService, ThrowingFactory>();

        var ok = engine.TryAcquireLease(out var result);

        ok.Should().BeFalse();
        result.Status.Should().Be(LeaseAcquisitionStatus.Faulted);
        engine.GetMetadata().Status.Should().Be(LifecycleStatus.Faulted);
    }

    [Fact]
    public void FaultedFactory_DrainReturnsFaultedStatus()
    {
        using var engine = new Singleton<SimpleService, ThrowingFactory>();

        Assert.Throws<InvalidOperationException>(() => _ = engine.Instance);

        var result = engine.DrainAsync().AsTask().GetAwaiter().GetResult();

        result.Status.Should().Be(DrainStatus.Faulted);
    }

    [Fact]
    public void Diagnostics_ReceiveLifecycleCallbacks()
    {
        var diagnostics = new RecordingDiagnostics();
        var engine = SingletonBuilder.For<SimpleService>()
            .WithFactory<CountingFactory>()
            .WithDiagnostics(diagnostics)
            .Prepare()
            .Build();

        _ = engine.Instance;
        var lease = engine.AcquireLease();
        lease.Dispose();
        engine.Dispose();

        diagnostics.Events.Should().Contain("created");
        diagnostics.Events.Should().Contain("acquired");
        diagnostics.Events.Should().Contain("released");
        diagnostics.Events.Should().Contain("disposed");
    }

    internal sealed class SimpleService : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    internal readonly struct CountingFactory : ISingletonFactory<SimpleService>
    {
        public static SimpleService Create() => new();
    }

    internal readonly struct ThrowingFactory : ISingletonFactory<SimpleService>
    {
        public static SimpleService Create() => throw new InvalidOperationException("factory boom");
    }

    internal sealed class AsyncService : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    internal readonly struct AsyncFactory : ISingletonFactory<AsyncService>
    {
        public static AsyncService Create() => new();
    }

    internal struct RecordingDiagnostics : ISingletonDiagnostics
    {
        public List<string> Events { get; } = new();

        public RecordingDiagnostics()
        {
        }

        public void OnInstanceCreated(InstanceName name) => Events.Add("created");

        public void OnInstanceFaulted(InstanceName name, Exception error) => Events.Add("faulted");

        public void OnLeaseAcquired(InstanceName name, LeaseCount activeLeases) => Events.Add("acquired");

        public void OnLeaseReleased(InstanceName name, LeaseCount activeLeases) => Events.Add("released");

        public void OnDrainStarted(InstanceName name) => Events.Add("drain-started");

        public void OnDrainCompleted(InstanceName name, DrainResult result) => Events.Add("drain-completed");

        public void OnDisposed(InstanceName name) => Events.Add("disposed");
    }
}
