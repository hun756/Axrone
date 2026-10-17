namespace Axrone.GameLoop.Tests;

internal struct RegistryProbeState
{
    public int Value;

    public RegistryProbeState(int value) => Value = value;
}

internal sealed class ProbeSystem : IGameLoopSystem<RegistryProbeState>
{
    public SystemId Id { get; }
    public SystemPriority Priority { get; }
    public bool IsEnabled { get; set; } = true;
    public int BeforeUpdates;
    public int Updates;
    public bool Disposed;

    public ProbeSystem(string name, int priority = 0)
    {
        Id = SystemId.FromString(name);
        Priority = new SystemPriority(priority);
    }

    public void BeforeUpdate(ref readonly BeforeUpdateContext<RegistryProbeState> ctx) => BeforeUpdates++;
    public void FixedUpdate(ref readonly FixedUpdateContext<RegistryProbeState> ctx) => Updates++;
    public void Update(ref readonly UpdateContext<RegistryProbeState> ctx) => Updates++;
    public void Render(ref readonly RenderContext<RegistryProbeState> ctx) => Updates++;
    public void AfterFrame(ref readonly AfterFrameContext<RegistryProbeState> ctx) => Updates++;
    public void Dispose() => Disposed = true;
}

internal sealed class FailingSystem : IGameLoopSystem<RegistryProbeState>
{
    public SystemId Id { get; } = SystemId.FromString("failing");
    public int Calls;

    public void Update(ref readonly UpdateContext<RegistryProbeState> ctx)
    {
        Calls++;
        throw new InvalidOperationException("boom");
    }
}

public sealed class GameLoopRegistryTests
{
    [Fact]
    public void Register_Sorts_ByPriority_ThenInsertion()
    {
        using var registry = new SystemRegistry<RegistryProbeState>();
        registry.Register(new ProbeSystem("low", -10));
        registry.Register(new ProbeSystem("high", 10));
        registry.Register(new ProbeSystem("mid", 0));

        List<uint> order = new();
        foreach (var node in registry)
        {
            order.Add(node.Id.Value);
        }

        order.Should().HaveCount(3);
        order[0].Should().Be(SystemId.FromString("high").Value);
        order[1].Should().Be(SystemId.FromString("mid").Value);
        order[2].Should().Be(SystemId.FromString("low").Value);
    }

    [Fact]
    public void Register_Duplicate_Throws()
    {
        using var registry = new SystemRegistry<RegistryProbeState>();
        registry.Register(new ProbeSystem("dup"));
        Action again = () => registry.Register(new ProbeSystem("dup"));
        again.Should().Throw<GameLoopConfigurationException>().WithMessage("*already registered*");
    }

    [Fact]
    public void Unregister_Removes_AndDisposes()
    {
        using var registry = new SystemRegistry<RegistryProbeState>();
        var system = new ProbeSystem("temp");
        registry.Register(system);
        registry.Unregister(system.Id).Should().BeTrue();
        system.Disposed.Should().BeTrue();
        registry.Contains(system.Id).Should().BeFalse();
        registry.Unregister(system.Id).Should().BeFalse();
    }

    [Fact]
    public void Dispatch_Handles_Errors()
    {
        using var registry = new SystemRegistry<RegistryProbeState>();
        var ok = new ProbeSystem("ok");
        var failing = new FailingSystem();
        registry.Register(ok);
        registry.Register(failing);

        var state = new RegistryProbeState();
        var ctx = new UpdateContext<RegistryProbeState>(
            ref state, FrameIndex.Zero, GameTime.Zero, GameTime.Zero,
            GameTime.FromMilliseconds(16), GameTime.FromMilliseconds(16),
            GameTime.Zero, FixedStep.SixtyFps, TimeScale.Identity);

        bool threw = false;
        try
        {
            registry.DispatchUpdate<FailFastErrorPolicy<RegistryProbeState>>(in ctx, ref state);
        }
        catch (GameLoopSystemException)
        {
            threw = true;
        }

        threw.Should().BeTrue();

        failing.Calls.Should().Be(1);
        ok.Updates.Should().Be(1);
    }

    [Fact]
    public void Dispatch_Skips_Disabled()
    {
        using var registry = new SystemRegistry<RegistryProbeState>();
        var system = new ProbeSystem("skipped") { IsEnabled = false };
        registry.Register(system);

        var state = new RegistryProbeState();
        var ctx = new BeforeUpdateContext<RegistryProbeState>(
            ref state, FrameIndex.Zero, GameTime.Zero, GameTime.Zero,
            GameTime.FromMilliseconds(16), GameTime.FromMilliseconds(16),
            GameTime.Zero, FixedStep.SixtyFps, TimeScale.Identity);

        registry.DispatchBeforeUpdate<SuppressAndContinueErrorPolicy<RegistryProbeState>>(in ctx, ref state);
        system.BeforeUpdates.Should().Be(0);
    }
}
