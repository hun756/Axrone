using Xunit;
using FluentAssertions;
using Axrone.Execution;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.FrameGraph.PassExecutors;
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
/// Tests for the pump-derived present path: the packet replayed through
/// <see cref="RenderCommandProcessor"/> must blit the source onto framebuffer 0 —
/// the case with no destination handle at all, because the default framebuffer
/// is not registry-managed — and a present whose source went stale must refuse to
/// blit anything.
/// </summary>
public class PresentPumpTests
{
    private static (GLContext Context, MockGLApi Mock, PassExecutionContext Ctx, GLFramebuffer Source) CreateFrame()
    {
        var mock = new MockGLApi();
        var context = new GLContext(mock);
        var source = new GLFramebuffer(context, 64, 64, "x");
        var ctx = new PassExecutionContext(context);
        ctx.SetResource("x", source);
        return (context, mock, ctx, source);
    }

    [Fact]
    public void PresentThroughPump_BlitsSourceOntoDefaultFramebuffer()
    {
        var (context, mock, ctx, source) = CreateFrame();

        // A scaled-up linear-filtered present: every packet field is non-default,
        // so a lost rectangle, mask, or filter shows up as a log diff.
        var pass = new PresentPassExecutor(
            "present", "x",
            destinationWidth: 128,
            destinationHeight: 128,
            filterMode: GLConst.LinearFilter);

        mock.ClearCallLog();

        using var pump = new RenderPump(new ExecutorOptions { Capacity = 8 });
        pass.EnqueueCommands(pump, ctx).IsEnqueued.Should().BeTrue();

        var pumpContext = new RenderPumpContext(context);
        pump.PumpAll(ref pumpContext).Should().Be((nuint)1);

        IReadOnlyList<string> log = mock.CallLog;
        int blitIndex = IndexOfBlit(log);
        blitIndex.Should().BeGreaterThanOrEqualTo(0, "the pump path must reach the GL API");

        // Full source rect read, full destination rect written, mask and filter intact.
        log[blitIndex].Should().Be(
            $"BlitFramebuffer(0, 0, 64, 64, 0, 0, 128, 128, {GLConst.ColorBufferBit}, {GLConst.LinearFilter})");

        // Both bind sites must precede the blit: reads on the resolved framebuffer,
        // draws on framebuffer 0 — present carries no destination handle, so the
        // draw side can only be the default framebuffer.
        List<string> before = log.Take(blitIndex).ToList();
        before.Should().ContainSingle(l => l == $"BindFramebuffer({GLConst.ReadFramebuffer}, {source.Id})");
        before.Should().ContainSingle(l => l == $"BindFramebuffer({GLConst.DrawFramebuffer}, 0)");

        source.Dispose();
        context.Dispose();
    }

    [Fact]
    public void PresentThroughPump_IssuesSameBlitAsDirect()
    {
        var (context, mock, ctx, source) = CreateFrame();
        var (directContext, directMock, directCtx, directSource) = CreateFrame();

        var pass = new PresentPassExecutor(
            "present", "x",
            destinationWidth: 128,
            destinationHeight: 128,
            filterMode: GLConst.LinearFilter);

        mock.ClearCallLog();
        directMock.ClearCallLog();

        pass.Execute(directContext, directCtx);

        using var pump = new RenderPump(new ExecutorOptions { Capacity = 8 });
        pass.EnqueueCommands(pump, ctx).IsEnqueued.Should().BeTrue();

        var pumpContext = new RenderPumpContext(context);
        pump.PumpAll(ref pumpContext).Should().Be((nuint)1);

        mock.CallLog.Should().NotBeEmpty("the pump path must reach the GL API");

        // Guard against a vacuous pass: two empty filters would compare equal.
        List<string> pumpLines = PresentRelevant(mock);
        pumpLines.Should().NotBeEmpty("the filtered lines are what direct/pump parity is about");
        pumpLines.Should().Equal(PresentRelevant(directMock));

        source.Dispose();
        directSource.Dispose();
        context.Dispose();
        directContext.Dispose();
    }

    [Fact]
    public void PresentThroughPump_UnsetDestinationFollowsSourceSize()
    {
        var (context, mock, ctx, source) = CreateFrame();

        // Zero destination dimensions mean "follow the source", the same sizing
        // rule Execute applies.
        var pass = new PresentPassExecutor("present", "x");

        mock.ClearCallLog();

        using var pump = new RenderPump(new ExecutorOptions { Capacity = 8 });
        pass.EnqueueCommands(pump, ctx).IsEnqueued.Should().BeTrue();

        var pumpContext = new RenderPumpContext(context);
        pump.PumpAll(ref pumpContext).Should().Be((nuint)1);

        mock.CallLog.Should().Contain(
            c => c == $"BlitFramebuffer(0, 0, 64, 64, 0, 0, 64, 64, {GLConst.ColorBufferBit}, {GLConst.NearestFilter})");

        source.Dispose();
        context.Dispose();
    }

    [Fact]
    public void StaleSourceThroughPump_FailsPump()
    {
        var (context, mock, ctx, source) = CreateFrame();

        var pass = new PresentPassExecutor("present", "x");
        using var pump = new RenderPump(new ExecutorOptions { Capacity = 8 });
        pass.EnqueueCommands(pump, ctx).IsEnqueued.Should().BeTrue();

        source.Dispose(); // stale source handle

        mock.ClearCallLog();

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

        threw.Should().BeTrue("stale source handle must fail the pump");
        mock.CallLog.Should().NotContain(c => c.StartsWith("BlitFramebuffer(", StringComparison.Ordinal),
            "a failed present must not blit anything, not even onto framebuffer 0");

        context.Dispose();
    }

    private static int IndexOfBlit(IReadOnlyList<string> log)
    {
        for (int i = 0; i < log.Count; i++)
        {
            if (log[i].StartsWith("BlitFramebuffer(", StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// The GL calls a present is defined by: the read/draw framebuffer binds that
    /// select the blit endpoints, and the blit itself. The direct path
    /// additionally rebinds <see cref="GLConst.Framebuffer"/> to 0 and resets the
    /// viewport after the blit — a presentation courtesy the packet does not
    /// carry — so equality is asserted over the blit-relevant lines only.
    /// </summary>
    private static List<string> PresentRelevant(MockGLApi mock) => mock.CallLog
        .Where(l => l.StartsWith("BlitFramebuffer(", StringComparison.Ordinal)
            || IsBind(l, GLConst.ReadFramebuffer)
            || IsBind(l, GLConst.DrawFramebuffer))
        .ToList();

    private static bool IsBind(string line, uint target) =>
        line.StartsWith($"BindFramebuffer({target}, ", StringComparison.Ordinal);
}
