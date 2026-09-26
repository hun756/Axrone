using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.FrameGraph.PassExecutors;
using Axrone.Render.OpenGL.Shading;

namespace Axrone.Render.OpenGL.Tests;

/// <summary>
/// Tests for all 12 pass executors in the frame graph system.
/// Covers constructor initialization (Name, Kind), fluent configuration,
/// and Validate() pre-condition checks.
/// </summary>
public sealed class PassExecutorTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public PassExecutorTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    // =========================================================================
    // ClearPassExecutor Tests
    // =========================================================================

    [Fact]
    public void ClearPassExecutor_Constructor_SetsCorrectNameAndKind()
    {
        var pass = new ClearPassExecutor("clear", clearColor: new Vector4(0, 0, 0, 1));

        pass.Name.Should().Be("clear");
        pass.Kind.Should().Be(FramePassKind.Clear);
    }

    [Fact]
    public void ClearPassExecutor_Validate_ThrowsWhenNoBuffersToClear()
    {
        var pass = new ClearPassExecutor(
            "clear",
            clearColorEnabled: false,
            clearDepthEnabled: false,
            clearStencilEnabled: false);

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);
    }

    [Fact]
    public void ClearPassExecutor_Validate_DoesNotThrowWhenClearColorEnabled()
    {
        var pass = new ClearPassExecutor("clear", clearColorEnabled: true);

        var action = () => pass.Validate();

        action.Should().NotThrow();
    }

    [Fact]
    public void ClearPassExecutor_IsEnabled_DefaultsToTrue()
    {
        var pass = new ClearPassExecutor("clear");

        pass.IsEnabled.Should().BeTrue();
    }

    // =========================================================================
    // OpaquePassExecutor Tests
    // =========================================================================

    [Fact]
    public void OpaquePassExecutor_Constructor_SetsCorrectNameAndKind()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new OpaquePassExecutor("opaque", program);

        pass.Name.Should().Be("opaque");
        pass.Kind.Should().Be(FramePassKind.Opaque);

        program.Dispose();
    }

    [Fact]
    public void OpaquePassExecutor_Validate_ThrowsWhenProgramDisposed()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new OpaquePassExecutor("opaque", program);
        program.Dispose();

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);
    }

    [Fact]
    public void OpaquePassExecutor_Validate_DoesNotThrowWithValidProgram()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new OpaquePassExecutor("opaque", program);

        var action = () => pass.Validate();

        action.Should().NotThrow();

        program.Dispose();
    }

    // =========================================================================
    // TransparentPassExecutor Tests
    // =========================================================================

    [Fact]
    public void TransparentPassExecutor_Constructor_SetsCorrectNameAndKind()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new TransparentPassExecutor("transparent", program);

        pass.Name.Should().Be("transparent");
        pass.Kind.Should().Be(FramePassKind.Transparent);

        program.Dispose();
    }

    [Fact]
    public void TransparentPassExecutor_Validate_ThrowsWhenProgramDisposed()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new TransparentPassExecutor("transparent", program);
        program.Dispose();

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);
    }

    [Fact]
    public void TransparentPassExecutor_Validate_DoesNotThrowWithValidProgram()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new TransparentPassExecutor("transparent", program);

        var action = () => pass.Validate();

        action.Should().NotThrow();

        program.Dispose();
    }

    // =========================================================================
    // BloomPassExecutor Tests
    // =========================================================================

    [Fact]
    public void BloomPassExecutor_Constructor_SetsCorrectNameAndKind()
    {
        var brightPass = new GLProgram(_context, "vs", "fs");
        var blur = new GLProgram(_context, "vs", "fs");
        var composite = new GLProgram(_context, "vs", "fs");
        var pass = new BloomPassExecutor("bloom", brightPass, blur, composite);

        pass.Name.Should().Be("bloom");
        pass.Kind.Should().Be(FramePassKind.Bloom);

        brightPass.Dispose();
        blur.Dispose();
        composite.Dispose();
    }

    [Fact]
    public void BloomPassExecutor_WithThreshold_SetsThreshold()
    {
        var brightPass = new GLProgram(_context, "vs", "fs");
        var blur = new GLProgram(_context, "vs", "fs");
        var composite = new GLProgram(_context, "vs", "fs");
        var pass = new BloomPassExecutor("bloom", brightPass, blur, composite);

        var result = pass.WithThreshold(1.5f);

        pass.Threshold.Should().Be(1.5f);
        result.Should().BeSameAs(pass, "fluent API should return same instance");

        brightPass.Dispose();
        blur.Dispose();
        composite.Dispose();
    }

    [Fact]
    public void BloomPassExecutor_WithBlurIterations_SetsIterations()
    {
        var brightPass = new GLProgram(_context, "vs", "fs");
        var blur = new GLProgram(_context, "vs", "fs");
        var composite = new GLProgram(_context, "vs", "fs");
        var pass = new BloomPassExecutor("bloom", brightPass, blur, composite);

        var result = pass.WithBlurIterations(3);

        pass.BlurIterations.Should().Be(3);
        result.Should().BeSameAs(pass);

        brightPass.Dispose();
        blur.Dispose();
        composite.Dispose();
    }

    [Fact]
    public void BloomPassExecutor_WithBloomIntensity_SetsIntensity()
    {
        var brightPass = new GLProgram(_context, "vs", "fs");
        var blur = new GLProgram(_context, "vs", "fs");
        var composite = new GLProgram(_context, "vs", "fs");
        var pass = new BloomPassExecutor("bloom", brightPass, blur, composite);

        pass.WithBloomIntensity(0.75f);

        pass.BloomIntensity.Should().Be(0.75f);

        brightPass.Dispose();
        blur.Dispose();
        composite.Dispose();
    }

    [Fact]
    public void BloomPassExecutor_Validate_ThrowsWhenThresholdNegative()
    {
        var brightPass = new GLProgram(_context, "vs", "fs");
        var blur = new GLProgram(_context, "vs", "fs");
        var composite = new GLProgram(_context, "vs", "fs");
        var pass = new BloomPassExecutor("bloom", brightPass, blur, composite);
        pass.WithThreshold(-1.0f);

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        brightPass.Dispose();
        blur.Dispose();
        composite.Dispose();
    }

    [Fact]
    public void BloomPassExecutor_Validate_ThrowsWhenBlurIterationsLessThanOne()
    {
        var brightPass = new GLProgram(_context, "vs", "fs");
        var blur = new GLProgram(_context, "vs", "fs");
        var composite = new GLProgram(_context, "vs", "fs");
        var pass = new BloomPassExecutor("bloom", brightPass, blur, composite);
        pass.WithBlurIterations(0);

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        brightPass.Dispose();
        blur.Dispose();
        composite.Dispose();
    }

    [Fact]
    public void BloomPassExecutor_Validate_ThrowsWhenBloomIntensityNegative()
    {
        var brightPass = new GLProgram(_context, "vs", "fs");
        var blur = new GLProgram(_context, "vs", "fs");
        var composite = new GLProgram(_context, "vs", "fs");
        var pass = new BloomPassExecutor("bloom", brightPass, blur, composite);
        pass.WithBloomIntensity(-0.5f);

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        brightPass.Dispose();
        blur.Dispose();
        composite.Dispose();
    }

    [Fact]
    public void BloomPassExecutor_Validate_DoesNotThrowWithValidConfig()
    {
        var brightPass = new GLProgram(_context, "vs", "fs");
        var blur = new GLProgram(_context, "vs", "fs");
        var composite = new GLProgram(_context, "vs", "fs");
        var pass = new BloomPassExecutor("bloom", brightPass, blur, composite);
        pass.WithThreshold(1.0f).WithBlurIterations(5).WithBloomIntensity(0.5f);

        var action = () => pass.Validate();

        action.Should().NotThrow();

        brightPass.Dispose();
        blur.Dispose();
        composite.Dispose();
    }

    // =========================================================================
    // ToneMapPassExecutor Tests
    // =========================================================================

    [Fact]
    public void ToneMapPassExecutor_Constructor_SetsCorrectNameAndKind()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ToneMapPassExecutor("tonemap", shader);

        pass.Name.Should().Be("tonemap");
        pass.Kind.Should().Be(FramePassKind.ToneMap);

        shader.Dispose();
    }

    [Fact]
    public void ToneMapPassExecutor_WithOperator_SetsOperator()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ToneMapPassExecutor("tonemap", shader);

        var result = pass.WithOperator(ToneMapOperator.Reinhard);

        pass.Operator.Should().Be(ToneMapOperator.Reinhard);
        result.Should().BeSameAs(pass);

        shader.Dispose();
    }

    [Fact]
    public void ToneMapPassExecutor_WithExposure_SetsExposure()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ToneMapPassExecutor("tonemap", shader);

        var result = pass.WithExposure(2.0f);

        pass.Exposure.Should().Be(2.0f);
        result.Should().BeSameAs(pass);

        shader.Dispose();
    }

    [Fact]
    public void ToneMapPassExecutor_WithGamma_SetsGamma()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ToneMapPassExecutor("tonemap", shader);

        pass.WithGamma(1.8f);

        pass.Gamma.Should().Be(1.8f);

        shader.Dispose();
    }

    [Fact]
    public void ToneMapPassExecutor_DefaultOperator_IsAces()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ToneMapPassExecutor("tonemap", shader);

        pass.Operator.Should().Be(ToneMapOperator.Aces);

        shader.Dispose();
    }

    [Fact]
    public void ToneMapPassExecutor_Validate_ThrowsWhenExposureNotPositive()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ToneMapPassExecutor("tonemap", shader);
        pass.WithExposure(0f);

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        shader.Dispose();
    }

    [Fact]
    public void ToneMapPassExecutor_Validate_ThrowsWhenGammaNotPositive()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ToneMapPassExecutor("tonemap", shader);
        pass.WithGamma(-1f);

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        shader.Dispose();
    }

    [Fact]
    public void ToneMapPassExecutor_Validate_DoesNotThrowWithValidConfig()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ToneMapPassExecutor("tonemap", shader);
        pass.WithExposure(1.0f).WithGamma(2.2f);

        var action = () => pass.Validate();

        action.Should().NotThrow();

        shader.Dispose();
    }

    // =========================================================================
    // FxaaPassExecutor Tests
    // =========================================================================

    [Fact]
    public void FxaaPassExecutor_Constructor_SetsCorrectNameAndKind()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FxaaPassExecutor("fxaa", shader);

        pass.Name.Should().Be("fxaa");
        pass.Kind.Should().Be(FramePassKind.Fxaa);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPassExecutor_WithSubpixelQuality_SetsQuality()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FxaaPassExecutor("fxaa", shader);

        var result = pass.WithSubpixelQuality(0.5f);

        pass.SubpixelQuality.Should().Be(0.5f);
        result.Should().BeSameAs(pass);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPassExecutor_WithEdgeThreshold_SetsThreshold()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FxaaPassExecutor("fxaa", shader);

        pass.WithEdgeThreshold(0.2f);

        pass.EdgeThreshold.Should().Be(0.2f);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPassExecutor_WithEdgeThresholdMin_SetsThresholdMin()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FxaaPassExecutor("fxaa", shader);

        pass.WithEdgeThresholdMin(0.05f);

        pass.EdgeThresholdMin.Should().Be(0.05f);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPassExecutor_Validate_ThrowsWhenSubpixelQualityOutOfRange()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FxaaPassExecutor("fxaa", shader);
        pass.WithSubpixelQuality(1.5f);

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPassExecutor_Validate_ThrowsWhenSubpixelQualityNegative()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FxaaPassExecutor("fxaa", shader);
        pass.WithSubpixelQuality(-0.1f);

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPassExecutor_Validate_ThrowsWhenEdgeThresholdNegative()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FxaaPassExecutor("fxaa", shader);
        pass.WithEdgeThreshold(-0.1f);

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPassExecutor_Validate_ThrowsWhenEdgeThresholdMinNegative()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FxaaPassExecutor("fxaa", shader);
        pass.WithEdgeThresholdMin(-0.01f);

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPassExecutor_Validate_DoesNotThrowWithValidConfig()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FxaaPassExecutor("fxaa", shader);
        pass.WithSubpixelQuality(0.75f).WithEdgeThreshold(0.125f).WithEdgeThresholdMin(0.0312f);

        var action = () => pass.Validate();

        action.Should().NotThrow();

        shader.Dispose();
    }

    // =========================================================================
    // ComputePassExecutor Tests
    // =========================================================================

    [Fact]
    public void ComputePassExecutor_Constructor_SetsCorrectNameAndKind()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ComputePassExecutor("compute", shader);

        pass.Name.Should().Be("compute");
        pass.Kind.Should().Be(FramePassKind.Compute);

        shader.Dispose();
    }

    [Fact]
    public void ComputePassExecutor_WithDispatchSize_SetsGroupCounts()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ComputePassExecutor("compute", shader);

        var result = pass.WithDispatchSize(8, 4, 2);

        pass.GroupCountX.Should().Be(8u);
        pass.GroupCountY.Should().Be(4u);
        pass.GroupCountZ.Should().Be(2u);
        result.Should().BeSameAs(pass);

        shader.Dispose();
    }

    [Fact]
    public void ComputePassExecutor_DefaultDispatchSize_IsOneOneOne()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ComputePassExecutor("compute", shader);

        pass.GroupCountX.Should().Be(1u);
        pass.GroupCountY.Should().Be(1u);
        pass.GroupCountZ.Should().Be(1u);

        shader.Dispose();
    }

    [Fact]
    public void ComputePassExecutor_Validate_ThrowsWhenGroupCountXLessThanOne()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ComputePassExecutor("compute", shader);
        pass.WithDispatchSize(0, 1, 1);

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        shader.Dispose();
    }

    [Fact]
    public void ComputePassExecutor_BindStorageBuffer_AddsBinding()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ComputePassExecutor("compute", shader);

        var result = pass.BindStorageBuffer(0, "myBuffer");

        pass.Bindings.Should().HaveCount(1);
        pass.Bindings[0].BindingPoint.Should().Be(0u);
        pass.Bindings[0].ResourceName.Should().Be("myBuffer");
        pass.Bindings[0].Type.Should().Be(ComputeBindingType.ShaderStorageBuffer);
        result.Should().BeSameAs(pass);

        shader.Dispose();
    }

    [Fact]
    public void ComputePassExecutor_BindImage_AddsBinding()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ComputePassExecutor("compute", shader);

        pass.BindImage(1, "outputImage");

        pass.Bindings.Should().HaveCount(1);
        pass.Bindings[0].Type.Should().Be(ComputeBindingType.Image);

        shader.Dispose();
    }

    [Fact]
    public void ComputePassExecutor_BindUniformBuffer_AddsBinding()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ComputePassExecutor("compute", shader);

        pass.BindUniformBuffer(2, "params");

        pass.Bindings.Should().HaveCount(1);
        pass.Bindings[0].Type.Should().Be(ComputeBindingType.UniformBuffer);

        shader.Dispose();
    }

    [Fact]
    public void ComputePassExecutor_MultipleBindings_AllTracked()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ComputePassExecutor("compute", shader);

        pass.BindStorageBuffer(0, "buffer1")
            .BindImage(1, "image1")
            .BindUniformBuffer(2, "ubo1");

        pass.Bindings.Should().HaveCount(3);

        shader.Dispose();
    }

    [Fact]
    public void ComputePassExecutor_Validate_DoesNotThrowWithValidConfig()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new ComputePassExecutor("compute", shader);
        pass.WithDispatchSize(4, 4, 1);

        var action = () => pass.Validate();

        action.Should().NotThrow();

        shader.Dispose();
    }

    // =========================================================================
    // FullscreenQuadPassExecutor Tests
    // =========================================================================

    [Fact]
    public void FullscreenQuadPassExecutor_Constructor_SetsCorrectNameAndKind()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FullscreenQuadPassExecutor("fullscreen", shader);

        pass.Name.Should().Be("fullscreen");
        pass.Kind.Should().Be(FramePassKind.FullscreenQuad);

        shader.Dispose();
    }

    [Fact]
    public void FullscreenQuadPassExecutor_BindTexture_AddsTextureBinding()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FullscreenQuadPassExecutor("fullscreen", shader);

        var result = pass.BindTexture(0, "inputTex");

        pass.TextureBindings.Should().HaveCount(1);
        pass.TextureBindings[0].Unit.Should().Be(0);
        pass.TextureBindings[0].TextureName.Should().Be("inputTex");
        result.Should().BeSameAs(pass);

        shader.Dispose();
    }

    [Fact]
    public void FullscreenQuadPassExecutor_MultipleTextureBindings_AllTracked()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FullscreenQuadPassExecutor("fullscreen", shader);

        pass.BindTexture(0, "tex0").BindTexture(1, "tex1").BindTexture(2, "tex2");

        pass.TextureBindings.Should().HaveCount(3);

        shader.Dispose();
    }

    [Fact]
    public void FullscreenQuadPassExecutor_Validate_ThrowsWhenTextureUnitOutOfRange()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FullscreenQuadPassExecutor("fullscreen", shader);
        pass.BindTexture(32, "inputTex"); // 32 is out of [0, 31]

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        shader.Dispose();
    }

    [Fact]
    public void FullscreenQuadPassExecutor_Validate_ThrowsWhenTextureUnitNegative()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FullscreenQuadPassExecutor("fullscreen", shader);
        pass.BindTexture(-1, "inputTex");

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        shader.Dispose();
    }

    [Fact]
    public void FullscreenQuadPassExecutor_WithOutput_SetsOutputFramebuffer()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FullscreenQuadPassExecutor("fullscreen", shader);

        var result = pass.WithOutput("output_fbo");

        pass.OutputFramebufferName.Should().Be("output_fbo");
        result.Should().BeSameAs(pass);

        shader.Dispose();
    }

    [Fact]
    public void FullscreenQuadPassExecutor_OutputFramebufferName_DefaultsToNull()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FullscreenQuadPassExecutor("fullscreen", shader);

        pass.OutputFramebufferName.Should().BeNull();

        shader.Dispose();
    }

    [Fact]
    public void FullscreenQuadPassExecutor_Validate_DoesNotThrowWithValidConfig()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = new FullscreenQuadPassExecutor("fullscreen", shader);
        pass.BindTexture(0, "inputTex");

        var action = () => pass.Validate();

        action.Should().NotThrow();

        shader.Dispose();
    }

    // =========================================================================
    // BlitPassExecutor Tests
    // =========================================================================

    [Fact]
    public void BlitPassExecutor_Constructor_SetsCorrectNameAndKind()
    {
        var pass = new BlitPassExecutor("blit", "source_fbo", "dest_fbo");

        pass.Name.Should().Be("blit");
        pass.Kind.Should().Be(FramePassKind.Blit);
    }

    [Fact]
    public void BlitPassExecutor_Validate_ThrowsWhenSourceAndDestinationAreSame()
    {
        var pass = new BlitPassExecutor("blit", "same_fbo", "same_fbo");

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);
    }

    [Fact]
    public void BlitPassExecutor_Validate_DoesNotThrowWhenDifferent()
    {
        var pass = new BlitPassExecutor("blit", "source_fbo", "dest_fbo");

        var action = () => pass.Validate();

        action.Should().NotThrow();
    }

    [Fact]
    public void BlitPassExecutor_DeclaresCorrectResourceDependencies()
    {
        var pass = new BlitPassExecutor("blit", "source_fbo", "dest_fbo");

        pass.GetReadResources().ToArray().Should().Contain("source_fbo");
        pass.GetWrittenResources().ToArray().Should().Contain("dest_fbo");
    }

    // =========================================================================
    // CustomPassExecutor Tests
    // =========================================================================

    [Fact]
    public void CustomPassExecutor_Constructor_SetsCorrectNameAndKind()
    {
        var pass = new CustomPassExecutor("custom", FramePassKind.Custom, (ctx, execCtx) => { });

        pass.Name.Should().Be("custom");
        pass.Kind.Should().Be(FramePassKind.Custom);
    }

    [Fact]
    public void CustomPassExecutor_Constructor_AcceptsAnyPassKind()
    {
        var pass = new CustomPassExecutor("custom_post", FramePassKind.PostProcess, (ctx, execCtx) => { });

        pass.Kind.Should().Be(FramePassKind.PostProcess);
    }

    [Fact]
    public void CustomPassExecutor_ExecuteCallback_IsAccessible()
    {
        Action<GLContext, PassExecutionContext> callback = (ctx, execCtx) => { };
        var pass = new CustomPassExecutor("custom", FramePassKind.Custom, callback);

        pass.ExecuteCallback.Should().BeSameAs(callback);
    }

    [Fact]
    public void CustomPassExecutor_Validate_InvokesValidateCallback()
    {
        bool validateInvoked = false;
        var pass = new CustomPassExecutor(
            "custom",
            FramePassKind.Custom,
            (ctx, execCtx) => { },
            () => { validateInvoked = true; });

        pass.Validate();

        validateInvoked.Should().BeTrue();
    }

    [Fact]
    public void CustomPassExecutor_ValidateCallback_IsAccessible()
    {
        Action validateCallback = () => { };
        var pass = new CustomPassExecutor(
            "custom",
            FramePassKind.Custom,
            (ctx, execCtx) => { },
            validateCallback);

        pass.ValidateCallback.Should().BeSameAs(validateCallback);
    }

    [Fact]
    public void CustomPassExecutor_ValidateCallbackIsNull_DoesNotThrow()
    {
        var pass = new CustomPassExecutor("custom", FramePassKind.Custom, (ctx, execCtx) => { });

        var action = () => pass.Validate();

        action.Should().NotThrow();
    }

    [Fact]
    public void CustomPassExecutor_Reads_DeclaresReadResource()
    {
        var pass = new CustomPassExecutor("custom", FramePassKind.Custom, (ctx, execCtx) => { });
        pass.Reads("input_texture");

        pass.GetReadResources().ToArray().Should().Contain("input_texture");
    }

    [Fact]
    public void CustomPassExecutor_Writes_DeclaresWrittenResource()
    {
        var pass = new CustomPassExecutor("custom", FramePassKind.Custom, (ctx, execCtx) => { });
        pass.Writes("output_texture");

        pass.GetWrittenResources().ToArray().Should().Contain("output_texture");
    }

    // =========================================================================
    // ShadowPassExecutor Tests
    // =========================================================================

    [Fact]
    public void ShadowPassExecutor_Constructor_SetsCorrectNameAndKind()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new ShadowPassExecutor(
            "shadow",
            program,
            "shadow_map",
            "shadow_fbo",
            Matrix4x4.Identity);

        pass.Name.Should().Be("shadow");
        pass.Kind.Should().Be(FramePassKind.Shadow);

        program.Dispose();
    }

    [Fact]
    public void ShadowPassExecutor_Validate_ThrowsWhenProgramDisposed()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new ShadowPassExecutor(
            "shadow",
            program,
            "shadow_map",
            "shadow_fbo",
            Matrix4x4.Identity);
        program.Dispose();

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);
    }

    [Fact]
    public void ShadowPassExecutor_Validate_DoesNotThrowWithValidProgram()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new ShadowPassExecutor(
            "shadow",
            program,
            "shadow_map",
            "shadow_fbo",
            Matrix4x4.Identity);

        var action = () => pass.Validate();

        action.Should().NotThrow();

        program.Dispose();
    }

    [Fact]
    public void ShadowPassExecutor_DeclaresCorrectResourceDependencies()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new ShadowPassExecutor(
            "shadow",
            program,
            "shadow_map",
            "shadow_fbo",
            Matrix4x4.Identity);

        pass.GetWrittenResources().ToArray().Should().Contain("shadow_map");
        pass.GetWrittenResources().ToArray().Should().Contain("shadow_fbo");

        program.Dispose();
    }

    // =========================================================================
    // PostProcessPassExecutor Tests
    // =========================================================================

    [Fact]
    public void PostProcessPassExecutor_Constructor_SetsCorrectNameAndKind()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new PostProcessPassExecutor("postprocess", program, "input_tex");

        pass.Name.Should().Be("postprocess");
        pass.Kind.Should().Be(FramePassKind.PostProcess);

        program.Dispose();
    }

    [Fact]
    public void PostProcessPassExecutor_Validate_ThrowsWhenProgramDisposed()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new PostProcessPassExecutor("postprocess", program, "input_tex");
        program.Dispose();

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);
    }

    [Fact]
    public void PostProcessPassExecutor_Validate_DoesNotThrowWithValidProgram()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new PostProcessPassExecutor("postprocess", program, "input_tex");

        var action = () => pass.Validate();

        action.Should().NotThrow();

        program.Dispose();
    }

    [Fact]
    public void PostProcessPassExecutor_SetUniform_ReturnsSameInstance()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new PostProcessPassExecutor("postprocess", program, "input_tex");

        var result = pass.SetUniform("u_param", (ctx, prog) => { });

        result.Should().BeSameAs(pass);

        program.Dispose();
    }

    [Fact]
    public void PostProcessPassExecutor_DeclaresCorrectReadResource()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new PostProcessPassExecutor("postprocess", program, "input_tex");

        pass.GetReadResources().ToArray().Should().Contain("input_tex");

        program.Dispose();
    }

    [Fact]
    public void PostProcessPassExecutor_WithOutputFramebuffer_DeclaresWriteResource()
    {
        var program = new GLProgram(_context, "vs", "fs");
        var pass = new PostProcessPassExecutor("postprocess", program, "input_tex", "output_fbo");

        pass.GetWrittenResources().ToArray().Should().Contain("output_fbo");

        program.Dispose();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
