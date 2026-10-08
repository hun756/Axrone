using Xunit;
using FluentAssertions;
using Axrone.Execution;
using Axrone.Render.Core.Abstractions;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.FrameGraph.Passes;
using Axrone.Render.OpenGL.Native;
using Axrone.Render.OpenGL.Resources;

using RenderPump = Axrone.Execution.CommandPump<
    Axrone.Render.Core.RenderCommand,
    Axrone.Render.OpenGL.FrameGraph.RenderPumpContext,
    Axrone.Render.OpenGL.FrameGraph.RenderCommandProcessor,
    Axrone.Utility.Backoff.SpinPolicies.AdaptiveSpinBackoff,
    Axrone.Execution.NullExecutorTelemetry>;

namespace Axrone.Render.OpenGL.Tests;

/// <summary>
/// Tests for the pump-derived clear path: the packet replayed through
/// <see cref="RenderCommandProcessor"/> must produce the same GL call sequence as
/// the direct leg of the clear pass, and a clear with no registry-backed
/// target must refuse to enqueue instead of fabricating a handle.
/// </summary>
public class ClearPumpTests
{
    private static (GLContext Context, MockGLApi Mock, PassExecutionContext Ctx, GLFramebuffer Target) CreateFrame()
    {
        var mock = new MockGLApi();
        var context = new GLContext(mock);
        var target = new GLFramebuffer(context, 64, 64, "x");
        var ctx = new PassExecutionContext(context);
        ctx.SetResource("x", target);
        return (context, mock, ctx, target);
    }

    /// <summary>
    /// Runs one pass on the direct leg: the same
    /// <see cref="IRenderContext"/>-native seam the frame graph uses, built over
    /// the pass's own <see cref="PassExecutionContext"/>.
    /// </summary>
    private static void ExecuteDirect(GLContext context, IRenderPass pass, PassExecutionContext ctx)
    {
        var renderCtx = new GLRenderContext(context, ctx);
        pass.Execute(renderCtx);
    }

    [Fact]
    public void ClearThroughPump_IssuesSameCallsAsDirect()
    {
        var (context, mock, ctx, target) = CreateFrame();
        var (directContext, directMock, directCtx, directTarget) = CreateFrame();

        // Distinctive, non-default values on every enabled buffer: any value lost
        // or reordered between the packet and the processor shows up as a log diff.
        var pass = ClearPass.Create(
            "clear", "x",
            new Vec4(0.1f, 0.2f, 0.3f, 1.0f),
            clearDepth: 0.5f,
            clearStencil: 3,
            clearStencilEnabled: true);

        mock.ClearCallLog();
        directMock.ClearCallLog();

        ExecuteDirect(directContext, pass, directCtx);

        using var pump = new RenderPump(new ExecutorOptions { Capacity = 8 });
        pass.EnqueueCommands(pump, ctx).IsEnqueued.Should().BeTrue();

        var pumpContext = new RenderPumpContext(context);
        pump.PumpAll(ref pumpContext).Should().Be((nuint)1);

        mock.CallLog.Should().NotBeEmpty("the pump path must reach the GL API");
        mock.CallLog.Should().Equal(directMock.CallLog);

        target.Dispose();
        directTarget.Dispose();
        context.Dispose();
        directContext.Dispose();
    }

    [Fact]
    public void ClearThroughPump_IssuesColorAndDepthOnNamedFramebuffer()
    {
        var (context, mock, ctx, target) = CreateFrame();

        // Integral clear values keep the log assertions independent of the
        // ambient culture's decimal separator.
        var pass = ClearPass.Create("clear", "x", new Vec4(1f, 1f, 1f, 1f));
        mock.ClearCallLog();

        using var pump = new RenderPump(new ExecutorOptions { Capacity = 8 });
        pass.EnqueueCommands(pump, ctx).IsEnqueued.Should().BeTrue();

        var pumpContext = new RenderPumpContext(context);
        pump.PumpAll(ref pumpContext).Should().Be((nuint)1);

        mock.CallLog.Should().ContainSingle(c => c == $"BindFramebuffer({GLConst.Framebuffer}, {target.Id})");
        mock.CallLog.Should().ContainSingle(c => c == "ClearColor(1, 1, 1, 1)");
        mock.CallLog.Should().ContainSingle(c => c == "ClearDepth(1)");
        mock.CallLog.Should().ContainSingle(c => c == $"Clear({GLConst.ColorBufferBit | GLConst.DepthBufferBit})");
        mock.CallLog.Should().NotContain(c => c.StartsWith("ClearStencil(", StringComparison.Ordinal));

        target.Dispose();
        context.Dispose();
    }

    [Fact]
    public void DefaultTargetThroughPump_ReportsClosed()
    {
        var (context, mock, ctx, target) = CreateFrame();

        var pass = ClearPass.Create("clear"); // default framebuffer
        using var pump = new RenderPump(new ExecutorOptions { Capacity = 8 });

        EnqueueResult receipt = pass.EnqueueCommands(pump, ctx);

        receipt.IsEnqueued.Should().BeFalse("FBO 0 has no registry handle to carry");
        receipt.Status.Should().Be(EnqueueStatus.Closed);
        receipt.SequenceNumber.Should().Be(-1);

        var pumpContext = new RenderPumpContext(context);
        pump.PumpAll(ref pumpContext).Should().Be((nuint)0);
        mock.CallLog.Should().NotContain(c => c.StartsWith("Clear(", StringComparison.Ordinal));

        target.Dispose();
        context.Dispose();
    }

    [Fact]
    public void UnresolvableTargetThroughPump_ReportsClosed()
    {
        var (context, mock, ctx, target) = CreateFrame();

        var pass = ClearPass.Create("clear", "not-registered");
        using var pump = new RenderPump(new ExecutorOptions { Capacity = 8 });

        EnqueueResult receipt = pass.EnqueueCommands(pump, ctx);

        receipt.Status.Should().Be(EnqueueStatus.Closed);
        receipt.SequenceNumber.Should().Be(-1);

        var pumpContext = new RenderPumpContext(context);
        pump.PumpAll(ref pumpContext).Should().Be((nuint)0);
        mock.CallLog.Should().NotContain(c => c.StartsWith("Clear(", StringComparison.Ordinal));

        target.Dispose();
        context.Dispose();
    }

    [Fact]
    public void DisposedTarget_FailsPump()
    {
        var (context, mock, ctx, target) = CreateFrame();

        var pass = ClearPass.Create("clear", "x", new Vec4(1f, 1f, 1f, 1f));
        using var pump = new RenderPump(new ExecutorOptions { Capacity = 8 });
        pass.EnqueueCommands(pump, ctx).IsEnqueued.Should().BeTrue();

        target.Dispose(); // stale target handle

        var pumpContext = new RenderPumpContext(context);
        bool threw = false;
        try
        {
            pump.PumpAll(ref pumpContext);
        }
        catch (Exception)
        {
            threw = true;
        }

        threw.Should().BeTrue("stale target handle must fail the pump");
        mock.CallLog.Should().NotContain(c => c.StartsWith("Clear(", StringComparison.Ordinal));

        context.Dispose();
    }
}
