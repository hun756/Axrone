using Axrone.Render.Core.Abstractions;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.FrameGraph.Passes;

namespace Axrone.Render.OpenGL.Tests;

public sealed class GenericRenderPassTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public GenericRenderPassTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private struct PostProcessPassData
    {
        public uint InputTexture;
        public uint OutputFramebuffer;
        public float Intensity;
    }

    [Fact]
    public void GenericPass_SetupPhase_CapturesDescriptorAndDependencies()
    {
        var vp = new ViewportRect(0, 0, 1920, 1080);
        var desc = RenderPassDescriptor.CreateDefault(vp, new ClearColorValue(0.1f, 0.2f, 0.3f, 1f));

        var pass = new RenderPass<PostProcessPassData>(
            "BloomExtract",
            FramePassKind.PostProcess,
            (builder, ref data) =>
            {
                builder.Reads("HDRSceneColor");
                builder.Writes("BloomMip0");
                builder.SetDescriptor(desc);
                builder.SetLoadAction(AttachmentLoadAction.Clear);
                builder.SetStoreAction(AttachmentStoreAction.Store);

                data.InputTexture = 42;
                data.OutputFramebuffer = 100;
                data.Intensity = 1.5f;
            },
            (in data, ctx, execCtx) => { });

        pass.Name.Should().Be("BloomExtract");
        pass.Kind.Should().Be(FramePassKind.PostProcess);
        pass.LoadAction.Should().Be(AttachmentLoadAction.Clear);
        pass.StoreAction.Should().Be(AttachmentStoreAction.Store);
        pass.Descriptor.Should().Be(desc);

        pass.GetReadResources().ToArray().Should().ContainSingle().Which.Should().Be("HDRSceneColor");
        pass.GetWrittenResources().ToArray().Should().ContainSingle().Which.Should().Be("BloomMip0");

        pass.Data.InputTexture.Should().Be(42);
        pass.Data.OutputFramebuffer.Should().Be(100);
        pass.Data.Intensity.Should().Be(1.5f);
    }

    [Fact]
    public void GLRenderContext_BeginPass_ExecutesLoadActionClear()
    {
        var glCtx = new GLRenderContext(_context);
        _mock.ClearCallLog();

        var vp = new ViewportRect(0, 0, 800, 600);
        Span<AttachmentDescriptor> atts = stackalloc AttachmentDescriptor[1]
        {
            AttachmentDescriptor.Color(0, AttachmentLoadAction.Clear, AttachmentStoreAction.Store, new ClearColorValue(0.5f, 0.5f, 0.5f, 1.0f))
        };
        var desc = RenderPassDescriptor.Create(15, vp, atts);

        glCtx.BeginPass(desc);

        _mock.CallLog.Should().Contain(c => c.Contains("BindFramebuffer", StringComparison.Ordinal) && c.Contains("15", StringComparison.Ordinal));
        _mock.CallLog.Should().Contain(c => c.Contains("Viewport", StringComparison.Ordinal) && c.Contains("800", StringComparison.Ordinal));
        _mock.CallLog.Should().Contain(c => c.Contains("ClearBufferfv", StringComparison.Ordinal));

        glCtx.EndPass();
    }

    [Fact]
    public void GLRenderContext_BeginPass_IssuesInvalidateHintForDontCareLoad()
    {
        var glCtx = new GLRenderContext(_context);
        _mock.ClearCallLog();

        var vp = new ViewportRect(0, 0, 800, 600);
        Span<AttachmentDescriptor> atts = stackalloc AttachmentDescriptor[1]
        {
            AttachmentDescriptor.Color(0, AttachmentLoadAction.DontCare, AttachmentStoreAction.Store)
        };
        var desc = RenderPassDescriptor.Create(15, vp, atts);

        glCtx.BeginPass(desc);
        glCtx.EndPass();

        _mock.CallLog.Should().Contain(c => c.Contains("InvalidateFramebuffer", StringComparison.Ordinal));
    }

    [Fact]
    public void GLRenderContext_EndPass_ExecutesStoreActionDiscard()
    {
        var glCtx = new GLRenderContext(_context);
        _mock.ClearCallLog();

        var vp = new ViewportRect(0, 0, 800, 600);
        Span<AttachmentDescriptor> atts = stackalloc AttachmentDescriptor[1]
        {
            AttachmentDescriptor.Color(0, AttachmentLoadAction.DontCare, AttachmentStoreAction.Discard)
        };
        var desc = RenderPassDescriptor.Create(15, vp, atts);

        glCtx.BeginPass(desc);
        glCtx.EndPass();

        _mock.CallLog.Should().Contain(c => c.Contains("InvalidateFramebuffer", StringComparison.Ordinal));
    }

    [Fact]
    public void GLRenderContext_Reentrancy_ThrowsInvalidOperation()
    {
        var glCtx = new GLRenderContext(_context);
        var vp = new ViewportRect(0, 0, 800, 600);
        var desc = RenderPassDescriptor.CreateDefault(vp);

        glCtx.BeginPass(desc);

        var action = () => glCtx.BeginPass(desc);
        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidOperation);

        glCtx.EndPass();
    }

    private struct GeometryData
    {
        public bool Ran;
    }

    private struct CompositeData
    {
        public bool Ran;
    }

    [Fact]
    public void FrameGraph_AddPassGeneric_CompilesAndExecutesInDagOrder()
    {
        using var fg = new global::Axrone.Render.OpenGL.FrameGraph.FrameGraph(_context);

        bool geomExecuted = false;
        bool compExecuted = false;
        var executionOrder = new List<string>();

        // Intentionally add dependent pass first: Composite reads "AlbedoTexture" written by Geometry
        fg.AddPass<CompositeData>(
            "CompositePass",
            FramePassKind.PostProcess,
            (builder, ref data) =>
            {
                builder.Reads("AlbedoTexture");
                builder.Writes("FinalOutput");
                data.Ran = true;
            },
            (in data, ctx, exec) =>
            {
                compExecuted = data.Ran;
                executionOrder.Add("CompositePass");
            });

        fg.AddPass<GeometryData>(
            "GeometryPass",
            FramePassKind.Opaque,
            (builder, ref data) =>
            {
                builder.Writes("AlbedoTexture");
                data.Ran = true;
            },
            (in data, ctx, exec) =>
            {
                geomExecuted = data.Ran;
                executionOrder.Add("GeometryPass");
            });

        fg.Execute();

        geomExecuted.Should().BeTrue();
        compExecuted.Should().BeTrue();

        // Topological order must place GeometryPass before CompositePass
        executionOrder.Should().ContainInOrder("GeometryPass", "CompositePass");
    }

    private struct ContextProbeData
    {
    }

    [Fact]
    public void FrameGraph_Execute_RoutesCustomPassThroughOwnedRenderContext()
    {
        using var fg = new global::Axrone.Render.OpenGL.FrameGraph.FrameGraph(_context);

        GLContext? receivedContext = null;
        PassExecutionContext? receivedPassContext = null;

        var pass = CustomPass.Create(
            "LegacyCapture",
            FramePassKind.Custom,
            (gl, ctx) =>
            {
                receivedContext = gl;
                receivedPassContext = ctx;
            });

        fg.AddPass(pass);

        fg.Execute();

        receivedContext.Should().BeSameAs(fg.RenderContext.GLContext);
        receivedPassContext.Should().BeSameAs(fg.RenderContext.PassContext);
    }

    [Fact]
    public void FrameGraph_Execute_GenericPassesShareGraphOwnedRenderContext()
    {
        using var fg = new global::Axrone.Render.OpenGL.FrameGraph.FrameGraph(_context);

        IRenderContext? first = null;
        IRenderContext? second = null;

        fg.AddPass<ContextProbeData>(
            "ProbeA",
            FramePassKind.Custom,
            (builder, ref data) => builder.Writes("ProbeResource"),
            (in data, ctx, exec) => first = ctx);

        fg.AddPass<ContextProbeData>(
            "ProbeB",
            FramePassKind.Custom,
            (builder, ref data) => builder.Reads("ProbeResource"),
            (in data, ctx, exec) => second = ctx);

        fg.Execute();

        first.Should().BeSameAs(fg.RenderContext);
        second.Should().BeSameAs(fg.RenderContext);
    }

    [Fact]
    public void RenderPass_Execute_MissingPassContext_FailsClosed()
    {
        var pass = new RenderPass<ContextProbeData>(
            "NativeOnly",
            FramePassKind.Custom,
            (builder, ref data) => { },
            (in data, ctx, exec) => { });

        // A GLRenderContext without a PassExecutionContext cannot satisfy the pass
        // contract, so execution must fail closed instead of running the delegate.
        var renderCtx = new GLRenderContext(_context, passContext: null);

        var action = () => ((IRenderPass)pass).Execute(renderCtx);

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidOperation);
    }
}
