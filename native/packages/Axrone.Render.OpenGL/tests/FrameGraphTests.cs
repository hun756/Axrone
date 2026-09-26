using Axrone.Render.OpenGL.Context;

namespace Axrone.Render.OpenGL.Tests;

// Alias to avoid ambiguity between namespace Axrone.Render.OpenGL.FrameGraph
// and class FrameGraph within that namespace.
using FG = global::Axrone.Render.OpenGL.FrameGraph.FrameGraph;
using FGP = global::Axrone.Render.OpenGL.FrameGraph.FramePassKind;
using FGPass = global::Axrone.Render.OpenGL.FrameGraph.RenderPass;
using FGCtx = global::Axrone.Render.OpenGL.FrameGraph.PassExecutionContext;

/// <summary>
/// Tests for FrameGraph pass scheduling, topological ordering,
/// cycle detection, and PassExecutionContext resource management.
/// </summary>
public sealed class FrameGraphTests : IDisposable
{
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
        var pass = new TestRenderPass("pass1", FGP.Custom);
        graph.AddPass(pass);

        graph.Execute();

        pass.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public void MultiplePasses_ExecuteInTopologicalOrder()
    {
        using var graph = new FG(_context);
        var executionOrder = new List<int>();

        var pass0 = new OrderTrackingPass("pass0", 0, executionOrder);
        var pass1 = new OrderTrackingPass("pass1", 1, executionOrder);
        var pass2 = new OrderTrackingPass("pass2", 2, executionOrder);

        // pass1 writes "scene", pass2 reads "scene" => pass1 must come before pass2
        pass1.AddWrite("scene");
        pass2.AddRead("scene");

        graph.AddPass(pass0);
        graph.AddPass(pass1);
        graph.AddPass(pass2);

        graph.Execute();

        executionOrder.Should().HaveCount(3);
        // pass1 must come before pass2
        executionOrder.IndexOf(1).Should().BeLessThan(executionOrder.IndexOf(2));
    }

    [Fact]
    public void CycleDetection_ThrowsGraphCycleDetected()
    {
        using var graph = new FG(_context);

        var passA = new TestRenderPassWithDeps("passA");
        passA.AddWrite("a");
        passA.AddRead("b");

        var passB = new TestRenderPassWithDeps("passB");
        passB.AddWrite("b");
        passB.AddRead("a");

        graph.AddPass(passA);
        graph.AddPass(passB);

        var action = () => graph.Compile();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.GraphCycleDetected);
    }

    [Fact]
    public void Reset_ClearsAllPasses()
    {
        using var graph = new FG(_context);
        graph.AddPass(new TestRenderPass("pass1", FGP.Custom));
        graph.AddPass(new TestRenderPass("pass2", FGP.Custom));
        graph.PassCount.Should().Be(2);

        graph.Reset();

        graph.PassCount.Should().Be(0);
    }

    [Fact]
    public void DisabledPass_IsSkipped()
    {
        using var graph = new FG(_context);
        var enabledPass = new TestRenderPass("enabled", FGP.Custom);
        var disabledPass = new TestRenderPass("disabled", FGP.Custom);
        disabledPass.IsEnabled = false;

        graph.AddPass(enabledPass);
        graph.AddPass(disabledPass);

        graph.Execute();

        enabledPass.ExecutionCount.Should().Be(1);
        disabledPass.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public void PassCount_ReflectsAddedPasses()
    {
        using var graph = new FG(_context);

        graph.PassCount.Should().Be(0);

        graph.AddPass(new TestRenderPass("pass1", FGP.Custom));
        graph.PassCount.Should().Be(1);

        graph.AddPass(new TestRenderPass("pass2", FGP.Custom));
        graph.PassCount.Should().Be(2);
    }

    [Fact]
    public void Dispose_PreventsSubsequentOperations()
    {
        var graph = new FG(_context);
        graph.Dispose();

        var action = () => graph.AddPass(new TestRenderPass("pass1", FGP.Custom));

        action.Should().Throw<ObjectDisposedException>();
    }

    // ========================================================================
    // Helper pass implementations
    // ========================================================================

    /// <summary>
    /// Simple test pass that counts executions.
    /// </summary>
    private sealed class TestRenderPass : FGPass
    {
        public int ExecutionCount { get; private set; }

        public TestRenderPass(string name, FGP kind) : base(name, kind)
        {
        }

        public override void Execute(GLContext context, FGCtx ctx)
        {
            ExecutionCount++;
        }
    }

    /// <summary>
    /// Test pass that records its execution order.
    /// </summary>
    private sealed class OrderTrackingPass : FGPass
    {
        private readonly int _order;
        private readonly List<int> _executionOrder;

        public OrderTrackingPass(string name, int order, List<int> executionOrder) : base(name, FGP.Custom)
        {
            _order = order;
            _executionOrder = executionOrder;
        }

        public void AddRead(string resource) => Reads(resource);
        public void AddWrite(string resource) => Writes(resource);

        public override void Execute(GLContext context, FGCtx ctx)
        {
            _executionOrder.Add(_order);
        }
    }

    /// <summary>
    /// Test pass with configurable read/write dependencies for cycle detection tests.
    /// </summary>
    private sealed class TestRenderPassWithDeps : FGPass
    {
        public TestRenderPassWithDeps(string name) : base(name, FGP.Custom)
        {
        }

        public void AddRead(string resource) => Reads(resource);
        public void AddWrite(string resource) => Writes(resource);

        public override void Execute(GLContext context, FGCtx ctx)
        {
        }
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
