namespace Axrone.Render.OpenGL.Tests;

using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.FrameGraph.Passes;

// Aliases to avoid ambiguity between namespace and the typestate FrameGraph
// handles: FG is the building handle (AddPass/Reset/Compile), FGC the compiled
// handle returned by Compile() that carries Execute().
using FG = global::Axrone.Render.OpenGL.FrameGraph.FrameGraph<global::Axrone.Render.OpenGL.FrameGraph.BuildingPhase, global::Axrone.Render.OpenGL.FrameGraph.DefaultGraphPolicy>;
using FGC = global::Axrone.Render.OpenGL.FrameGraph.FrameGraph<global::Axrone.Render.OpenGL.FrameGraph.CompiledPhase, global::Axrone.Render.OpenGL.FrameGraph.DefaultGraphPolicy>;
using FGP = global::Axrone.Render.OpenGL.FrameGraph.FramePassKind;

/// <summary>
/// Integration tests for FrameGraph end-to-end workflows including simple and complex
/// pipelines, resource sharing, cycle detection, and reset/rebuild scenarios.
/// </summary>
public sealed class FrameGraphPipelineTests : IDisposable
{
    private static readonly string[] s_depth = new string[] { "depth" };
    private static readonly string[] s_sceneColor = new string[] { "sceneColor" };
    private static readonly string[] s_sceneColorDepth = new string[] { "sceneColor", "sceneDepth" };
    private static readonly string[] s_sceneColorBloom = new string[] { "sceneColor", "bloomResult" };
    private static readonly string[] s_bloomResult = new string[] { "bloomResult" };
    private static readonly string[] s_tonemapped = new string[] { "tonemapped" };
    private static readonly string[] s_finalImage = new string[] { "finalImage" };
    private static readonly string[] s_shadowMap = new string[] { "shadowMap" };
    private static readonly string[] s_sharedTexture = new string[] { "sharedTexture" };
    private static readonly string[] s_data = new string[] { "data" };
    private static readonly string[] s_processed = new string[] { "processed" };
    private static readonly string[] s_resourceA = new string[] { "resourceA" };
    private static readonly string[] s_resourceB = new string[] { "resourceB" };
    private static readonly string[] s_resourceC = new string[] { "resourceC" };
    private static readonly string[] s_x = new string[] { "x" };
    private static readonly string[] s_y = new string[] { "y" };
    private static readonly string[] s_z = new string[] { "z" };

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

        // Opaque writes "depth", Transparent reads "depth" => ordering constraint
        var clearPass = CreateTrackingPass("Clear", FGP.Clear, executionOrder);
        var opaquePass = CreateTrackingPass("Opaque", FGP.Opaque, executionOrder, writes: s_depth);
        var transparentPass = CreateTrackingPass("Transparent", FGP.Transparent, executionOrder, reads: s_depth);

        graph.AddPass(clearPass);
        graph.AddPass(opaquePass);
        graph.AddPass(transparentPass);

        // Act
        graph.Compile().Execute();

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

        // Dependency chain:
        // Scene writes "sceneColor", "sceneDepth"
        // Bloom reads "sceneColor", writes "bloomResult"
        // ToneMap reads "sceneColor" + "bloomResult", writes "tonemapped"
        // FXAA reads "tonemapped", writes "finalImage"
        // Shadow writes "shadowMap" (independent)
        // UI reads "finalImage"
        var scenePass = CreateTrackingPass("Scene", FGP.Opaque, executionOrder, writes: s_sceneColorDepth);
        var bloomPass = CreateTrackingPass("Bloom", FGP.Bloom, executionOrder, reads: s_sceneColor, writes: s_bloomResult);
        var toneMapPass = CreateTrackingPass("ToneMap", FGP.ToneMap, executionOrder, reads: s_sceneColorBloom, writes: s_tonemapped);
        var fxaaPass = CreateTrackingPass("FXAA", FGP.Fxaa, executionOrder, reads: s_tonemapped, writes: s_finalImage);
        var shadowPass = CreateTrackingPass("Shadow", FGP.Shadow, executionOrder, writes: s_shadowMap);
        var uiPass = CreateTrackingPass("UI", FGP.Transparent, executionOrder, reads: s_finalImage);

