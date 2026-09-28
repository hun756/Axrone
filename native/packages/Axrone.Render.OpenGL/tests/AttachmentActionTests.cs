using Axrone.Render.Core.Abstractions;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;

// Aliases to avoid ambiguity between namespace Axrone.Render.OpenGL.FrameGraph
// and class FrameGraph within that namespace.
using FG = global::Axrone.Render.OpenGL.FrameGraph.FrameGraph;
using FGP = global::Axrone.Render.OpenGL.FrameGraph.FramePassKind;

namespace Axrone.Render.OpenGL.Tests;

/// <summary>
/// Tests for the opt-in attachment load/store metadata on <see cref="IRenderPass"/>:
/// defaults, protected declaration flow-through, and the single Compile() validation
/// rule for a DontCare load with no writes. Scheduling/execution must be unaffected.
/// </summary>
public sealed class AttachmentActionTests : IDisposable
{
    private static readonly string[] s_color = new string[] { "color" };
    private static readonly string[] s_resourceA = new string[] { "a" };
    private static readonly string[] s_resourceB = new string[] { "b" };
    private static readonly string[] s_resourceC = new string[] { "c" };

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
        var pass = CreateStubPass("default");

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
        var pass = CreateStubPass("load", loadAction: action);

        pass.LoadAction.Should().Be(action);
        pass.StoreAction.Should().Be(AttachmentStoreAction.Store);
    }

    [Theory]
    [InlineData(AttachmentStoreAction.Store)]
    [InlineData(AttachmentStoreAction.Discard)]
    [InlineData(AttachmentStoreAction.Resolve)]
    public void DeclaresStoreAction_FlowsThroughToProperty(AttachmentStoreAction action)
    {
        var pass = CreateStubPass("store", storeAction: action);

        pass.StoreAction.Should().Be(action);
        pass.LoadAction.Should().Be(AttachmentLoadAction.Load);
    }

    [Fact]
    public void BothActions_AreIndependentlyDeclarable()
    {
        var pass = CreateStubPass(
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
        graph.AddPass(CreateStubPass("noWrites", loadAction: AttachmentLoadAction.DontCare));

        Action action = () => graph.Compile();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration)
            .WithMessage("*noWrites*");
    }

    [Fact]
    public void Execute_DontCareWithNoWrites_ThrowsInvalidPassConfiguration()
    {
        using var graph = new FG(_context);
        int executionCount = 0;
        graph.AddPass(CreateStubPass(
            "noWritesExec",
            loadAction: AttachmentLoadAction.DontCare,
            onExecute: () => executionCount++));

        Action action = () => graph.Execute();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration)
            .WithMessage("*noWritesExec*");
        executionCount.Should().Be(0);
    }

    [Theory]
    [InlineData(AttachmentLoadAction.Load)]
    [InlineData(AttachmentLoadAction.Clear)]
    public void Compile_WithoutWrites_NonDontCareLoad_StillSucceeds(AttachmentLoadAction action)
    {
        using var graph = new FG(_context);
        graph.AddPass(CreateStubPass("noWritesOk", loadAction: action));

        Action compile = () => graph.Compile();

        compile.Should().NotThrow();
    }

    [Fact]
    public void Compile_DisabledDontCareWithoutWrites_IsNotValidated()
    {
        using var graph = new FG(_context);
        var pass = CreateStubPass("disabled", loadAction: AttachmentLoadAction.DontCare);
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
        int executionCount = 0;
        var pass = CreateStubPass(
            "writes",
            loadAction: AttachmentLoadAction.DontCare,
            storeAction: AttachmentStoreAction.Discard,
            writes: s_color,
            onExecute: () => executionCount++);
        graph.AddPass(pass);

        Action compile = () => graph.Compile();
        compile.Should().NotThrow();

        Action execute = () => graph.Execute();
        execute.Should().NotThrow();

        executionCount.Should().Be(1);
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
    /// Payload of the stub pass. The pass state is carried by the captured
    /// callbacks, so the payload itself holds no data.
    /// </summary>
    private struct StubPassData
    {
    }

    /// <summary>
    /// Creates a minimal pass that optionally declares attachment metadata and
    /// dependencies, invoking <paramref name="onExecute"/> on the direct leg.
    /// </summary>
    private static RenderPass<StubPassData> CreateStubPass(
        string name,
        AttachmentLoadAction? loadAction = null,
        AttachmentStoreAction? storeAction = null,
        string[]? reads = null,
        string[]? writes = null,
        Action? onExecute = null)
    {
        return new RenderPass<StubPassData>(
            name,
            FramePassKind.Custom,
            (IRenderPassBuilder builder, ref StubPassData data) =>
            {
                if (loadAction.HasValue)
                {
                    builder.SetLoadAction(loadAction.Value);
                }

                if (storeAction.HasValue)
                {
                    builder.SetStoreAction(storeAction.Value);
                }

                if (reads is not null)
                {
                    for (int i = 0; i < reads.Length; i++)
                    {
                        builder.Reads(reads[i]);
                    }
                }

                if (writes is not null)
                {
                    for (int i = 0; i < writes.Length; i++)
                    {
                        builder.Writes(writes[i]);
                    }
                }
            },
            (in StubPassData data, IRenderContext context, PassExecutionContext ctx) => onExecute?.Invoke());
    }

    /// <summary>
    /// Builds and executes a 3-pass dependency chain, optionally declaring
    /// attachment metadata on each pass. Execution order is logged in order.
    /// </summary>
    private void RunChainedGraph(List<int> order, bool withMetadata)
    {
        using var graph = new FG(_context);

        AttachmentLoadAction load = withMetadata ? AttachmentLoadAction.DontCare : AttachmentLoadAction.Load;
        AttachmentStoreAction store = withMetadata ? AttachmentStoreAction.Discard : AttachmentStoreAction.Store;

        int idA = 0;
        int idB = 1;
        int idC = 2;

        // Every pass must write at least one resource: the DontCare variant declares
        // AttachmentLoadAction.DontCare on all three passes, which is only valid when
        // the pass actually writes the attachment.
        var passA = CreateStubPass("a", loadAction: load, storeAction: store, writes: s_resourceA, onExecute: () => order.Add(idA));
        var passB = CreateStubPass("b", loadAction: load, storeAction: store, reads: s_resourceA, writes: s_resourceB, onExecute: () => order.Add(idB));
        var passC = CreateStubPass("c", loadAction: load, storeAction: store, reads: s_resourceB, writes: s_resourceC, onExecute: () => order.Add(idC));

        graph.AddPass(passA);
        graph.AddPass(passB);
        graph.AddPass(passC);
        graph.Execute();
    }
}
