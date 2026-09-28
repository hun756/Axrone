using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph.Passes;

namespace Axrone.Render.OpenGL.Tests;

// Aliases to avoid ambiguity between namespace Axrone.Render.OpenGL.FrameGraph
// and class FrameGraph within that namespace.
using FG = global::Axrone.Render.OpenGL.FrameGraph.FrameGraph;
using FGP = global::Axrone.Render.OpenGL.FrameGraph.FramePassKind;

/// <summary>
/// Tests for FrameGraph pass scheduling, topological ordering,
/// cycle detection, and PassExecutionContext resource management.
/// </summary>
public sealed class FrameGraphTests : IDisposable
{
    private static readonly string[] s_scene = new string[] { "scene" };
    private static readonly string[] s_resourceA = new string[] { "a" };
    private static readonly string[] s_resourceB = new string[] { "b" };

    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public FrameGraphTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    [Fact]
    public void EmptyFrameGraph_CompilesAndExecutes_WithoutError()
    {
        using var graph = new FG(_context);

        var action = () => graph.Execute();

        action.Should().NotThrow();
    }

    [Fact]
    public void SinglePass_Executes()
    {
        using var graph = new FG(_context);
        int executionCount = 0;
        graph.AddPass(CustomPass.Create("pass1", FGP.Custom, (gl, ctx) => executionCount++));

        graph.Execute();

        executionCount.Should().Be(1);
    }

    [Fact]
    public void MultiplePasses_ExecuteInTopologicalOrder()
    {
        using var graph = new FG(_context);
        var executionOrder = new List<int>();

        // pass1 writes "scene", pass2 reads "scene" => pass1 must come before pass2
        graph.AddPass(CustomPass.Create("pass0", FGP.Custom, (gl, ctx) => executionOrder.Add(0)));
        graph.AddPass(CustomPass.Create("pass1", FGP.Custom, (gl, ctx) => executionOrder.Add(1), writes: s_scene));
        graph.AddPass(CustomPass.Create("pass2", FGP.Custom, (gl, ctx) => executionOrder.Add(2), reads: s_scene));

        graph.Execute();

        executionOrder.Should().HaveCount(3);
        // pass1 must come before pass2
        executionOrder.IndexOf(1).Should().BeLessThan(executionOrder.IndexOf(2));
    }

    [Fact]
    public void CycleDetection_ThrowsGraphCycleDetected()
    {
        using var graph = new FG(_context);

        graph.AddPass(CustomPass.Create(
            "passA",
            FGP.Custom,
            static (_, _) => { },
            reads: s_resourceB,
            writes: s_resourceA));

        graph.AddPass(CustomPass.Create(
            "passB",
            FGP.Custom,
            static (_, _) => { },
            reads: s_resourceA,
            writes: s_resourceB));

        var action = () => graph.Compile();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.GraphCycleDetected);
    }

    [Fact]
    public void Reset_ClearsAllPasses()
    {
        using var graph = new FG(_context);
        graph.AddPass(CustomPass.Create("pass1", FGP.Custom, static (_, _) => { }));
        graph.AddPass(CustomPass.Create("pass2", FGP.Custom, static (_, _) => { }));
        graph.PassCount.Should().Be(2);

        graph.Reset();

        graph.PassCount.Should().Be(0);
    }

    [Fact]
    public void DisabledPass_IsSkipped()
    {
        using var graph = new FG(_context);
        int enabledCount = 0;
        int disabledCount = 0;
        var enabledPass = CustomPass.Create("enabled", FGP.Custom, (gl, ctx) => enabledCount++);
        var disabledPass = CustomPass.Create("disabled", FGP.Custom, (gl, ctx) => disabledCount++);
        disabledPass.IsEnabled = false;

        graph.AddPass(enabledPass);
        graph.AddPass(disabledPass);

        graph.Execute();

        enabledCount.Should().Be(1);
        disabledCount.Should().Be(0);
    }

    [Fact]
    public void PassCount_ReflectsAddedPasses()
    {
        using var graph = new FG(_context);

        graph.PassCount.Should().Be(0);

        graph.AddPass(CustomPass.Create("pass1", FGP.Custom, static (_, _) => { }));
        graph.PassCount.Should().Be(1);

        graph.AddPass(CustomPass.Create("pass2", FGP.Custom, static (_, _) => { }));
        graph.PassCount.Should().Be(2);
    }

    [Fact]
    public void Dispose_PreventsSubsequentOperations()
    {
        var graph = new FG(_context);
        graph.Dispose();

        var action = () => graph.AddPass(CustomPass.Create("pass1", FGP.Custom, static (_, _) => { }));

        action.Should().Throw<ObjectDisposedException>();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
