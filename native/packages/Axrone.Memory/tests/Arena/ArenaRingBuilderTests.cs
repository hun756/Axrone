using Axrone.Memory.Arena;
using Axrone.Utility.Builders;

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
    public void TryBuild_NonPowerOfTwo_ReportsDiagnostic()
    {
        var builder = ArenaRingBuilder<int>.Create(3);

        builder.TryBuild(out var ring, out BuilderDiagnostic diagnostic).Should().BeFalse();
        ring.Should().BeNull();
        diagnostic.Code.Should().Be(BuilderStatusCode.ValidationFailed);
        diagnostic.Message.Should().Contain("power of two");
    }

    [Fact]
    public void State_ValidatesWithoutBuilder()
    {
        var state = ArenaRingState<int>.Default;
        state.Capacity = 24;

        ArenaRingState<int>.TryValidate(in state, out var diagnostic).Should().BeFalse();
        diagnostic.Code.Should().Be(BuilderStatusCode.ValidationFailed);
    }

    [Fact]
    public void Fork_DivergesIndependently()
    {
        var original = ArenaRingBuilder<int>.Create(32);
        var fork = original.Fork();
        fork.WithCapacity(64);

        using var originalRing = original.Build();
        using var forkRing = fork.Build();

        originalRing.Capacity.Should().Be(32u);
        forkRing.Capacity.Should().Be(64u);
    }

    [Fact]
    public void Reset_RestoresDefaults()
    {
        var builder = ArenaRingBuilder<int>.Create(32)
            .WithPinnedObjectHeapStorage()
            .WithTelemetry("Custom", "x");
        builder.Reset();

        builder.TryBuild(out _, out BuilderDiagnostic diagnostic).Should().BeFalse();
        diagnostic.Code.Should().Be(BuilderStatusCode.ValidationFailed);
    }

    [Fact]
    public void Build_NonPowerOfTwo_PreservesThrowContract()
    {
        var act = () => ArenaRingBuilder<int>.Create(3).Build();

        act.Should().Throw<ArgumentOutOfRangeException>();
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
