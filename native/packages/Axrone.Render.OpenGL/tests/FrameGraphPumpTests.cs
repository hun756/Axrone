using Axrone.Execution;
using Axrone.Render.Core.Abstractions;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.FrameGraph.Passes;
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
/// pump while classic passes keep their direct <see cref="IRenderPass.Execute(IRenderContext)"/>
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
        graph.AddPass(CreatePumpTestPass("blit", source, destination));

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
        int executionCount = 0;
        graph.AddPass(CustomPass.Create("classic", FramePassKind.Custom, (gl, ctx) => executionCount++));

        graph.EnqueuePumpPasses().Should().Be(0u);

        graph.Execute();

        executionCount.Should().Be(1);
    }

    [Fact]
    public void EnqueuePumpPasses_SkipsDisabledPumpPass()
    {
        using var graph = new FG(_context);
        var source = new GLFramebuffer(_context, 64, 64, "src");
        var destination = new GLFramebuffer(_context, 64, 64, "dst");
        var pass = CreatePumpTestPass("blit", source, destination);
        pass.IsEnabled = false;
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
        graph.AddPass(CreatePumpTestPass("blit", source, destination));

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
    /// Payload of the pump-capable test pass: the two captured framebuffers.
    /// </summary>
    private struct PumpTestPassData
    {
        public GLFramebuffer Source;
        public GLFramebuffer Destination;
    }

    /// <summary>
    /// Creates a pump-capable test pass: enqueues a real blit command for two
    /// captured framebuffers, mirroring <see cref="BlitPass"/> but resolving
    /// resources from its payload (the graph's internal execution context
    /// is unreachable from tests).
    /// </summary>
    private static PumpRenderPass<PumpTestPassData> CreatePumpTestPass(
        string name,
        GLFramebuffer source,
        GLFramebuffer destination)
    {
        return new PumpRenderPass<PumpTestPassData>(
            name,
            FramePassKind.Blit,
            (IRenderPassBuilder builder, ref PumpTestPassData data) =>
            {
                data.Source = source;
                data.Destination = destination;
            },
            (in PumpTestPassData data, IRenderContext context, PassExecutionContext ctx) =>
            {
                // Pump-capable pass: the direct leg has nothing to do.
            },
            (in PumpTestPassData data, RenderPump pump, PassExecutionContext ctx) =>
            {
                DescriptorHandle<GLResourceNode> sourceHandle = data.Source.RegistryHandle;
                DescriptorHandle<GLResourceNode> destinationHandle = data.Destination.RegistryHandle;

                RenderCommand command = RenderCommand.CreateBlit(
                    in sourceHandle, in destinationHandle,
                    0, 0, data.Source.Width, data.Source.Height,
                    0, 0, data.Destination.Width, data.Destination.Height,
                    GLConst.ColorBufferBit, GLConst.NearestFilter);
                return pump.TryEnqueue(in command);
            });
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
