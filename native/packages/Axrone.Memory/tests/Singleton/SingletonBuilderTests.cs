using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests.Singleton;

public sealed class SingletonBuilderTests
{
    [Fact]
    public void For_WithFactory_Prepare_Build_ProducesWorkingEngine()
    {
        using var engine = SingletonBuilder.For<SimpleService>()
            .WithFactory<CountingFactory>()
            .Prepare()
            .Build();

        var instance = engine.Instance;

        instance.Should().NotBeNull();
        engine.GetMetadata().Status.Should().Be(LifecycleStatus.Initialized);
    }

    [Fact]
    public void For_DefaultFactory_ProducesWorkingEngine()
    {
        using var engine = SingletonBuilder.For<SimpleService>()
            .Prepare()
            .Build();

        engine.Instance.Should().NotBeNull();
    }

    [Fact]
    public void WithBackoff_WithDiagnostics_Prepare_Build_FlowsPoliciesThrough()
    {
        var diagnostics = new RecordingDiagnostics();
        using var engine = SingletonBuilder.For<SimpleService>()
            .WithFactory<CountingFactory>()
            .WithBackoff(new ExponentialSpinBackoff())
            .WithDiagnostics(diagnostics)
            .Prepare()
            .Build();

        _ = engine.Instance;

        engine.GetMetadata().Status.Should().Be(LifecycleStatus.Initialized);
        diagnostics.Events.Should().Contain("created");
    }

    [Fact]
    public void WithOptions_AppliesInstanceNameAndDrainTimeout()
    {
        var options = new SingletonOptions
        {
            InstanceName = new InstanceName("registration-a"),
            DrainTimeout = TimeSpan.FromSeconds(5),
            MetricCapacity = 32
        };

        using var engine = SingletonBuilder.For<SimpleService>()
            .WithFactory<CountingFactory>()
            .WithOptions(options)
            .Prepare()
            .Build();

        var metadata = engine.GetMetadata();

        metadata.Name.Value.Should().Be("registration-a");
        engine.Options.DrainTimeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ArityTwoSingleton_ProducesWorkingEngine()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();

        var instance = engine.Instance;

        instance.Should().NotBeNull();
        engine.GetMetadata().Status.Should().Be(LifecycleStatus.Initialized);
    }

    [Fact]
    public void ArityTwoSingleton_WithOptions_AppliesConfiguration()
    {
        var options = new SingletonOptions { InstanceName = new InstanceName("arity-two") };
        using var engine = new Singleton<SimpleService, CountingFactory>(options);

        engine.GetMetadata().Name.Value.Should().Be("arity-two");
    }

    [Fact]
    public void StaticSingleton_ValueIsStable()
    {
        var first = StaticSingleton<SimpleService, CountingFactory>.Instance;
        var second = StaticSingleton<SimpleService, CountingFactory>.Instance;

        first.Should().NotBeNull();
        second.Should().BeSameAs(first);
        StaticSingleton<SimpleService, CountingFactory>.Engine.GetMetadata().Status
            .Should().Be(LifecycleStatus.Initialized);
    }

    [Fact]
    public void StaticSingleton_AcquireLease_ReturnsUsableLease()
    {
        var lease = StaticSingleton<SimpleService, CountingFactory>.AcquireLease();

        lease.Instance.Should().BeSameAs(StaticSingleton<SimpleService, CountingFactory>.Instance);
        lease.Dispose();

        StaticSingleton<SimpleService, CountingFactory>.Engine.GetMetadata().ActiveLeases.Value.Should().Be(0);
    }

    [Fact]
    public void StaticSingleton_TryAcquireLease_SuccessPath()
    {
        var ok = StaticSingleton<SimpleService, CountingFactory>.TryAcquireLease(out var result);

        ok.Should().BeTrue();
        result.Lease.Dispose();
    }

    [Fact]
    public void Engine_ImplementsInspectorContracts()
    {
        using var engine = new Singleton<SimpleService, CountingFactory>();
        _ = engine.Instance;

        var inspector = (ISingletonInspector)engine;
        var metadata = inspector.GetMetadata();
        var captured = inspector.TryGetMetadata(out var same);

        captured.Should().BeTrue();
        same.Name.Should().Be(metadata.Name);
        engine.GetMetadata().Status.Should().Be(LifecycleStatus.Initialized);
    }

    internal sealed class SimpleService
    {
    }

    internal readonly struct CountingFactory : ISingletonFactory<SimpleService>
    {
        public static SimpleService Create() => new();
    }

    internal struct RecordingDiagnostics : ISingletonDiagnostics
    {
        public List<string> Events { get; } = new();

        public RecordingDiagnostics()
        {
        }

        public void OnInstanceCreated(InstanceName name) => Events.Add("created");

        public void OnInstanceFaulted(InstanceName name, Exception error) => Events.Add("faulted");

        public void OnLeaseAcquired(InstanceName name, LeaseCount activeLeases) => Events.Add("acquired");

        public void OnLeaseReleased(InstanceName name, LeaseCount activeLeases) => Events.Add("released");

        public void OnDrainStarted(InstanceName name) => Events.Add("drain-started");

        public void OnDrainCompleted(InstanceName name, DrainResult result) => Events.Add("drain-completed");

        public void OnDisposed(InstanceName name) => Events.Add("disposed");
    }
}
