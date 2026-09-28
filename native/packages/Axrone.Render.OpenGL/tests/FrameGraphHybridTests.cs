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
/// Tests for the pump-driven <see cref="FG.Execute"/> walk: pump-capable passes
/// enqueue into the graph-owned pump, classic passes run direct, and the GLOBAL
/// topological order is preserved across the interleaving.
/// </summary>
/// <remarks>
/// The graph owns its internal <see cref="PassExecutionContext"/> and tests cannot
/// register resources into it, so the pump-capable test passes capture their
/// framebuffers directly and enqueue real blit commands whose handles resolve
/// through the context registry when pumped. Distinct framebuffer sizes give every
/// pass a distinct <c>BlitFramebuffer</c> log line, which is what the ordering
/// assertions key on.
/// </remarks>
public sealed class FrameGraphHybridTests : IDisposable
{
    private const string DirectMarker = "Viewport(1, 2, 8, 16)";
    private const string FallbackMarker = "Viewport(3, 4, 5, 6)";

    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public FrameGraphHybridTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    [Fact]
    public void Execute_InterleavesPumpedAndDirectPasses_InGraphOrder()
    {
        using var graph = new FG(_context);
        using var aSource = new GLFramebuffer(_context, 64, 64, "a-src");
        using var aDestination = new GLFramebuffer(_context, 64, 64, "a-dst");
        using var bSource = new GLFramebuffer(_context, 32, 32, "b-src");
        using var bDestination = new GLFramebuffer(_context, 32, 32, "b-dst");

        var blitA = new PumpBlitPass("blitA", aSource, aDestination);
        blitA.AddWrite("stageA");
        var direct = MarkerPass("direct", reads: ["stageA"], writes: ["stageB"]);
        var blitB = new PumpBlitPass("blitB", bSource, bDestination);
        blitB.AddRead("stageB");

        // Added in reverse on purpose: only the compiled dependency order
        // (blitA -> direct -> blitB) can produce the asserted GL order.
        graph.AddPass(blitB);
        graph.AddPass(direct);
        graph.AddPass(blitA);

        _mock.ClearCallLog();
        graph.Execute();

        List<string> log = _mock.CallLog.ToList();
        int a = IndexOfBlit(log, 64);
        int marker = IndexOfMarker(log, DirectMarker);
        int b = IndexOfBlit(log, 32);

        a.Should().BeGreaterThanOrEqualTo(0, "pass blitA enqueues into the pump");
        marker.Should().BeGreaterThanOrEqualTo(0, "the direct pass must run");
        b.Should().BeGreaterThanOrEqualTo(0, "pass blitB enqueues into the pump");

        a.Should().BeLessThan(marker, "pumped work lands before the direct pass");
        marker.Should().BeLessThan(b, "the direct pass drains before it runs, blitB enqueues after");

        blitA.EnqueueAttempts.Should().Be(1);
        blitB.EnqueueAttempts.Should().Be(1);
        blitA.DirectExecutionCount.Should().Be(0, "an accepted enqueue never takes the direct leg");
        blitB.DirectExecutionCount.Should().Be(0, "an accepted enqueue never takes the direct leg");

        graph.PumpQueuedCommands().Should().Be(0u, "the final drain leaves nothing queued");
    }

    [Fact]
    public void Execute_DirectPass_SeesPumpedWorkFromEarlierPasses()
    {
        using var graph = new FG(_context);
        using var source = new GLFramebuffer(_context, 64, 64, "src");
        using var destination = new GLFramebuffer(_context, 64, 64, "dst");

        int blitsSeenByDirectPass = -1;
        var blit = new PumpBlitPass("blit", source, destination);
        blit.AddWrite("stage");
        var direct = CustomPass.Create("direct", FramePassKind.Custom, (context, _) =>
        {
            // Snapshot the log at the moment the direct pass runs: a blit that
            // pumped work from the earlier pass is already applied here.
            blitsSeenByDirectPass = _mock.CallLog
                .Count(c => c.StartsWith("BlitFramebuffer(", StringComparison.Ordinal));
            context.GL.Viewport(1, 2, 8, 16);
        }, reads: ["stage"]);

        graph.AddPass(blit);
        graph.AddPass(direct);

        _mock.ClearCallLog();
        graph.Execute();

        blitsSeenByDirectPass.Should().Be(1, "drain-before-direct must apply the pumped blit first");
        _mock.CallLog.Should().Contain(DirectMarker);
        _mock.CallLog.Should().Contain(c => c.StartsWith("BlitFramebuffer(", StringComparison.Ordinal));
    }

