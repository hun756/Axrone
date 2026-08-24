using Axrone.ObjectPool;

namespace Axrone.ObjectPool.Tests;

public class ThrowHelperTests
{
    [Fact]
    public void ThrowArgumentNullException_ShouldThrow()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ThrowHelper.ThrowArgumentNullException("param"));
        ex.ParamName.Should().Be("param");
    }

    [Fact]
    public void ThrowArgumentNullExceptionGeneric_ShouldThrow()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ThrowHelper.ThrowArgumentNullException<string>("param"));
        ex.ParamName.Should().Be("param");
    }

    [Fact]
    public void ThrowObjectDisposedException_ShouldThrow()
    {
        var ex = Assert.Throws<ObjectDisposedException>(() => ThrowHelper.ThrowObjectDisposedException("obj"));
        ex.ObjectName.Should().Be("obj");
    }

    [Fact]
    public void ThrowInvalidOperationException_ShouldThrow()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ThrowHelper.ThrowInvalidOperationException("msg"));
        ex.Message.Should().Be("msg");
    }

    [Fact]
    public void ThrowArgumentOutOfRangeException_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThrowHelper.ThrowArgumentOutOfRangeException("param"));
    }

    [Fact]
    public void ThrowNotSupportedException_ShouldThrow()
    {
        Assert.Throws<NotSupportedException>(() => ThrowHelper.ThrowNotSupportedException("msg"));
    }
}

public class PoolMetricsTests
{
    [Fact]
    public void PoolMetrics_ShouldBeReadonlyStruct()
    {
        typeof(PoolMetrics).IsValueType.Should().BeTrue();
    }

    [Fact]
    public void PoolMetrics_ShouldHaveAllProperties()
    {
        var metrics = new PoolMetrics
        {
            TotalCreated = 10,
            TotalRented = 20,
            TotalReturned = 15,
            TotalDestroyed = 5,
            CurrentCount = 10,
            AvailableCount = 5,
            RentedCount = 5,
            MaximumCapacity = 100,
            AverageRentDurationMs = 1.5,
            AverageCreateDurationMs = 0.5,
            AverageResetDurationMs = 0.1,
            P95RentDurationMs = 3.0,
            P99RentDurationMs = 5.0,
            HitRatio = 0.8,
            Utilization = 50.0,
            TurnoverRate = 0.75,
            GrowthRate = 0.5,
            MemoryUsageBytes = 1024,
            MemoryPressureLevel = 0.1,
            AverageThroughputPerSecond = 100,
            CurrentThroughputPerSecond = 120,
            PeakUtilization = 90,
            PeakTime = DateTime.UtcNow,
            LastResetTime = DateTime.UtcNow,
            LastScavengeTime = DateTime.UtcNow,
        };

        metrics.TotalCreated.Should().Be(10);
        metrics.TotalRented.Should().Be(20);
        metrics.TotalReturned.Should().Be(15);
        metrics.TotalDestroyed.Should().Be(5);
        metrics.CurrentCount.Should().Be(10);
        metrics.AvailableCount.Should().Be(5);
        metrics.RentedCount.Should().Be(5);
        metrics.MaximumCapacity.Should().Be(100);
        metrics.AverageRentDurationMs.Should().Be(1.5);
        metrics.P95RentDurationMs.Should().Be(3.0);
        metrics.P99RentDurationMs.Should().Be(5.0);
        metrics.HitRatio.Should().Be(0.8);
        metrics.Utilization.Should().Be(50.0);
        metrics.TurnoverRate.Should().Be(0.75);
        metrics.GrowthRate.Should().Be(0.5);
        metrics.MemoryUsageBytes.Should().Be(1024);
        metrics.PeakUtilization.Should().Be(90);
    }

    [Fact]
    public void PoolMetrics_Default_ShouldBeZero()
    {
        var metrics = new PoolMetrics();
        metrics.TotalCreated.Should().Be(0);
        metrics.TotalRented.Should().Be(0);
        metrics.CurrentCount.Should().Be(0);
        metrics.AvailableCount.Should().Be(0);
        metrics.RentedCount.Should().Be(0);
        metrics.HitRatio.Should().Be(0);
        metrics.Utilization.Should().Be(0);
    }
}

public class PoolDiagnosticsTests
{
    [Fact]
    public void PoolDiagnostics_ShouldBeReadonlyStruct()
    {
        typeof(PoolDiagnostics).IsValueType.Should().BeTrue();
    }

    [Fact]
    public void PoolDiagnostics_ShouldHaveAllProperties()
    {
        var now = DateTime.UtcNow;
        var diag = new PoolDiagnostics
        {
            Uptime = TimeSpan.FromMinutes(5),
            ConfigurationChanges = 2,
            ScavengeCount = 10,
            ExpansionCount = 3,
            ShrinkCount = 1,
            FailedValidationCount = 0,
            ExhaustionCount = 0,
            InitialCapacity = 16,
            MaximumCapacity = 128,
            AllocFailureCount = 0,
            DisposeFailureCount = 0,
            RecycleFailureCount = 0,
            Events = new List<KeyValuePair<DateTime, string>>(),
            CustomMetrics = new Dictionary<string, double>(),
            LastException = null,
        };

        diag.Uptime.Should().Be(TimeSpan.FromMinutes(5));
        diag.ConfigurationChanges.Should().Be(2);
        diag.ScavengeCount.Should().Be(10);
        diag.ExpansionCount.Should().Be(3);
        diag.ShrinkCount.Should().Be(1);
        diag.InitialCapacity.Should().Be(16);
        diag.MaximumCapacity.Should().Be(128);
        diag.LastException.Should().BeNull();
    }

    [Fact]
    public void PoolDiagnostics_Default_ShouldBeZero()
    {
        var diag = new PoolDiagnostics();
        diag.ScavengeCount.Should().Be(0);
        diag.ExpansionCount.Should().Be(0);
        diag.ShrinkCount.Should().Be(0);
        diag.FailedValidationCount.Should().Be(0);
        diag.ExhaustionCount.Should().Be(0);
    }
}
