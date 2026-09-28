namespace Axrone.Render.OpenGL.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.FrameGraph.Passes;

// Alias to avoid ambiguity between namespace and the typestate FrameGraph
// handle: FG is the building handle, and Compile() hands out the compiled
// handle that carries Execute().
using FG = global::Axrone.Render.OpenGL.FrameGraph.FrameGraph<global::Axrone.Render.OpenGL.FrameGraph.BuildingPhase, global::Axrone.Render.OpenGL.FrameGraph.DefaultGraphPolicy>;

public class FrameGraphOrderTests
{
    /// <summary>
    /// Creates a pass that appends its name to <paramref name="log"/> on execution
    /// and declares the given resource dependencies at construction.
    /// </summary>
    private static RenderPass<CustomPassData> CreateRecordingPass(string name, List<string> log, string[] reads, string[] writes)
        => CustomPass.Create(name, FramePassKind.Custom, (gl, ctx) => log.Add(name), reads: reads, writes: writes);

    private static GLContext CreateContext()
    {
        var api = new MockGLApi();
        return new GLContext(api);
    }

    private static readonly string[] s_colorRead = new string[] { "color" };
    private static readonly string[] s_empty = Array.Empty<string>();
    private static readonly string[] s_shadedWrite = new string[] { "shaded" };
    private static readonly string[] s_colorShadedRead = new string[] { "color", "shaded" };
    private static readonly string[] s_xWrite = new string[] { "x" };
    private static readonly string[] s_yRead = new string[] { "y" };

    [Fact]
    public void Compile_OrdersWritersBeforeReaders()
    {
        using var context = CreateContext();
        var graph = new FG(context);
        var log = new List<string>();

        // Inserted backwards: reader first, writer last. The reader also consumes
        // the middle pass output, forcing a total order.
        graph.AddPass(CreateRecordingPass("read", log, s_colorShadedRead, s_empty));
        graph.AddPass(CreateRecordingPass("middle", log, s_colorRead, s_shadedWrite));
        graph.AddPass(CreateRecordingPass("write", log, s_empty, s_colorRead));

        graph.Compile().Execute();

        log.Should().Equal("write", "middle", "read");
    }

    [Fact]
    public void Compile_ThrowsOnCycles()
    {
        using var context = CreateContext();
        var graph = new FG(context);
        var log = new List<string>();

        graph.AddPass(CreateRecordingPass("a", log, s_yRead, s_xWrite));
        graph.AddPass(CreateRecordingPass("b", log, s_xWrite, s_yRead));

        Action compile = () => graph.Compile();
        compile.Should().Throw<RenderException>()
            .Where(ex => ex.Code == RenderErrorCode.GraphCycleDetected);
    }
}
