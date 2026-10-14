using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests.Singleton;

public sealed class SingletonMetricsTests
{
    [Fact]
    public void GetMetadata_ReflectsNameStatusAndLeaseCount()
    {
        var options = new SingletonOptions { InstanceName = new InstanceName("metrics-registration") };
        using var engine = new Singleton<SimpleService, CountingFactory>(options);
        _ = engine.Instance;

        var lease = engine.AcquireLease();
        var metadata = engine.GetMetadata();

        metadata.Name.Value.Should().Be("metrics-registration");
        metadata.Status.Should().Be(LifecycleStatus.Initialized);
        metadata.ActiveLeases.Value.Should().Be(1);
        metadata.Iteration.Value.Should().BeGreaterThan(0);
        metadata.CreatedAt.Ticks.Should().BeGreaterThan(0);

        lease.Dispose();
    }

    [Fact]
    public void EnumerateMetrics_CopiesRecordsIntoStackallocSpan()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();
        _ = engine.Instance;

        Span<MetricRecord> buffer = stackalloc MetricRecord[64];
        var count = engine.EnumerateMetrics(buffer);

        count.Should().BeGreaterThan(0);

        var foundCreated = false;
        for (var i = 0; i < count; i++)
        {
            if (buffer[i].Kind == MetricKind.InstanceCreated)
            {
                foundCreated = true;
                break;
            }
        }

        foundCreated.Should().BeTrue();
    }

    [Fact]
    public void EnumerateMetrics_RecordsAreChronological()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();
        _ = engine.Instance;
        var lease = engine.AcquireLease();
        lease.Dispose();

        Span<MetricRecord> buffer = stackalloc MetricRecord[64];
        var count = engine.EnumerateMetrics(buffer);

        var previous = -1L;
        for (var i = 0; i < count; i++)
        {
            buffer[i].Iteration.Value.Should().BeGreaterThan(previous);
            previous = buffer[i].Iteration.Value;
        }
    }

    [Fact]
    public void EnumerateMetrics_BufferTooSmall_Throws()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();
        _ = engine.Instance;

        var buffer = Array.Empty<MetricRecord>();

        var act = () => engine.EnumerateMetrics(buffer);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MetricSequence_RingBuffer_OverwritesOldestWhenFull()
    {
        var sequence = new MetricSequence(capacity: 2);

        sequence.Append(new MetricRecord(MetricKind.LeaseAcquired, EpochTimestamp.Now, new Iteration(1), new LeaseCount(1)));
        sequence.Append(new MetricRecord(MetricKind.LeaseReleased, EpochTimestamp.Now, new Iteration(2), new LeaseCount(0)));
        sequence.Append(new MetricRecord(MetricKind.LeaseAcquired, EpochTimestamp.Now, new Iteration(3), new LeaseCount(1)));

        sequence.Count.Should().Be(2);
        sequence[0].Iteration.Value.Should().Be(2);
        sequence[1].Iteration.Value.Should().Be(3);
    }

    [Fact]
    public void MetricSequence_DuckTypedForeach_IteratesChronologically()
    {
        var sequence = new MetricSequence(capacity: 4);
        sequence.Append(new MetricRecord(MetricKind.InstanceCreated, EpochTimestamp.Now, new Iteration(1), new LeaseCount(0)));
        sequence.Append(new MetricRecord(MetricKind.LeaseAcquired, EpochTimestamp.Now, new Iteration(2), new LeaseCount(1)));

        var iterations = new List<long>();
        foreach (var record in sequence)
        {
            iterations.Add(record.Iteration.Value);
        }

        iterations.Should().Equal(1L, 2L);
    }

    [Fact]
    public void LeaseAcquisitionResult_Switch_SelectsCallback()
    {
        var successResult = new LeaseAcquisitionResult<int>(LeaseAcquisitionStatus.Success, 7);
        var failureResult = new LeaseAcquisitionResult<int>(LeaseAcquisitionStatus.Faulted, default);

        var successValue = successResult.Switch(onSuccess: lease => lease * 2, onFailure: status => -1);
        var failureValue = failureResult.Switch(onSuccess: lease => lease * 2, onFailure: status => -1);

        successValue.Should().Be(14);
        failureValue.Should().Be(-1);
    }

    [Fact]
    public void PaddedState_HasCacheLineSize()
    {
        Marshal.SizeOf<PaddedState>().Should().Be(64);
    }

    internal sealed class SimpleService
    {
    }

    internal readonly struct CountingFactory : ISingletonFactory<SimpleService>
    {
        public static SimpleService Create() => new();
    }
}
