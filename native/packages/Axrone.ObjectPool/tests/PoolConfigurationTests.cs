using Axrone.ObjectPool;

namespace Axrone.ObjectPool.Tests;

public class PoolConfigurationTests
{
    [Fact]
    public void Default_ShouldHaveCorrectInitialCapacity()
    {
        var config = PoolConfiguration.Default;
        config.InitialCapacity.Should().Be(Environment.ProcessorCount * 2);
    }

    [Fact]
    public void Default_ShouldHaveCorrectMaximumCapacity()
    {
        var config = PoolConfiguration.Default;
        config.MaximumCapacity.Should().Be(Environment.ProcessorCount * 32);
    }

    [Fact]
    public void Default_ShouldHaveFiveMinuteIdleTimeout()
    {
        var config = PoolConfiguration.Default;
        config.IdleTimeout.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Default_ShouldUseThreadLocalStrategy()
    {
        var config = PoolConfiguration.Default;
        config.Strategy.Should().Be(PoolingStrategy.ThreadLocal);
    }

    [Fact]
    public void Default_ShouldNotTrackLeaks()
    {
        var config = PoolConfiguration.Default;
        config.TrackLeaks.Should().BeFalse();
    }

    [Fact]
    public void Default_ShouldClearOnReturn()
    {
        var config = PoolConfiguration.Default;
        config.ClearOnReturn.Should().BeTrue();
    }

    [Fact]
    public void Default_ShouldNotThrowOnExhaustion()
    {
        var config = PoolConfiguration.Default;
        config.ThrowOnExhaustion.Should().BeFalse();
    }

    [Fact]
    public void Default_ShouldHaveThirtySecondScavengeInterval()
    {
        var config = PoolConfiguration.Default;
        config.ScavengeIntervalMs.Should().Be(30000);
    }

    [Fact]
    public void Default_ShouldUseGrowWhenNeededPolicy()
    {
        var config = PoolConfiguration.Default;
        config.AllocationPolicy.Should().Be(AllocationPolicy.GrowWhenNeeded);
    }

    [Fact]
    public void Default_ShouldUseSlimSynchronization()
    {
        var config = PoolConfiguration.Default;
        config.UseSlimSynchronization.Should().BeTrue();
    }

    [Fact]
    public void Default_ShouldAllowPoolExpansion()
    {
        var config = PoolConfiguration.Default;
        config.AllowPoolExpansion.Should().BeTrue();
    }

    [Fact]
    public void Default_ShouldTargetEightyPercentUtilization()
    {
        var config = PoolConfiguration.Default;
        config.TargetUtilizationPercentage.Should().Be(80);
    }

    [Fact]
    public void Default_ShouldUseBasicDiagnostics()
    {
        var config = PoolConfiguration.Default;
        config.DiagnosticsLevel.Should().Be(DiagnosticsLevel.Basic);
    }

    [Fact]
    public void Default_ShouldUseDefaultConcurrency()
    {
        var config = PoolConfiguration.Default;
        config.ConcurrencyLevel.Should().Be(ConcurrencyLevel.Default);
    }

    [Fact]
    public void Default_ShouldUseMediumMetricsSampling()
    {
        var config = PoolConfiguration.Default;
        config.MetricsSamplingRate.Should().Be(MetricsSamplingRate.Medium);
    }

    [Fact]
    public void Default_ShouldUseLruEviction()
    {
        var config = PoolConfiguration.Default;
        config.EvictionStrategy.Should().Be(EvictionStrategy.LeastRecentlyUsed);
    }

    [Fact]
    public void Default_ShouldUseBalancedMemoryOptimization()
    {
        var config = PoolConfiguration.Default;
        config.MemoryOptimization.Should().Be(MemoryOptimizationLevel.Balanced);
    }

    [Fact]
    public void HighPerformance_ShouldPreallocateAll()
    {
        var config = PoolConfiguration.HighPerformance;
        config.AllocationPolicy.Should().Be(AllocationPolicy.PreallocateAll);
    }

    [Fact]
    public void HighPerformance_ShouldNotClearOnReturn()
    {
        var config = PoolConfiguration.HighPerformance;
        config.ClearOnReturn.Should().BeFalse();
    }

    [Fact]
    public void HighPerformance_ShouldUseMaximumConcurrency()
    {
        var config = PoolConfiguration.HighPerformance;
        config.ConcurrencyLevel.Should().Be(ConcurrencyLevel.Maximum);
    }

    [Fact]
    public void HighPerformance_ShouldUseMinimalDiagnostics()
    {
        var config = PoolConfiguration.HighPerformance;
        config.DiagnosticsLevel.Should().Be(DiagnosticsLevel.Minimal);
    }

    [Fact]
    public void HighPerformance_ShouldHaveNoEviction()
    {
        var config = PoolConfiguration.HighPerformance;
        config.EvictionStrategy.Should().Be(EvictionStrategy.None);
    }

    [Fact]
    public void HighPerformance_ShouldOptimizeForSpeed()
    {
        var config = PoolConfiguration.HighPerformance;
        config.MemoryOptimization.Should().Be(MemoryOptimizationLevel.Speed);
    }

    [Fact]
    public void MemoryConserving_ShouldTrackLeaks()
    {
        var config = PoolConfiguration.MemoryConserving;
        config.TrackLeaks.Should().BeTrue();
    }

    [Fact]
    public void MemoryConserving_ShouldThrowOnExhaustion()
    {
        var config = PoolConfiguration.MemoryConserving;
        config.ThrowOnExhaustion.Should().BeTrue();
    }

    [Fact]
    public void MemoryConserving_ShouldUseCentralizedQueue()
    {
        var config = PoolConfiguration.MemoryConserving;
        config.Strategy.Should().Be(PoolingStrategy.CentralizedQueue);
    }

    [Fact]
    public void MemoryConserving_ShouldUseLazyAllocation()
    {
        var config = PoolConfiguration.MemoryConserving;
        config.AllocationPolicy.Should().Be(AllocationPolicy.LazyAllocation);
    }

    [Fact]
    public void MemoryConserving_ShouldNotAllowExpansion()
    {
        var config = PoolConfiguration.MemoryConserving;
        config.AllowPoolExpansion.Should().BeFalse();
    }

    [Fact]
    public void MemoryConserving_ShouldOptimizeForMemory()
    {
        var config = PoolConfiguration.MemoryConserving;
        config.MemoryOptimization.Should().Be(MemoryOptimizationLevel.Memory);
    }

    [Fact]
    public void MemoryConserving_ShouldUseFullDiagnostics()
    {
        var config = PoolConfiguration.MemoryConserving;
        config.DiagnosticsLevel.Should().Be(DiagnosticsLevel.Full);
    }

    [Fact]
    public void NativeAotOptimized_ShouldUseThreadLocalWithPartitioning()
    {
        var config = PoolConfiguration.NativeAotOptimized;
        config.Strategy.Should().Be(PoolingStrategy.ThreadLocalWithPartitioning);
    }

    [Fact]
    public void NativeAotOptimized_ShouldUseTimeBasedEviction()
    {
        var config = PoolConfiguration.NativeAotOptimized;
        config.EvictionStrategy.Should().Be(EvictionStrategy.TimeBased);
    }

    [Fact]
    public void NativeAotOptimized_ShouldOptimizeForNativeAot()
    {
        var config = PoolConfiguration.NativeAotOptimized;
        config.MemoryOptimization.Should().Be(MemoryOptimizationLevel.NativeAot);
    }

    [Fact]
    public void NativeAotOptimized_ShouldHaveCorrectMaxCapacity()
    {
        var config = PoolConfiguration.NativeAotOptimized;
        config.MaximumCapacity.Should().Be(Environment.ProcessorCount * 24);
    }
}