    [Fact]
    public void Execute_DirectPassFirst_SeesNoPumpedWorkYet()
    {
        // Negative control for the test above: with nothing enqueued before the
        // direct pass, the drain-before-direct rule must pump zero commands.
        using var graph = new FG(_context);
        using var source = new GLFramebuffer(_context, 64, 64, "src");
        using var destination = new GLFramebuffer(_context, 64, 64, "dst");

        int blitsSeenByDirectPass = -1;
        var direct = CustomPass.Create("direct", FramePassKind.Custom, (context, _) =>
        {
            blitsSeenByDirectPass = _mock.CallLog
                .Count(c => c.StartsWith("BlitFramebuffer(", StringComparison.Ordinal));
            context.GL.Viewport(1, 2, 8, 16);
        }, writes: ["stage"]);
        var blit = new PumpBlitPass("blit", source, destination);
        blit.AddRead("stage");

        graph.AddPass(direct);
        graph.AddPass(blit);

        _mock.ClearCallLog();
        graph.Execute();

        blitsSeenByDirectPass.Should().Be(0);
        List<string> log = _mock.CallLog.ToList();
        IndexOfBlit(log, 64).Should().BeGreaterThan(
            IndexOfMarker(log, DirectMarker),
            "the trailing pump pass executes at the final drain, after the direct pass");
    }

    [Fact]
    public void Execute_RefusedEnqueue_DrainsAndRetries_AndBothPassesRun()
    {
        // Capacity 2 is the smallest legal ring (ExecutorOptions requires a power
        // of two >= 2), so pass A fills it completely and pass B's enqueue is
        // refused: the drain-retry leg is the only way B can still run pumped.
        using var graph = new FG(_context, 2);
        using var aSource = new GLFramebuffer(_context, 64, 64, "a-src");
        using var aDestination = new GLFramebuffer(_context, 64, 64, "a-dst");
        using var bSource = new GLFramebuffer(_context, 32, 32, "b-src");
        using var bDestination = new GLFramebuffer(_context, 32, 32, "b-dst");

        var blitA = new PumpBlitPass("blitA", aSource, aDestination, commandCount: 2);
        blitA.AddWrite("stageA");
        var blitB = new PumpBlitPass("blitB", bSource, bDestination);
        blitB.AddRead("stageA");

        graph.AddPass(blitA);
        graph.AddPass(blitB);

        _mock.ClearCallLog();
        graph.Execute();

        blitB.EnqueueAttempts.Should().Be(2, "the refused enqueue is retried once after the drain");
        blitA.DirectExecutionCount.Should().Be(0);
        blitB.DirectExecutionCount.Should().Be(0, "the retry succeeded, so no direct fallback");

        List<string> log = _mock.CallLog.ToList();
        log.Count(c => c.StartsWith("BlitFramebuffer(0, 0, 64, 64", StringComparison.Ordinal))
            .Should().Be(2, "both of pass A's pumped commands ran");
        log.Count(c => c.StartsWith("BlitFramebuffer(0, 0, 32, 32", StringComparison.Ordinal))
            .Should().Be(1, "pass B ran pumped after the retry");

        IndexOfBlit(log, 64).Should().BeLessThan(
            IndexOfBlit(log, 32),
            "the drain that frees the ring also lands pass A's work ahead of pass B");

        graph.PumpQueuedCommands().Should().Be(0u);
    }

    [Fact]
    public void Execute_PersistentlyRefusedEnqueue_FallsBackToDirectPath()
    {
        using var graph = new FG(_context);
        using var source = new GLFramebuffer(_context, 64, 64, "src");
        using var destination = new GLFramebuffer(_context, 64, 64, "dst");

        var blit = new PumpBlitPass("blit", source, destination);
        blit.AddWrite("stage");
        var refusing = new RefusingPumpPass("refusing");
        refusing.AddRead("stage");

        graph.AddPass(blit);
        graph.AddPass(refusing);

        _mock.ClearCallLog();
        graph.Execute();

        refusing.EnqueueAttempts.Should().Be(2, "one attempt plus one drain-retry");
        refusing.DirectExecutionCount.Should().Be(1, "a still-refused pass runs exactly once, direct");

        List<string> log = _mock.CallLog.ToList();
        IndexOfBlit(log, 64).Should().BeGreaterThanOrEqualTo(0);
        IndexOfMarker(log, FallbackMarker).Should().BeGreaterThan(
            IndexOfBlit(log, 64),
            "the drain inside the refusal path lands the earlier pumped work first");

        graph.PumpQueuedCommands().Should().Be(0u);
    }

