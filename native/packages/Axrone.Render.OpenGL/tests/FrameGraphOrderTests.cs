namespace Axrone.Render.OpenGL.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;

public class FrameGraphOrderTests
{
    private sealed class RecordingPass : RenderPass
    {
        private readonly List<string> _log;

        public RecordingPass(string name, List<string> log, string[] reads, string[] writes)
            : base(name, FramePassKind.Custom)
        {
            _log = log;
            foreach (string read in reads)
            {
                Reads(read);
            }

            foreach (string write in writes)
            {
                Writes(write);
            }
        }

        public override void Execute(GLContext context, PassExecutionContext ctx) => _log.Add(Name);
    }

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
        var graph = new FrameGraph(context);
        var log = new List<string>();

        // Inserted backwards: reader first, writer last. The reader also consumes
        // the middle pass output, forcing a total order.
        graph.AddPass(new RecordingPass("read", log, s_colorShadedRead, s_empty));
        graph.AddPass(new RecordingPass("middle", log, s_colorRead, s_shadedWrite));
        graph.AddPass(new RecordingPass("write", log, s_empty, s_colorRead));

        graph.Compile();
        graph.Execute();

        log.Should().Equal("write", "middle", "read");
    }

    [Fact]
    public void Compile_ThrowsOnCycles()
    {
        using var context = CreateContext();
        var graph = new FrameGraph(context);
        var log = new List<string>();

        graph.AddPass(new RecordingPass("a", log, s_yRead, s_xWrite));
        graph.AddPass(new RecordingPass("b", log, s_xWrite, s_yRead));

        Action compile = () => graph.Compile();
        compile.Should().Throw<RenderException>()
            .Where(ex => ex.Code == RenderErrorCode.GraphCycleDetected);
    }
}
