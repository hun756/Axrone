#pragma warning disable CA1062 // Phase parameters are supplied by the frame graph, which null-checks before dispatch.

using Axrone.Render.Core.Abstractions;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.FrameGraph.Passes;

// Alias to avoid ambiguity between namespace Axrone.Render.OpenGL.FrameGraph
// and the typestate FrameGraph handle within that namespace: FG is the building
// handle (AddPass/Reset/Compile), and Compile() hands out the compiled handle
// that carries Execute(). RenderContext is readable on the building handle.
using FG = global::Axrone.Render.OpenGL.FrameGraph.FrameGraph<global::Axrone.Render.OpenGL.FrameGraph.BuildingPhase, global::Axrone.Render.OpenGL.FrameGraph.DefaultGraphPolicy>;

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

    // ========================================================================
    // Test payloads: each one owns its setup / validate / execute phases
    // ========================================================================

    /// <summary>
    /// Payload of the descriptor probe pass. The setup phase declares the
    /// dependency, descriptor and attachment metadata; the remaining payload
    /// fields prove the pass stores and exposes its data unchanged.
    /// </summary>
    private struct DescriptorPassData
        : IPassSetup<DescriptorPassData>, IPassValidate<DescriptorPassData>, IPassExecute<DescriptorPassData>
    {
        public string ReadName;
        public string WriteName;
        public RenderPassDescriptor Descriptor;
        public AttachmentLoadAction LoadAction;
        public AttachmentStoreAction StoreAction;
        public uint InputTexture;
        public uint OutputFramebuffer;
        public float Intensity;

        public static void Declare(IRenderPassBuilder builder, ref DescriptorPassData data)
        {
            builder.Reads(data.ReadName);
            builder.Writes(data.WriteName);
            builder.SetDescriptor(in data.Descriptor);
            builder.SetLoadAction(data.LoadAction);
            builder.SetStoreAction(data.StoreAction);
        }

        public static void Validate(in DescriptorPassData data)
        {
        }

        public static void Execute(in DescriptorPassData data, IRenderContext context, PassExecutionContext ctx)
        {
        }
    }

    /// <summary>
    /// Payload of the DAG-order passes. The setup phase flips the payload into its
    /// ready state, so the execute phase observing that flag proves the same payload
    /// instance travelled from setup through to execution.
    /// </summary>
    private struct LoggedPassData
        : IPassSetup<LoggedPassData>, IPassValidate<LoggedPassData>, IPassExecute<LoggedPassData>
    {
        public string? ReadName;
        public string? WriteName;
        public string LogEntry;
        public List<string> ExecutionLog;
        public bool Ran;

        public static void Declare(IRenderPassBuilder builder, ref LoggedPassData data)
        {
            if (data.ReadName is not null)
            {
                builder.Reads(data.ReadName);
            }

            if (data.WriteName is not null)
            {
                builder.Writes(data.WriteName);
            }

            data.Ran = true;
        }

        public static void Validate(in LoggedPassData data)
        {
        }

        public static void Execute(in LoggedPassData data, IRenderContext context, PassExecutionContext ctx)
        {
            if (data.Ran)
            {
                data.ExecutionLog.Add(data.LogEntry);
            }
        }
    }

    /// <summary>
    /// Payload of the context probes. It records the context it was executed with
    /// into a caller-owned slot, so both probes can be compared against the same
    /// graph-owned instance.
    /// </summary>
    private struct ContextProbeData
        : IPassSetup<ContextProbeData>, IPassValidate<ContextProbeData>, IPassExecute<ContextProbeData>
    {
        public string? ReadName;
        public string? WriteName;
        public IRenderContext?[] Slot;

        public static void Declare(IRenderPassBuilder builder, ref ContextProbeData data)
        {
            if (data.ReadName is not null)
            {
                builder.Reads(data.ReadName);
            }

            if (data.WriteName is not null)
            {
                builder.Writes(data.WriteName);
            }
        }

        public static void Validate(in ContextProbeData data)
        {
        }

        public static void Execute(in ContextProbeData data, IRenderContext context, PassExecutionContext ctx)
        {
            data.Slot[0] = context;
        }
    }

    // ========================================================================
    // Setup phase
    // ========================================================================

    [Fact]
    public void GenericPass_SetupPhase_CapturesDescriptorAndDependencies()
    {
        var vp = new ViewportRect(0, 0, 1920, 1080);
        var desc = RenderPassDescriptor.CreateDefault(vp, new ClearColorValue(0.1f, 0.2f, 0.3f, 1f));

        var pass = new RenderPass<DescriptorPassData>(
            "BloomExtract",
            FramePassKind.PostProcess,
            new DescriptorPassData
            {
                ReadName = "HDRSceneColor",
                WriteName = "BloomMip0",
                Descriptor = desc,
                LoadAction = AttachmentLoadAction.Clear,
                StoreAction = AttachmentStoreAction.Store,
                InputTexture = 42,
                OutputFramebuffer = 100,
                Intensity = 1.5f
            });

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

    // ========================================================================
    // Execution routing
    // ========================================================================

    [Fact]
    public void FrameGraph_AddPassGeneric_CompilesAndExecutesInDagOrder()
    {
        using var fg = new FG(_context);

        var executionOrder = new List<string>();

        // Intentionally add dependent pass first: Composite reads "AlbedoTexture" written by Geometry
        fg.AddPass<LoggedPassData, DefaultGraphPolicy>(
            "CompositePass",
            FramePassKind.PostProcess,
            new LoggedPassData
            {
                ReadName = "AlbedoTexture",
                WriteName = "FinalOutput",
                LogEntry = "CompositePass",
                ExecutionLog = executionOrder
            });

        fg.AddPass<LoggedPassData, DefaultGraphPolicy>(
            "GeometryPass",
            FramePassKind.Opaque,
            new LoggedPassData
            {
                WriteName = "AlbedoTexture",
                LogEntry = "GeometryPass",
                ExecutionLog = executionOrder
            });

        fg.Compile().Execute();

        // Topological order must place GeometryPass before CompositePass. Both
        // entries exist only if each pass saw the payload its setup phase wrote.
        executionOrder.Should().HaveCount(2);
        executionOrder.Should().ContainInOrder("GeometryPass", "CompositePass");
    }

    [Fact]
    public void FrameGraph_Execute_RoutesCustomPassThroughOwnedRenderContext()
    {
        using var fg = new FG(_context);

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

        fg.Compile().Execute();

        receivedContext.Should().BeSameAs(fg.RenderContext.GLContext);
        receivedPassContext.Should().BeSameAs(fg.RenderContext.PassContext);
    }

    [Fact]
    public void FrameGraph_Execute_GenericPassesShareGraphOwnedRenderContext()
    {
        using var fg = new FG(_context);

        IRenderContext?[] firstSlot = new IRenderContext?[1];
        IRenderContext?[] secondSlot = new IRenderContext?[1];

        fg.AddPass<ContextProbeData, DefaultGraphPolicy>(
            "ProbeA",
            FramePassKind.Custom,
            new ContextProbeData
            {
                WriteName = "ProbeResource",
                Slot = firstSlot
            });

        fg.AddPass<ContextProbeData, DefaultGraphPolicy>(
            "ProbeB",
            FramePassKind.Custom,
            new ContextProbeData
            {
                ReadName = "ProbeResource",
                Slot = secondSlot
            });

        fg.Compile().Execute();

        firstSlot[0].Should().BeSameAs(fg.RenderContext);
        secondSlot[0].Should().BeSameAs(fg.RenderContext);
    }

    [Fact]
    public void RenderPass_Execute_MissingPassContext_FailsClosed()
    {
        var pass = new RenderPass<ContextProbeData>(
            "NativeOnly",
            FramePassKind.Custom,
            new ContextProbeData { Slot = new IRenderContext?[1] });

        // A GLRenderContext without a PassExecutionContext cannot satisfy the pass
        // contract, so execution must fail closed instead of running the phase.
        var renderCtx = new GLRenderContext(_context, passContext: null);

        var action = () => ((IRenderPass)pass).Execute(renderCtx);

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidOperation);
    }
}