    [Fact]
    public void Execute_WithoutPumpCapablePasses_RunsDirectPassesOnly()
    {
        using var graph = new FG(_context);
        var direct = MarkerPass("direct");
        graph.AddPass(direct);

        _mock.ClearCallLog();
        var action = () => graph.Execute();

        action.Should().NotThrow();
        _mock.CallLog.Should().Contain(DirectMarker);
        _mock.CallLog.Should().NotContain(c => c.StartsWith("BlitFramebuffer(", StringComparison.Ordinal));
        graph.PumpQueuedCommands().Should().Be(0u, "pumping an empty ring is a no-op");
    }

    [Fact]
    public void Execute_SkipsDisabledPumpPass()
    {
        using var graph = new FG(_context);
        using var source = new GLFramebuffer(_context, 64, 64, "src");
        using var destination = new GLFramebuffer(_context, 64, 64, "dst");

        var blit = new PumpBlitPass("blit", source, destination) { IsEnabled = false };
        graph.AddPass(blit);
        graph.AddPass(MarkerPass("direct"));

        _mock.ClearCallLog();
        graph.Execute();

        blit.EnqueueAttempts.Should().Be(0);
        blit.DirectExecutionCount.Should().Be(0);
        _mock.CallLog.Should().NotContain(c => c.StartsWith("BlitFramebuffer(", StringComparison.Ordinal));
        _mock.CallLog.Should().Contain(DirectMarker);
    }

