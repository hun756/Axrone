using Xunit;
using FluentAssertions;
using Axrone.Execution;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.FrameGraph.Passes;
using Axrone.Render.OpenGL.Resources;
using Axrone.Utility.Backoff.SpinPolicies;

using RenderPump = Axrone.Execution.CommandPump<
    Axrone.Render.Core.RenderCommand,
    Axrone.Render.OpenGL.FrameGraph.RenderPumpContext,
    Axrone.Render.OpenGL.FrameGraph.RenderCommandProcessor,
    Axrone.Utility.Backoff.SpinPolicies.AdaptiveSpinBackoff,
    Axrone.Execution.NullExecutorTelemetry>;

namespace Axrone.Render.OpenGL.Tests;

public class RenderPumpTests
{
    private static (GLContext Context, MockGLApi Mock, GLFramebuffer Source, GLFramebuffer Destination) CreateFrame()
    {
        var mock = new MockGLApi();
        var context = new GLContext(mock);
        var source = new GLFramebuffer(context, 64, 64, "src");
        var destination = new GLFramebuffer(context, 64, 64, "dst");
        return (context, mock, source, destination);
    }

    [Fact]
    public void BlitThroughPump_IssuesSameBlitAsDirect()
    {
        var (context, mock, source, destination) = CreateFrame();
        var ctx = new PassExecutionContext(context);
        ctx.SetResource("src", source);
        ctx.SetResource("dst", destination);

        var pass = BlitPass.Create("blit", "src", "dst");
        using var pump = new RenderPump(new ExecutorOptions { Capacity = 8 });

        pass.EnqueueCommands(pump, ctx).IsEnqueued.Should().BeTrue();

        var pumpContext = new RenderPumpContext(context);
        pump.PumpAll(ref pumpContext).Should().Be((nuint)1);

        mock.CallLog.Should().Contain(c => c.Contains("BlitFramebuffer(0, 0, 64, 64, 0, 0, 64, 64", StringComparison.Ordinal));

        source.Dispose();
        destination.Dispose();
        context.Dispose();
    }

    [Fact]
    public void StaleHandle_FailsPump()
    {
        var (context, _, source, destination) = CreateFrame();
        var ctx = new PassExecutionContext(context);
        ctx.SetResource("src", source);
        ctx.SetResource("dst", destination);

        var pass = BlitPass.Create("blit", "src", "dst");
        using var pump = new RenderPump(new ExecutorOptions { Capacity = 8 });
        pass.EnqueueCommands(pump, ctx).IsEnqueued.Should().BeTrue();

        source.Dispose(); // stale source handle

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

        threw.Should().BeTrue("stale handle must fail the pump");

        destination.Dispose();
        context.Dispose();
    }
}
