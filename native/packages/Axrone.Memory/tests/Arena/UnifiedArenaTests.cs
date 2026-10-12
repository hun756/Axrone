namespace Axrone.Memory.Tests.Arena;

using Axrone.Memory.Arena;

public class UnifiedArenaValuesTests
{
    [Fact]
    public void ChunkId_OrdersAndIncrements()
    {
        ChunkId.None.Value.Should().Be((uint)0);
        ChunkId id = new(41);
        (++id).Value.Should().Be((uint)42);
        (new ChunkId(1) < new ChunkId(2)).Should().BeTrue();
        new ChunkId(3).ToString().Should().Be("ChunkId(3)");
    }

    [Fact]
    public void Marker_ZeroDetection()
    {
        ArenaMarker.Zero.IsZero.Should().BeTrue();
        new ArenaMarker(new ChunkId(2), ByteSize.FromBytes(64)).IsZero.Should().BeFalse();
    }

    [Fact]
    public void AllocationResult_SuccessFlag()
    {
        unsafe
        {
            int value = 5;
            var handle = new ArenaAllocationHandle(&value, ByteSize.FromBytes((nuint)sizeof(int)));

            handle.AsSpan<int>().ToArray().Should().Equal(5);
            handle.AsRef<int>().Should().Be(5);
            AllocationResult.Succeeded(handle).IsSuccess.Should().BeTrue();
            AllocationResult.Failed(AllocationStatus.OutOfMemory).Status.Should().Be(AllocationStatus.OutOfMemory);
        }
    }

    [Fact]
    public void Handle_AsRef_TooSmall_Throws()
    {
        unsafe
        {
            byte value = 1;
            var handle = new ArenaAllocationHandle(&value, ByteSize.FromBytes(1));

            Action act = () => handle.AsRef<long>();

            act.Should().Throw<InvalidOperationException>();
        }
    }
}

public class UnifiedArenaPolicyTests
{
    private static readonly ByteSize Max = ByteSize.FromMegabytes(64);

    [Fact]
    public void Geometric_Doubles_ClampsAndFloors()
    {
        GeometricGrowthPolicy.ComputeNextSize(ByteSize.FromKilobytes(4), ByteSize.FromBytes(100), Max)
            .Should().Be(ByteSize.FromKilobytes(8));
        GeometricGrowthPolicy.ComputeNextSize(ByteSize.FromKilobytes(4), ByteSize.FromKilobytes(16), Max)
            .Should().Be(ByteSize.FromKilobytes(16));
        GeometricGrowthPolicy.ComputeNextSize(ByteSize.FromMegabytes(48), ByteSize.FromBytes(100), Max)
            .Should().Be(Max);
    }

    [Fact]
    public void Exponential_GrowsByHalf()
    {
        ExponentialGrowthPolicy.ComputeNextSize(ByteSize.FromKilobytes(4), ByteSize.FromBytes(100), Max)
            .Should().Be(ByteSize.FromKilobytes(6));
    }

    [Fact]
    public void Fixed_KeepsSize_RaisingToFloor()
    {
        FixedGrowthPolicy.ComputeNextSize(ByteSize.FromKilobytes(4), ByteSize.FromBytes(100), Max)
            .Should().Be(ByteSize.FromKilobytes(4));
        FixedGrowthPolicy.ComputeNextSize(ByteSize.FromKilobytes(4), ByteSize.FromKilobytes(8), Max)
            .Should().Be(ByteSize.FromKilobytes(8));
    }

    [Fact]
    public void Backoff_AdvancesCounter()
    {
        AdaptiveSpinBackoff.Initialize(out int adaptive);
        AdaptiveSpinBackoff.Advance(ref adaptive);
        adaptive.Should().Be(1);
        AdaptiveSpinBackoff.Reset(ref adaptive);
        adaptive.Should().Be(0);

        YieldingBackoff.Initialize(out int yielding);
        YieldingBackoff.Advance(ref yielding);
        yielding.Should().Be(1);
    }

    [Fact]
    public void NullSink_AcceptsCalls()
    {
        Action act = () =>
        {
            NullMetricsSink.OnChunkAllocated(ByteSize.FromBytes(8));
            NullMetricsSink.OnChunkFreed(ByteSize.FromBytes(8));
            NullMetricsSink.OnAllocationFailed(ByteSize.FromBytes(8));
            NullMetricsSink.OnReset(ByteSize.FromBytes(8));
        };

        act.Should().NotThrow();
    }
}

public class UnifiedArenaOptionsTests
{
    [Fact]
    public void Defaults_MatchSpec()
    {
        var options = new ArenaOptions();

        options.InitialChunkSize.Should().Be(ByteSize.FromBytes(65536));
        options.MaxChunkSize.Should().Be(ByteSize.FromBytes(67108864));
        options.DefaultAlignment.Should().Be(Alignment.PointerAlignment);
        options.ZeroOnReset.Should().BeFalse();
    }

    [Fact]
    public void SmallSizes_LiftToPageFloor()
    {
        var options = new ArenaOptions { InitialChunkSize = ByteSize.FromBytes(100), MaxChunkSize = ByteSize.FromBytes(200) };

        options.InitialChunkSize.Should().Be(ByteSize.FromBytes(4096));
        options.MaxChunkSize.Should().Be(ByteSize.FromBytes(4096));
    }

    [Fact]
    public void ZeroAlignment_FallsBackToPointer()
    {
        var options = new ArenaOptions { DefaultAlignment = default };

        options.DefaultAlignment.Should().Be(Alignment.PointerAlignment);
    }

    [Fact]
    public void Builder_ChainsConfiguration()
    {
        ArenaBuilder<ConfiguredArenaState> builder = ArenaBuilder<UnconfiguredArenaState>.Create()
            .WithInitialChunkSize(ByteSize.FromKilobytes(8))
            .WithMaxChunkSize(ByteSize.FromMegabytes(1))
            .WithZeroOnReset(true);

        builder.Should().BeOfType<ArenaBuilder<ConfiguredArenaState>>();
    }
}