    [Fact]
    public void Execute_FailingDirectPass_ThrowsRenderExceptionWithPassName()
    {
        using var graph = new FG(_context);
        var boom = CustomPass.Create("boom", FramePassKind.Custom, (_, _) =>
            throw new InvalidOperationException("pass body blew up"));
        graph.AddPass(boom);

        var action = () => graph.Execute();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.PassExecutionFailed)
            .WithMessage($"*Pass '{nameof(boom)}' failed: pass body blew up*");
    }

    [Fact]
    public void Execute_FailingDirectPassAfterPumpedPass_StillThrowsRenderException()
    {
        using var graph = new FG(_context);
        using var source = new GLFramebuffer(_context, 64, 64, "src");
        using var destination = new GLFramebuffer(_context, 64, 64, "dst");

        var blit = new PumpBlitPass("blit", source, destination);
        blit.AddWrite("stage");
        var boom = CustomPass.Create("boom", FramePassKind.Custom, (_, _) =>
            throw new InvalidOperationException("pass body blew up"), reads: ["stage"]);

        graph.AddPass(blit);
        graph.AddPass(boom);

        var action = () => graph.Execute();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.PassExecutionFailed);

        // The drain before the failing direct pass still ran the earlier work.
        _mock.CallLog.Should().Contain(c => c.StartsWith("BlitFramebuffer(", StringComparison.Ordinal));
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    /// <summary>
    /// A direct-only pass that emits the one GL line the ordering assertions key
    /// on. Integral arguments keep the log line culture-independent.
    /// </summary>
    private static RenderPass<CustomPassData> MarkerPass(string name, string[]? reads = null, string[]? writes = null) =>
        CustomPass.Create(name, FramePassKind.Custom, (context, _) => context.GL.Viewport(1, 2, 8, 16),
            reads: reads, writes: writes);

    private static int IndexOfBlit(List<string> log, int size) =>
        log.FindIndex(c => c.StartsWith($"BlitFramebuffer(0, 0, {size}, {size}", StringComparison.Ordinal));

    private static int IndexOfMarker(List<string> log, string marker) =>
        log.FindIndex(c => string.Equals(c, marker, StringComparison.Ordinal));

    /// <summary>
    /// Pump-capable test pass: enqueues <c>commandCount</c> real blit commands for
    /// two captured framebuffers, mirroring the shipped blit pass but resolving
    /// its handles from its own fields (the graph's internal execution context is
    /// unreachable from tests). Written as a plain <see cref="IRenderPass"/>
    /// double because the production passes are sealed generic types.
    /// </summary>
    private sealed class PumpBlitPass : IRenderPass, IPumpEnqueue
    {
        private readonly GLFramebuffer _source;
        private readonly GLFramebuffer _destination;
        private readonly int _commandCount;
        private readonly List<string> _reads = new(2);
        private readonly List<string> _writes = new(2);
        private int _enqueueAttempts;

        public PumpBlitPass(string name, GLFramebuffer source, GLFramebuffer destination, int commandCount = 1)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(commandCount, 1);
            Name = name;
            _source = source;
            _destination = destination;
            _commandCount = commandCount;
        }

        public string Name { get; }

        public bool IsEnabled { get; set; } = true;

        public RenderPassDescriptor Descriptor => default;

        public AttachmentLoadAction LoadAction => AttachmentLoadAction.Load;

        public AttachmentStoreAction StoreAction => AttachmentStoreAction.Store;

        /// <summary>Gets how many times the graph asked this pass to enqueue.</summary>
        public int EnqueueAttempts
        {
            get => _enqueueAttempts;
        }

        /// <summary>Gets how many times the graph ran this pass on the direct path.</summary>
        public int DirectExecutionCount { get; private set; }

        public void AddRead(string resource) => _reads.Add(resource);

        public void AddWrite(string resource) => _writes.Add(resource);

        public ReadOnlySpan<string> GetReadResources() => _reads.ToArray();

        public ReadOnlySpan<string> GetWrittenResources() => _writes.ToArray();

        public void Validate()
        {
        }

        public void Execute(IRenderContext context) => DirectExecutionCount++;

        public EnqueueResult EnqueueCommands(RenderPump pump, PassExecutionContext ctx)
        {
            _enqueueAttempts++;
            EnqueueResult receipt = default;
            for (int i = 0; i < _commandCount; i++)
            {
                DescriptorHandle<GLResourceNode> sourceHandle = _source.RegistryHandle;
                DescriptorHandle<GLResourceNode> destinationHandle = _destination.RegistryHandle;

                RenderCommand command = RenderCommand.CreateBlit(
                    in sourceHandle, in destinationHandle,
                    0, 0, _source.Width, _source.Height,
                    0, 0, _destination.Width, _destination.Height,
                    GLConst.ColorBufferBit, GLConst.NearestFilter);
                receipt = pump.TryEnqueue(in command);
            }

            return receipt;
        }
    }

    /// <summary>
    /// Pump-capable pass that always refuses, so the graph's refusal fallback has
    /// to run its direct path. Emits a distinct GL line when it does.
    /// </summary>
    private sealed class RefusingPumpPass : IRenderPass, IPumpEnqueue
    {
        private readonly List<string> _reads = new(2);
        private readonly List<string> _writes = new(2);

        public RefusingPumpPass(string name) => Name = name;

        public string Name { get; }

        public bool IsEnabled { get; set; } = true;

        public RenderPassDescriptor Descriptor => default;

        public AttachmentLoadAction LoadAction => AttachmentLoadAction.Load;

        public AttachmentStoreAction StoreAction => AttachmentStoreAction.Store;

        /// <summary>Gets how many times the graph asked this pass to enqueue.</summary>
        public int EnqueueAttempts { get; private set; }

        /// <summary>Gets how many times the graph ran this pass on the direct path.</summary>
        public int DirectExecutionCount { get; private set; }

        public void AddRead(string resource) => _reads.Add(resource);

        public void AddWrite(string resource) => _writes.Add(resource);

        public ReadOnlySpan<string> GetReadResources() => _reads.ToArray();

        public ReadOnlySpan<string> GetWrittenResources() => _writes.ToArray();

        public void Validate()
        {
        }

        public EnqueueResult EnqueueCommands(RenderPump pump, PassExecutionContext ctx)
        {
            EnqueueAttempts++;
            return new EnqueueResult(-1, EnqueueStatus.QueueFull);
        }

        public void Execute(IRenderContext context)
        {
            DirectExecutionCount++;
            ((GLRenderContext)context).GLContext.GL.Viewport(3, 4, 5, 6);
        }
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
