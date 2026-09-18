using Axrone.Memory.Arena;

namespace Axrone.Memory.Tests.Arena;

public class ArenaRingBuilderTests
{
    [Fact]
    public void Build_With_Defaults_Creates_Ring()
    {
        using var ring = ArenaRingBuilder<int>.Create(32).Build();
        ring.Capacity.Should().Be(32u);
        ring.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void Build_With_Explicit_Backoff_Creates_Ring()
    {
        using var ring = ArenaRingBuilder<int>.Create(16)
            .WithTopology(MemoryTopology.NativeAligned)
            .WithZeroOnRecycle()
            .WithTelemetry("TestMeter", "test-instance")
            .Build<AggressiveSpinBackoff>();

        ring.Capacity.Should().Be(16u);
    }

    [Fact]
    public void Build_With_POH_Topology()
    {
        using var ring = ArenaRingBuilder<int>.Create(8)
            .WithPinnedObjectHeapStorage()
            .Build();

        ring.Producer.TryWrite(42).Should().BeTrue();
        ring.Consumer.TryRead(out int result).Should().BeTrue();
        result.Should().Be(42);
    }

    [Fact]
    public void WithCapacity_Overrides_Initial()
    {
        using var ring = ArenaRingBuilder<int>.Create(8)
            .WithCapacity(64)
            .Build();

        ring.Capacity.Should().Be(64u);
    }
}
