using Axrone.Render.OpenGL.Context;

// Aliases to avoid ambiguity between namespace Axrone.Render.OpenGL.FrameGraph
// and class FrameGraph within that namespace.
using FG = global::Axrone.Render.OpenGL.FrameGraph.FrameGraph;
using FGP = global::Axrone.Render.OpenGL.FrameGraph.FramePassKind;
using FGPass = global::Axrone.Render.OpenGL.FrameGraph.RenderPass;
using FGCtx = global::Axrone.Render.OpenGL.FrameGraph.PassExecutionContext;

namespace Axrone.Render.OpenGL.Tests;

/// <summary>
/// Tests for the opt-in attachment load/store metadata on <see cref="FGPass"/>:
/// defaults, protected declaration flow-through, and the single Compile() validation
/// rule for a DontCare load with no writes. Scheduling/execution must be unaffected.
/// </summary>
public sealed class AttachmentActionTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public AttachmentActionTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    // ========================================================================
    // Defaults
    // ========================================================================

    [Fact]
    public void StubPass_DefaultsToLoadAndStore()
    {
        var pass = new StubPass("default");

        pass.LoadAction.Should().Be(AttachmentLoadAction.Load);
        pass.StoreAction.Should().Be(AttachmentStoreAction.Store);
    }

    [Fact]
    public void ActionEnums_HaveStableWireValues()
    {
        ((byte)AttachmentLoadAction.Load).Should().Be(0);
        ((byte)AttachmentLoadAction.Clear).Should().Be(1);
        ((byte)AttachmentLoadAction.DontCare).Should().Be(2);

        ((byte)AttachmentStoreAction.Store).Should().Be(0);
        ((byte)AttachmentStoreAction.Discard).Should().Be(1);
        ((byte)AttachmentStoreAction.Resolve).Should().Be(2);
    }

    // ========================================================================
    // Protected declaration flow-through
    // ========================================================================

    [Theory]
    [InlineData(AttachmentLoadAction.Load)]
    [InlineData(AttachmentLoadAction.Clear)]
    [InlineData(AttachmentLoadAction.DontCare)]
    public void DeclaresLoadAction_FlowsThroughToProperty(AttachmentLoadAction action)
    {
        var pass = new StubPass("load", loadAction: action);

        pass.LoadAction.Should().Be(action);
        pass.StoreAction.Should().Be(AttachmentStoreAction.Store);
    }

    [Theory]
    [InlineData(AttachmentStoreAction.Store)]
    [InlineData(AttachmentStoreAction.Discard)]
    [InlineData(AttachmentStoreAction.Resolve)]
    public void DeclaresStoreAction_FlowsThroughToProperty(AttachmentStoreAction action)
    {
        var pass = new StubPass("store", storeAction: action);

        pass.StoreAction.Should().Be(action);
        pass.LoadAction.Should().Be(AttachmentLoadAction.Load);
    }

    [Fact]
    public void BothActions_AreIndependentlyDeclarable()
    {
        var pass = new StubPass(
            "both",
            loadAction: AttachmentLoadAction.Clear,
            storeAction: AttachmentStoreAction.Resolve);

        pass.LoadAction.Should().Be(AttachmentLoadAction.Clear);
        pass.StoreAction.Should().Be(AttachmentStoreAction.Resolve);
    }

    // ========================================================================
    // Compile() validation: DontCare + zero writes is a configuration error
    // ========================================================================

    [Fact]
    public void Compile_DontCareWithNoWrites_ThrowsInvalidPassConfiguration()
    {
        using var graph = new FG(_context);
        graph.AddPass(new StubPass("noWrites", loadAction: AttachmentLoadAction.DontCare));

        Action action = () => graph.Compile();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration)
            .WithMessage("*noWrites*");
    }

    [Fact]
    public void Execute_DontCareWithNoWrites_ThrowsInvalidPassConfiguration()
    {
        using var graph = new FG(_context);
        var pass = new StubPass("noWritesExec", loadAction: AttachmentLoadAction.DontCare);
        graph.AddPass(pass);

        Action action = () => graph.Execute();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration)
            .WithMessage("*noWritesExec*");
        pass.ExecutionCount.Should().Be(0);
    }

    [Theory]
    [InlineData(AttachmentLoadAction.Load)]
    [InlineData(AttachmentLoadAction.Clear)]
    public void Compile_WithoutWrites_NonDontCareLoad_StillSucceeds(AttachmentLoadAction action)
    {
        using var graph = new FG(_context);
        graph.AddPass(new StubPass("noWritesOk", loadAction: action));

        Action compile = () => graph.Compile();

        compile.Should().NotThrow();
    }

    [Fact]
    public void Compile_DisabledDontCareWithoutWrites_IsNotValidated()
    {
        using var graph = new FG(_context);
        var pass = new StubPass("disabled", loadAction: AttachmentLoadAction.DontCare);
        pass.IsEnabled = false;
        graph.AddPass(pass);

        Action compile = () => graph.Compile();

        compile.Should().NotThrow();
    }

    // ========================================================================
    // Zero behavior change: metadata never affects ordering or execution
    // ========================================================================

    [Fact]
    public void Compile_DontCareWithWrites_CompilesAndExecutes()
    {
        using var graph = new FG(_context);
        var pass = new StubPass(
            "writes",
            loadAction: AttachmentLoadAction.DontCare,
            storeAction: AttachmentStoreAction.Discard);
        pass.AddWrite("color");
        graph.AddPass(pass);

        Action compile = () => graph.Compile();
        compile.Should().NotThrow();

        Action execute = () => graph.Execute();
        execute.Should().NotThrow();

        pass.ExecutionCount.Should().Be(1);
        pass.LoadAction.Should().Be(AttachmentLoadAction.DontCare);
        pass.StoreAction.Should().Be(AttachmentStoreAction.Discard);
    }

    [Fact]
    public void AttachmentMetadata_DoesNotAlterSortOrder()
    {
        var baselineOrder = new List<int>();
        var metadataOrder = new List<int>();

        RunChainedGraph(baselineOrder, withMetadata: false);
        RunChainedGraph(metadataOrder, withMetadata: true);

        metadataOrder.Should().HaveCount(3);
        metadataOrder.Should().Equal(baselineOrder);
        metadataOrder.Should().Equal(0, 1, 2);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.KeepAlive(_mock);
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    /// <summary>
    /// Builds and executes a 3-pass dependency chain, optionally declaring
    /// attachment metadata on each pass. Execution order is logged in order.
    /// </summary>
    private void RunChainedGraph(List<int> order, bool withMetadata)
    {
        using var graph = new FG(_context);

        AttachmentLoadAction load = withMetadata ? AttachmentLoadAction.DontCare : AttachmentLoadAction.Load;
        AttachmentStoreAction store = withMetadata ? AttachmentStoreAction.Discard : AttachmentStoreAction.Store;

        var passA = new StubPass("a", loadAction: load, storeAction: store, order: order, id: 0);
        var passB = new StubPass("b", loadAction: load, storeAction: store, order: order, id: 1);
        var passC = new StubPass("c", loadAction: load, storeAction: store, order: order, id: 2);

        passA.AddWrite("a");
        passB.AddRead("a");
        passB.AddWrite("b");
        passC.AddRead("b");

        // Every pass must write at least one resource: the DontCare variant declares
        // AttachmentLoadAction.DontCare on all three passes, which is only valid when
        // the pass actually writes the attachment.
        passC.AddWrite("c");

        graph.AddPass(passA);
        graph.AddPass(passB);
        graph.AddPass(passC);
        graph.Execute();
    }

    /// <summary>
    /// Minimal pass that optionally declares attachment metadata and dependencies.
    /// </summary>
    private sealed class StubPass : FGPass
    {
        private readonly List<int>? _order;
        private readonly int _id;

        public StubPass(
            string name,
            AttachmentLoadAction? loadAction = null,
            AttachmentStoreAction? storeAction = null,
            List<int>? order = null,
            int id = 0)
            : base(name, FGP.Custom)
        {
            _order = order;
            _id = id;

            if (loadAction.HasValue)
            {
                DeclaresLoadAction(loadAction.Value);
            }

            if (storeAction.HasValue)
            {
                DeclaresStoreAction(storeAction.Value);
            }
        }

        public int ExecutionCount { get; private set; }

        public void AddRead(string resource) => Reads(resource);

        public void AddWrite(string resource) => Writes(resource);

        public override void Execute(GLContext context, FGCtx ctx)
        {
            ExecutionCount++;
            _order?.Add(_id);
        }
    }
}
