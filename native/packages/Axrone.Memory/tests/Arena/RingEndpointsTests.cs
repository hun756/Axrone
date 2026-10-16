using Axrone.Memory.Arena;
using Axrone.Utility.Backoff.SpinPolicies;

namespace Axrone.Memory.Tests;

public sealed class RingEndpointsTests : IDisposable
{
    private readonly ArenaMemoryRing<int, AdaptiveSpinBackoff> _ring =
        new(new ArenaCapacity(16));

    public void Dispose()
    {
        _ring.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void WriteRead_RoundTrips_InOrder()
    {
        _ring.Producer.TryWrite(1).Should().BeTrue();
        _ring.Producer.TryWrite(2).Should().BeTrue();
        _ring.Consumer.TryRead(out int first).Should().BeTrue();
        _ring.Consumer.TryRead(out int second).Should().BeTrue();
        first.Should().Be(1);
        second.Should().Be(2);
    }

    [Fact]
    public void Read_Empty_ReturnsFalse()
    {
        _ring.Consumer.TryRead(out int item).Should().BeFalse();
        item.Should().Be(0);
    }

    [Fact]
    public void Write_Full_ReturnsFalse()
    {
        for (int i = 0; i < 16; i++)
        {
            _ring.Producer.TryWrite(i).Should().BeTrue();
        }

        _ring.Producer.TryWrite(99).Should().BeFalse();
    }

    [Fact]
    public void WriteMany_BatchesAtomically()
    {
        _ring.Producer.TryWriteMany(10, 20, 30).Should().BeTrue();
        _ring.Consumer.TryRead(out int a).Should().BeTrue();
        _ring.Consumer.TryRead(out int b).Should().BeTrue();
        _ring.Consumer.TryRead(out int c).Should().BeTrue();
        (a, b, c).Should().Be((10, 20, 30));
    }

    [Fact]
    public void Admin_Reports_Health_AndSnapshot()
    {
        _ring.Admin.IsHealthy.Should().BeTrue();
        _ring.Producer.TryWrite(5);
        MetricsSnapshot snapshot = _ring.Admin.GetSnapshot();
        snapshot.Capacity.Should().Be(16);
        snapshot.UtilizationPercentage.Should().BeGreaterThan(0);

        HealthReport health = _ring.Admin.ExamineHealth();
        health.IsHealthy.Should().BeTrue();
        health.StatusMessage.Should().Be("Active");
    }

    [Fact]
    public void Complete_Blocks_Further_Access()
    {
        _ring.Producer.TryWrite(1);
        _ring.Admin.Complete();
        _ring.Admin.ExamineHealth().StatusMessage.Should().Be("Completing (draining)");
        Action read = () => _ring.Consumer.TryRead(out _);
        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_WithError_Stays_Faulted_AfterDrain()
    {
        _ring.Admin.Complete(new InvalidOperationException("boom"));
        HealthReport health = _ring.Admin.ExamineHealth();
        health.IsHealthy.Should().BeFalse();
        health.StatusMessage.Should().Contain("boom");
        _ring.Admin.IsHealthy.Should().BeFalse();
        _ring.Admin.GetSnapshot().IsFaulted.Should().BeTrue();
    }
}