        graph.AddPass(scenePass);
        graph.AddPass(bloomPass);
        graph.AddPass(toneMapPass);
        graph.AddPass(fxaaPass);
        graph.AddPass(shadowPass);
        graph.AddPass(uiPass);

        // Act
        graph.Compile().Execute();

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

        var writerPass = CreateTrackingPass("Writer", FGP.Custom, executionOrder, writes: s_sharedTexture);
        var readerPass = CreateTrackingPass("Reader", FGP.Custom, executionOrder, reads: s_sharedTexture);

        // Add them in reverse order to verify the graph sorts them correctly
        graph.AddPass(readerPass);
        graph.AddPass(writerPass);

        // Act
        graph.Compile().Execute();

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

        // A writes "x", reads "z"
        // B writes "y", reads "x"
        // C writes "z", reads "y"
        // This creates: A -> B -> C -> A (cycle)
        var passA = CreateTrackingPass("A", FGP.Custom, reads: s_z, writes: s_x);
        var passB = CreateTrackingPass("B", FGP.Custom, reads: s_x, writes: s_y);
        var passC = CreateTrackingPass("C", FGP.Custom, reads: s_y, writes: s_z);

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

        graph.AddPass(CreateTrackingPass("Pass1", FGP.Custom, firstPassExecution));

        // Act: execute first graph
        FGC compiled = graph.Compile();
        compiled.Execute();
        firstPassExecution.Should().HaveCount(1);

        // Reset the graph
        graph.Reset();
        graph.PassCount.Should().Be(0);

        // Create new passes for second execution
        var pass2 = CreateTrackingPass("Pass2", FGP.Custom, secondPassExecution, writes: s_data);
        var pass3 = CreateTrackingPass("Pass3", FGP.Custom, secondPassExecution, reads: s_data);

        graph.AddPass(pass2);
        graph.AddPass(pass3);

        // Execute second graph
        compiled = graph.Compile();
        compiled.Execute();

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

        // Each writes a unique resource, no reads
        var passA = CreateTrackingPass("A", FGP.Custom, executionOrder, writes: s_resourceA);
        var passB = CreateTrackingPass("B", FGP.Custom, executionOrder, writes: s_resourceB);
        var passC = CreateTrackingPass("C", FGP.Custom, executionOrder, writes: s_resourceC);

        graph.AddPass(passA);
        graph.AddPass(passB);
        graph.AddPass(passC);

        // Act
        graph.Compile().Execute();

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

        var pass1 = CreateTrackingPass("Pass1", FGP.Custom, executionOrder, writes: s_data);
        var pass2 = CreateTrackingPass("Pass2", FGP.Custom, executionOrder, reads: s_data, writes: s_processed);
        var pass3 = CreateTrackingPass("Pass3", FGP.Custom, executionOrder, reads: s_processed);

        pass2.IsEnabled = false;

        graph.AddPass(pass1);
        graph.AddPass(pass2);
        graph.AddPass(pass3);

        // Act
        graph.Compile().Execute();

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
    /// Creates a test pass that records its execution in a shared list and
    /// declares the given resource dependencies at construction.
    /// </summary>
    /// <param name="name">The pass name, also used as the log entry.</param>
    /// <param name="kind">The pass kind classification.</param>
    /// <param name="executionLog">The shared execution log, or null to record nothing.</param>
    /// <param name="reads">The declared input resource names.</param>
    /// <param name="writes">The declared output resource names.</param>
    private static RenderPass<CustomPassData> CreateTrackingPass(
        string name,
        FGP kind,
        List<string>? executionLog = null,
        string[]? reads = null,
        string[]? writes = null)
    {
        return CustomPass.Create(name, kind, (gl, ctx) => executionLog?.Add(name), reads: reads, writes: writes);
    }
}
