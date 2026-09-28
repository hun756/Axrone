using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.Shading;

namespace Axrone.Render.Effects.Tests;

/// <summary>
/// Tests for the <see cref="BloomPassExecutor"/> and <see cref="ToneMapPassExecutor"/>
/// effect passes. Covers constructor initialization (Name, Kind), fluent configuration,
/// and Validate() pre-condition checks.
/// </summary>
public sealed class BloomToneMapExecutorTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public BloomToneMapExecutorTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
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

    public void Dispose()
    {
        _context.Dispose();
    }
}
