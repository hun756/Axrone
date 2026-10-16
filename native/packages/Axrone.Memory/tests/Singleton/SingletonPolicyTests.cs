using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests;

public sealed class SingletonPolicyTests
{
    private sealed class Widget
    {
        public int Value;
    }

    private readonly struct WidgetFactory : ISingletonFactory<Widget>
    {
        public static Widget Create() => new() { Value = 7 };
    }

    [Fact]
    public void BackoffPolicies_Grow_AndCap()
    {
        var adaptive = new AdaptiveBackoff();
        adaptive.MaxAttempts.Should().Be(64);
        adaptive.NextDelay(0).Should().Be(TimeSpan.FromMilliseconds(1));
        adaptive.NextDelay(1000).Should().Be(TimeSpan.FromMilliseconds(25));

        Action negative = () => adaptive.NextDelay(-1);
        negative.Should().Throw<ArgumentOutOfRangeException>();

        var spin = new ExponentialSpinBackoff();
        spin.MaxAttempts.Should().Be(64);
        spin.NextDelay(0).Should().Be(TimeSpan.Zero);
        spin.NextDelay(7).Should().Be(TimeSpan.Zero);
        spin.NextDelay(8).Should().Be(TimeSpan.FromMilliseconds(1));
        spin.NextDelay(1000).Should().Be(TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void BackoffPolicies_Reject_BadBounds()
    {
        Action badAttempts = () => _ = new AdaptiveBackoff(0);
        Action badBase = () => _ = new AdaptiveBackoff(64, -1.0);
        Action badMax = () => _ = new AdaptiveBackoff(64, 10.0, 5.0);
        Action badSpin = () => _ = new ExponentialSpinBackoff(64, -1);
        badAttempts.Should().Throw<ArgumentOutOfRangeException>();
        badBase.Should().Throw<ArgumentOutOfRangeException>();
        badMax.Should().Throw<ArgumentOutOfRangeException>();
        badSpin.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NullDiagnostics_Discards_AllCallbacks()
    {
        var sink = new NullDiagnostics();
        var name = new InstanceName("test");
        sink.OnInstanceCreated(name);
        sink.OnInstanceFaulted(name, new InvalidOperationException());
        sink.OnLeaseAcquired(name, new LeaseCount(1));
        sink.OnLeaseReleased(name, new LeaseCount(0));
        sink.OnDrainStarted(name);
        sink.OnDrainCompleted(name, new DrainResult(DrainStatus.Completed, new LeaseCount(0), TimeSpan.Zero));
        sink.OnDisposed(name);
    }

    [Fact]
    public void Lease_DoubleDispose_IsNoOp_AndDefaultIsInvalid()
    {
        using var engine = SingletonBuilder.For<Widget>().Prepare().Build();
        SingletonLease<Widget, DefaultActivatorFactory<Widget>, AdaptiveBackoff, NullDiagnostics> lease = engine.AcquireLease();
        lease.IsValid.Should().BeTrue();
        lease.Instance.Value.Should().Be(0);
        lease.Dispose();
        lease.Dispose();

        default(SingletonLease<Widget, DefaultActivatorFactory<Widget>, AdaptiveBackoff, NullDiagnostics>)
            .IsValid.Should().BeFalse();
        Action read = () => _ = default(SingletonLease<Widget, DefaultActivatorFactory<Widget>, AdaptiveBackoff, NullDiagnostics>).Instance;
        read.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Engine_Execute_RunsUnderLease()
    {
        using var engine = SingletonBuilder.For<Widget>().WithFactory<WidgetFactory>().Prepare().Build();
        var consumer = new CheckConsumer();
        engine.Execute(consumer, 0);
        consumer.Seen.Should().Be(7);
    }

    private sealed class CheckConsumer : ISingletonConsumer<Widget, int>
    {
        public int Seen = -1;

        public void Consume(Widget instance, int context) => Seen = instance.Value + context;
    }
}
