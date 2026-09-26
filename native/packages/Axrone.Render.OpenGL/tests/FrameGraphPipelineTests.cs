namespace Axrone.Render.OpenGL.Tests;

using Axrone.Render.OpenGL.Context;

// Aliases to avoid ambiguity between namespace and class name.
using FG = global::Axrone.Render.OpenGL.FrameGraph.FrameGraph;
using FGP = global::Axrone.Render.OpenGL.FrameGraph.FramePassKind;
using FGPass = global::Axrone.Render.OpenGL.FrameGraph.RenderPass;
using FGCtx = global::Axrone.Render.OpenGL.FrameGraph.PassExecutionContext;

/// <summary>
/// Integration tests for FrameGraph end-to-end workflows including simple and complex
/// pipelines, resource sharing, cycle detection, and reset/rebuild scenarios.
/// </summary>
public sealed class FrameGraphPipelineTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public FrameGraphPipelineTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    [Fact]
    public void SimplePipeline_ClearOpaqueTransparent_ExecutesInOrder()
    {
        // Arrange
        using var graph = new FG(_context);
        var executionOrder = new List<string>();

        var clearPass = new TrackingPass("Clear", FGP.Clear, executionOrder);
        var opaquePass = new TrackingPass("Opaque", FGP.Opaque, executionOrder);
        var transparentPass = new TrackingPass("Transparent", FGP.Transparent, executionOrder);

        // Opaque writes "depth", Transparent reads "depth" => ordering constraint
        opaquePass.AddWrite("depth");
        transparentPass.AddRead("depth");

        graph.AddPass(clearPass);
        graph.AddPass(opaquePass);
        graph.AddPass(transparentPass);

        // Act
        graph.Execute();

        // Assert: all 3 passes executed
        executionOrder.Should().HaveCount(3);
        executionOrder.Should().ContainInOrder("Clear", "Opaque", "Transparent");
    }

    [Fact]
    public void ComplexPipeline_SixPasses_VerifyExecutionOrder()
    {
        // Arrange: a realistic post-processing pipeline
        // Scene -> Bloom (bright extract + blur) -> ToneMap -> FXAA -> Present
        using var graph = new FG(_context);
        var executionOrder = new List<string>();

        var scenePass = new TrackingPass("Scene", FGP.Opaque, executionOrder);
        var bloomPass = new TrackingPass("Bloom", FGP.Bloom, executionOrder);
        var toneMapPass = new TrackingPass("ToneMap", FGP.ToneMap, executionOrder);
        var fxaaPass = new TrackingPass("FXAA", FGP.Fxaa, executionOrder);
        var shadowPass = new TrackingPass("Shadow", FGP.Shadow, executionOrder);
        var uiPass = new TrackingPass("UI", FGP.Transparent, executionOrder);

        // Dependency chain:
        // Scene writes "sceneColor", "sceneDepth"
        // Bloom reads "sceneColor", writes "bloomResult"
        // ToneMap reads "sceneColor" + "bloomResult", writes "tonemapped"
        // FXAA reads "tonemapped", writes "finalImage"
        // Shadow writes "shadowMap" (independent)
        // UI reads "finalImage"
        scenePass.AddWrite("sceneColor");
        scenePass.AddWrite("sceneDepth");

        bloomPass.AddRead("sceneColor");
        bloomPass.AddWrite("bloomResult");

        toneMapPass.AddRead("sceneColor");
        toneMapPass.AddRead("bloomResult");
        toneMapPass.AddWrite("tonemapped");

        fxaaPass.AddRead("tonemapped");
        fxaaPass.AddWrite("finalImage");

        shadowPass.AddWrite("shadowMap");

        uiPass.AddRead("finalImage");

        graph.AddPass(scenePass);
        graph.AddPass(bloomPass);
        graph.AddPass(toneMapPass);
        graph.AddPass(fxaaPass);
        graph.AddPass(shadowPass);
        graph.AddPass(uiPass);

        // Act
        graph.Execute();

        // Assert: all 6 passes executed
        executionOrder.Should().HaveCount(6);

        // Verify dependency ordering
        executionOrder.IndexOf("Scene").Should().BeLessThan(executionOrder.IndexOf("Bloom"),
            "Scene must execute before Bloom");
        executionOrder.IndexOf("Bloom").Should().BeLessThan(executionOrder.IndexOf("ToneMap"),
            "Bloom must execute before ToneMap");
        executionOrder.IndexOf("ToneMap").Should().BeLessThan(executionOrder.IndexOf("FXAA"),
            "ToneMap must execute before FXAA");
        executionOrder.IndexOf("FXAA").Should().BeLessThan(executionOrder.IndexOf("UI"),
            "FXAA must execute before UI");
    }

    [Fact]
    public void ResourceSharing_WriterExecutesBeforeReader()
    {
        // Arrange: two passes share a texture (one writes, one reads)
        using var graph = new FG(_context);
        var executionOrder = new List<string>();

        var writerPass = new TrackingPass("Writer", FGP.Custom, executionOrder);
        var readerPass = new TrackingPass("Reader", FGP.Custom, executionOrder);

        writerPass.AddWrite("sharedTexture");
        readerPass.AddRead("sharedTexture");

        // Add them in reverse order to verify the graph sorts them correctly
        graph.AddPass(readerPass);
        graph.AddPass(writerPass);

        // Act
        graph.Execute();

        // Assert: writer executed before reader despite being added second
        executionOrder.Should().HaveCount(2);
        executionOrder.IndexOf("Writer").Should().BeLessThan(executionOrder.IndexOf("Reader"),
            "Writer must execute before Reader due to shared resource dependency");
    }

    [Fact]
    public void CycleDetection_CircularDependencies_ThrowsGraphCycleDetected()
    {
        // Arrange: create circular dependency A -> B -> C -> A
        using var graph = new FG(_context);

        var passA = new DepPass("A");
        var passB = new DepPass("B");
        var passC = new DepPass("C");

        // A writes "x", reads "z"
        // B writes "y", reads "x"
        // C writes "z", reads "y"
        // This creates: A -> B -> C -> A (cycle)
        passA.AddWrite("x");
        passA.AddRead("z");

        passB.AddWrite("y");
        passB.AddRead("x");

        passC.AddWrite("z");
        passC.AddRead("y");

        graph.AddPass(passA);
        graph.AddPass(passB);
        graph.AddPass(passC);

        // Act & Assert
        var action = () => graph.Compile();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.GraphCycleDetected);
    }

    [Fact]
    public void ResetAndRebuild_ExecuteResetNewGraph_ExecuteAgain()
    {
        // Arrange: first graph
        using var graph = new FG(_context);
        var firstPassExecution = new List<string>();
        var secondPassExecution = new List<string>();

        var pass1 = new TrackingPass("Pass1", FGP.Custom, firstPassExecution);
        graph.AddPass(pass1);

        // Act: execute first graph
        graph.Execute();
        firstPassExecution.Should().HaveCount(1);

        // Reset the graph
        graph.Reset();
        graph.PassCount.Should().Be(0);

        // Create new passes for second execution
        var pass2 = new TrackingPass("Pass2", FGP.Custom, secondPassExecution);
        var pass3 = new TrackingPass("Pass3", FGP.Custom, secondPassExecution);
        pass2.AddWrite("data");
        pass3.AddRead("data");

        graph.AddPass(pass2);
        graph.AddPass(pass3);

        // Execute second graph
        graph.Execute();

        // Assert: second graph executed correctly
        secondPassExecution.Should().HaveCount(2);
        secondPassExecution.IndexOf("Pass2").Should().BeLessThan(secondPassExecution.IndexOf("Pass3"));

        // First pass should not have been re-executed
        firstPassExecution.Should().HaveCount(1);
    }

    [Fact]
    public void IndependentPasses_AllExecuteWithoutOrderingConstraint()
    {
        // Arrange: passes with no shared resources can execute in any order
        using var graph = new FG(_context);
        var executionOrder = new List<string>();

        var passA = new TrackingPass("A", FGP.Custom, executionOrder);
        var passB = new TrackingPass("B", FGP.Custom, executionOrder);
        var passC = new TrackingPass("C", FGP.Custom, executionOrder);

        // Each writes a unique resource, no reads
        passA.AddWrite("resourceA");
        passB.AddWrite("resourceB");
        passC.AddWrite("resourceC");

        graph.AddPass(passA);
        graph.AddPass(passB);
        graph.AddPass(passC);

        // Act
        graph.Execute();

        // Assert: all executed, no particular order required
        executionOrder.Should().HaveCount(3);
        executionOrder.Should().Contain("A");
        executionOrder.Should().Contain("B");
        executionOrder.Should().Contain("C");
    }

    [Fact]
    public void DisabledPass_NotIncludedInTopologicalSort()
    {
        // Arrange: middle pass disabled
        using var graph = new FG(_context);
        var executionOrder = new List<string>();

        var pass1 = new TrackingPass("Pass1", FGP.Custom, executionOrder);
        var pass2 = new TrackingPass("Pass2", FGP.Custom, executionOrder);
        var pass3 = new TrackingPass("Pass3", FGP.Custom, executionOrder);

        pass1.AddWrite("data");
        pass2.AddRead("data");
        pass2.AddWrite("processed");
        pass3.AddRead("processed");

        pass2.IsEnabled = false;

        graph.AddPass(pass1);
        graph.AddPass(pass2);
        graph.AddPass(pass3);

        // Act
        graph.Execute();

        // Assert: disabled pass skipped
        executionOrder.Should().HaveCount(2);
        executionOrder.Should().Contain("Pass1");
        executionOrder.Should().Contain("Pass3");
        executionOrder.Should().NotContain("Pass2");
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    // ========================================================================
    // Helper pass implementations
    // ========================================================================

    /// <summary>
    /// Test pass that records its execution in a shared list.
    /// </summary>
    private sealed class TrackingPass : FGPass
    {
        private readonly List<string> _executionLog;

        public TrackingPass(string name, FGP kind, List<string> executionLog)
            : base(name, kind)
        {
            _executionLog = executionLog;
        }

        public void AddRead(string resource) => Reads(resource);
        public void AddWrite(string resource) => Writes(resource);

        public override void Execute(GLContext context, FGCtx ctx)
        {
            _executionLog.Add(Name);
        }
    }

    /// <summary>
    /// Test pass with configurable dependencies for cycle detection tests.
    /// </summary>
    private sealed class DepPass : FGPass
    {
        public DepPass(string name) : base(name, FGP.Custom)
        {
        }

        public void AddRead(string resource) => Reads(resource);
        public void AddWrite(string resource) => Writes(resource);

        public override void Execute(GLContext context, FGCtx ctx)
        {
        }
    }
}
