using Axrone.ObjectPool;

namespace Axrone.ObjectPool.Tests;

public class EnumTests
{
    [Fact]
    public void PoolingStrategy_ShouldHaveAllMembers()
    {
        Enum.GetValues<PoolingStrategy>().Should().HaveCount(9);
    }

    [Fact]
    public void PoolingStrategy_ShouldContainCentralizedQueue()
    {
        Enum.IsDefined(PoolingStrategy.CentralizedQueue).Should().BeTrue();
    }

    [Fact]
    public void PoolingStrategy_ShouldContainThreadLocal()
    {
        Enum.IsDefined(PoolingStrategy.ThreadLocal).Should().BeTrue();
    }

    [Fact]
    public void PoolingStrategy_ShouldContainLockFree()
    {
        Enum.IsDefined(PoolingStrategy.LockFree).Should().BeTrue();
    }

    [Fact]
    public void PoolingStrategy_ShouldContainBoundedChannel()
    {
        Enum.IsDefined(PoolingStrategy.BoundedChannel).Should().BeTrue();
    }

    [Fact]
    public void PoolingStrategy_ShouldContainShardedByThread()
    {
        Enum.IsDefined(PoolingStrategy.ShardedByThread).Should().BeTrue();
    }

    [Fact]
    public void AllocationPolicy_ShouldHaveAllMembers()
    {
        Enum.GetValues<AllocationPolicy>().Should().HaveCount(5);
    }

    [Fact]
    public void AllocationPolicy_ShouldContainLazyAllocation()
    {
        Enum.IsDefined(AllocationPolicy.LazyAllocation).Should().BeTrue();
    }

    [Fact]
    public void AllocationPolicy_ShouldContainPreallocateAll()
    {
        Enum.IsDefined(AllocationPolicy.PreallocateAll).Should().BeTrue();
    }

    [Fact]
    public void ConcurrencyLevel_Minimal_ShouldBeOne()
    {
        ((int)ConcurrencyLevel.Minimal).Should().Be(1);
    }

    [Fact]
    public void ConcurrencyLevel_Default_ShouldBeFour()
    {
        ((int)ConcurrencyLevel.Default).Should().Be(4);
    }

    [Fact]
    public void ConcurrencyLevel_Maximum_ShouldBeSixteen()
    {
        ((int)ConcurrencyLevel.Maximum).Should().Be(16);
    }

    [Fact]
    public void DiagnosticsLevel_ShouldHaveFiveLevels()
    {
        Enum.GetValues<DiagnosticsLevel>().Should().HaveCount(5);
    }

    [Fact]
    public void DiagnosticsLevel_None_ShouldBeFirst()
    {
        ((int)DiagnosticsLevel.None).Should().Be(0);
    }

    [Fact]
    public void EvictionStrategy_ShouldHaveSevenMembers()
    {
        Enum.GetValues<EvictionStrategy>().Should().HaveCount(7);
    }

    [Fact]
    public void EvictionStrategy_None_ShouldBeFirst()
    {
        ((int)EvictionStrategy.None).Should().Be(0);
    }

    [Fact]
    public void MemoryOptimizationLevel_ShouldHaveFiveMembers()
    {
        Enum.GetValues<MemoryOptimizationLevel>().Should().HaveCount(5);
    }

    [Fact]
    public void MetricsSamplingRate_ShouldHaveFiveMembers()
    {
        Enum.GetValues<MetricsSamplingRate>().Should().HaveCount(5);
    }
}
