using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.Shading;

namespace Axrone.Render.Effects.Tests;

/// <summary>
/// Tests for the <see cref="FxaaPassExecutor"/> effect pass. Covers constructor
/// initialization (Name, Kind), fluent configuration, and Validate() pre-condition checks.
/// </summary>
public sealed class FxaaPassExecutorTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public FxaaPassExecutorTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
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

    public void Dispose()
    {
        _context.Dispose();
    }
}
