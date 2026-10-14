using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests.Singleton;

public sealed class ManagedSingletonTests
{
    [Fact]
    public void Instance_InitializesOnceAndIsStable()
    {
        CountingFactory.CreationCount = 0;
        using var engine = new Singleton<SimpleService, CountingFactory>();

        var first = engine.Instance;
        var second = engine.Instance;

        first.Should().NotBeNull();
        second.Should().BeSameAs(first);
        CountingFactory.CreationCount.Should().Be(1);
        engine.GetMetadata().Status.Should().Be(LifecycleStatus.Initialized);
    }

    [Fact]
    public void Instance_IsStableAcrossLeaseAcquisition()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();

        var lease = engine.AcquireLease();

        lease.Instance.Should().BeSameAs(engine.Instance);
        lease.IsValid.Should().BeTrue();
    }

    [Fact]
    public void LeaseCount_TracksAcquireAndRelease()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();

        engine.GetMetadata().ActiveLeases.Value.Should().Be(0);

        var lease = engine.AcquireLease();
        engine.GetMetadata().ActiveLeases.Value.Should().Be(1);

        lease.Dispose();
        engine.GetMetadata().ActiveLeases.Value.Should().Be(0);
    }

    [Fact]
    public void LeaseDispose_IsIdempotent()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();

        var lease = engine.AcquireLease();
        lease.Dispose();
        lease.Dispose();

        engine.GetMetadata().ActiveLeases.Value.Should().Be(0);
    }

    [Fact]
    public void TryAcquireLease_SuccessPath_ReturnsUsableLease()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();

        var ok = engine.TryAcquireLease(out var result);

        ok.Should().BeTrue();
        result.Status.Should().Be(LeaseAcquisitionStatus.Success);
        result.HasLease.Should().BeTrue();
        result.Lease.Instance.Should().BeSameAs(engine.Instance);
        result.Lease.Dispose();
    }

    [Fact]
    public void TryAcquireLease_AfterComplete_ReturnsCompleteStatus()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();
        _ = engine.Instance;

        engine.Complete();

        var ok = engine.TryAcquireLease(out var result);
        ok.Should().BeFalse();
        result.Status.Should().Be(LeaseAcquisitionStatus.Complete);
        result.HasLease.Should().BeFalse();
    }

    [Fact]
    public void TryAcquireLease_Switch_InvokesSelectedCallback()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();

        var success = false;
        var failure = false;
        var ok = engine.TryAcquireLease(out var result);

        result.Switch(
            onSuccess: lease => { success = true; lease.Dispose(); },
            onFailure: status => failure = true);

        ok.Should().BeTrue();
        success.Should().BeTrue();
        failure.Should().BeFalse();
    }

    [Fact]
    public void Execute_InvokesConsumerUnderLease()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();
        var consumer = new RefStructConsumer();

        engine.Execute(consumer, new RefStructContext(42));

        consumer.ObservedInstance.Should().BeSameAs(engine.Instance);
        consumer.ObservedContextValue.Should().Be(42);
        engine.GetMetadata().ActiveLeases.Value.Should().Be(0);
    }

    [Fact]
    public void Execute_ReleasesLeaseWhenConsumerThrows()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();
        var consumer = new ThrowingConsumer();

        Assert.Throws<InvalidOperationException>(() => engine.Execute(consumer, new RefStructContext(1)));

        engine.GetMetadata().ActiveLeases.Value.Should().Be(0);
    }

    [Fact]
    public void Instance_AfterDrain_ThrowsObjectDisposed()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();
        _ = engine.Instance;

        engine.Complete();
        var result = engine.DrainAsync().AsTask().GetAwaiter().GetResult();

        result.Status.Should().Be(DrainStatus.Completed);
        Assert.Throws<ObjectDisposedException>(() => _ = engine.Instance);
    }

    internal sealed class SimpleService : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    internal readonly struct CountingFactory : ISingletonFactory<SimpleService>
    {
        public static int CreationCount;

        public static SimpleService Create()
        {
            Interlocked.Increment(ref CreationCount);
            return new SimpleService();
        }
    }

    internal readonly ref struct RefStructContext
    {
        public readonly int Value;

        public RefStructContext(int value) => Value = value;
    }

    internal sealed class RefStructConsumer : ISingletonConsumer<SimpleService, RefStructContext>
    {
        public SimpleService? ObservedInstance { get; private set; }
        public int ObservedContextValue { get; private set; }

        public void Consume(SimpleService instance, RefStructContext context)
        {
            ObservedInstance = instance;
            ObservedContextValue = context.Value;
        }
    }

    internal sealed class ThrowingConsumer : ISingletonConsumer<SimpleService, RefStructContext>
    {
        public void Consume(SimpleService instance, RefStructContext context) =>
            throw new InvalidOperationException("consumer boom");
    }
}
