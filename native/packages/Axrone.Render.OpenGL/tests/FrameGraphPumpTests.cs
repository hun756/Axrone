using Axrone.Execution;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.FrameGraph.PassExecutors;
using Axrone.Render.OpenGL.Native;
using Axrone.Render.OpenGL.Resources;
using Axrone.Utility.Descriptors;

// Alias to avoid ambiguity between namespace Axrone.Render.OpenGL.FrameGraph
// and class FrameGraph within that namespace.
using FG = global::Axrone.Render.OpenGL.FrameGraph.FrameGraph;

using RenderPump = Axrone.Execution.CommandPump<
    Axrone.Render.Core.RenderCommand,
    Axrone.Render.OpenGL.FrameGraph.RenderPumpContext,
    Axrone.Render.OpenGL.FrameGraph.RenderCommandProcessor,
    Axrone.Utility.Backoff.SpinPolicies.AdaptiveSpinBackoff,
    Axrone.Execution.NullExecutorTelemetry>;

namespace Axrone.Render.OpenGL.Tests;

/// <summary>
/// Tests for the FrameGraph command-pump wiring: pump-capable passes
/// (<see cref="IPumpEnqueue"/>) enqueue library commands into the graph-owned
/// pump while classic passes keep their direct <see cref="RenderPass.Execute"/>
/// path (hybrid graph).
/// </summary>
/// <remarks>
/// The graph owns its internal <see cref="PassExecutionContext"/> and tests
/// cannot register resources into it, so the pump-capable test pass captures
/// its framebuffers directly and enqueues a real blit command whose handles
/// resolve through the context registry when pumped.
/// </remarks>
public sealed class FrameGraphPumpTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public FrameGraphPumpTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    [Fact]
    public void EnqueuePumpPasses_WithPumpPass_EnqueuesAndPumpsBlit()
    {
        using var graph = new FG(_context);
        var source = new GLFramebuffer(_context, 64, 64, "src");
        var destination = new GLFramebuffer(_context, 64, 64, "dst");
        var pass = new PumpTestPass("blit", source, destination);
        graph.AddPass(pass);

        graph.EnqueuePumpPasses().Should().Be(1u);

        graph.PumpQueuedCommands().Should().Be(1u);

        _mock.CallLog.Should().Contain(c => c.Contains("BlitFramebuffer(0, 0, 64, 64, 0, 0, 64, 64", StringComparison.Ordinal));

        source.Dispose();
        destination.Dispose();
    }

    [Fact]
    public void EnqueuePumpPasses_WithOnlyNonPumpPasses_ReturnsZero_AndExecuteStillWorks()
    {
        using var graph = new FG(_context);
        var pass = new NonPumpTestPass("classic");
        graph.AddPass(pass);

        graph.EnqueuePumpPasses().Should().Be(0u);

        graph.Execute();

        pass.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public void EnqueuePumpPasses_SkipsDisabledPumpPass()
    {
        using var graph = new FG(_context);
        var source = new GLFramebuffer(_context, 64, 64, "src");
        var destination = new GLFramebuffer(_context, 64, 64, "dst");
        var pass = new PumpTestPass("blit", source, destination) { IsEnabled = false };
        graph.AddPass(pass);

        graph.EnqueuePumpPasses().Should().Be(0u);

        source.Dispose();
        destination.Dispose();
    }

    [Fact]
    public void Reset_DoesNotDrainQueuedCommands()
    {
        using var graph = new FG(_context);
        var source = new GLFramebuffer(_context, 64, 64, "src");
        var destination = new GLFramebuffer(_context, 64, 64, "dst");
        graph.AddPass(new PumpTestPass("blit", source, destination));

        graph.EnqueuePumpPasses().Should().Be(1u);

        graph.Reset();
        graph.PassCount.Should().Be(0);

        // Documented semantics: Reset leaves the pump alive and undrained;
        // queued-but-unpumped commands survive and still pump.
        graph.PumpQueuedCommands().Should().Be(1u);

        source.Dispose();
        destination.Dispose();
    }

    [Fact]
    public void PumpQueuedCommands_OnDisposedGraph_ThrowsObjectDisposed()
    {
        var graph = new FG(_context);
        graph.Dispose();

        var action = () => graph.PumpQueuedCommands();

        action.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Constructor_NonPowerOfTwoPumpCapacity_Throws()
    {
        var action = () => new FG(_context, 100);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ========================================================================
    // Helper pass implementations
    // ========================================================================

    /// <summary>
    /// Pump-capable test pass: enqueues a real blit command for two captured
    /// framebuffers, mirroring <see cref="BlitPassExecutor"/> but resolving
    /// resources from its own fields (the graph's internal execution context
    /// is unreachable from tests).
    /// </summary>
    private sealed class PumpTestPass : RenderPass, IPumpEnqueue
    {
        private readonly GLFramebuffer _source;
        private readonly GLFramebuffer _destination;

        public PumpTestPass(string name, GLFramebuffer source, GLFramebuffer destination)
            : base(name, FramePassKind.Blit)
        {
            _source = source;
            _destination = destination;
        }

        public EnqueueResult EnqueueCommands(RenderPump pump, PassExecutionContext ctx)
        {
            DescriptorHandle<GLResourceNode> sourceHandle = _source.RegistryHandle;
            DescriptorHandle<GLResourceNode> destinationHandle = _destination.RegistryHandle;

            RenderCommand command = RenderCommand.CreateBlit(
                in sourceHandle, in destinationHandle,
                0, 0, _source.Width, _source.Height,
                0, 0, _destination.Width, _destination.Height,
                GLConst.ColorBufferBit, GLConst.NearestFilter);
            return pump.TryEnqueue(in command);
        }

        public override void Execute(GLContext context, PassExecutionContext ctx)
        {
        }
    }

    /// <summary>
    /// Classic non-pump pass: direct-execution only, counts executions.
    /// </summary>
    private sealed class NonPumpTestPass : RenderPass
    {
        public int ExecutionCount { get; private set; }

        public NonPumpTestPass(string name)
            : base(name, FramePassKind.Custom)
        {
        }

        public override void Execute(GLContext context, PassExecutionContext ctx)
        {
            ExecutionCount++;
        }
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
