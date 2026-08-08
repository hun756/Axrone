using Axrone.Memory.Lifetime;
using Axrone.Memory.Lifetime.Internal;
using SingletonFacade = Axrone.Memory.Lifetime.Singleton;

namespace Axrone.Memory.Tests;

public class SingletonDiagnosticsTests
{
    [Fact]
    public void GetMetrics_ReturnsDefault_WhenNotMonitored()
    {
        var metrics = SingletonDiagnostics.GetMetrics<SimpleService>();
        metrics.Should().Be(default(SingletonMetrics));
    }

    [Fact]
    public void RecordAccess_IncrementsAccessCount()
    {
        _ = SingletonFacade.GetInstance<MonitoredService>();

        SingletonDiagnostics.RecordAccess<MonitoredService>();
        SingletonDiagnostics.RecordAccess<MonitoredService>();
        SingletonDiagnostics.RecordAccess<MonitoredService>();

        var metrics = SingletonDiagnostics.GetMetrics<MonitoredService>();
        metrics.AccessCount.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void RecordCreationStart_And_End_SetsIsCreated()
    {
        SingletonDiagnostics.RecordCreationStart<MonitoredService>();
        SingletonDiagnostics.RecordCreationEnd<MonitoredService>();

        var metrics = SingletonDiagnostics.GetMetrics<MonitoredService>();
        metrics.IsCreated.Should().BeTrue();
    }

    [Fact]
    public void GetMetrics_ReportsCorrectTypeName()
    {
        _ = SingletonFacade.GetInstance<MonitoredService>();
        SingletonDiagnostics.RecordCreationStart<MonitoredService>();
        SingletonDiagnostics.RecordCreationEnd<MonitoredService>();

        var metrics = SingletonDiagnostics.GetMetrics<MonitoredService>();
        metrics.TypeName.Should().Be(nameof(MonitoredService));
    }

    [Fact]
    public void GetAllMetrics_ReturnsAllMonitoredTypes()
    {
        _ = SingletonFacade.GetInstance<MonitoredService>();
        SingletonDiagnostics.RecordCreationStart<MonitoredService>();
        SingletonDiagnostics.RecordCreationEnd<MonitoredService>();

        var all = SingletonDiagnostics.GetAllMetrics();
        all.Should().ContainKey(nameof(MonitoredService));
    }

    [Fact]
    public void RecordAccess_NoOp_WhenTypeNotMonitored()
    {
        SingletonDiagnostics.RecordAccess<SimpleService>();
        var metrics = SingletonDiagnostics.GetMetrics<SimpleService>();
        metrics.AccessCount.Should().Be(0);
    }

    [Fact]
    public void CreationTimeMs_IsNonNegative()
    {
        SingletonDiagnostics.RecordCreationStart<MonitoredService>();
        SingletonDiagnostics.RecordCreationEnd<MonitoredService>();

        var metrics = SingletonDiagnostics.GetMetrics<MonitoredService>();
        metrics.CreationTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }
}
