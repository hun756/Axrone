using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.Shading;

namespace Axrone.Render.Effects.Tests;

/// <summary>
/// Tests for the <see cref="FxaaPass"/> effect pass. Covers factory initialization
/// (Name, Kind), payload configuration through <c>Data</c>, and Validate() pre-condition
/// checks.
/// </summary>
public sealed class FxaaPassTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public FxaaPassTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    // =========================================================================
    // FxaaPass Tests
    // =========================================================================

    [Fact]
    public void FxaaPass_Constructor_SetsCorrectNameAndKind()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = FxaaPass.Create("fxaa", shader);

        pass.Name.Should().Be("fxaa");
        pass.Kind.Should().Be(FramePassKind.Fxaa);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPass_SubpixelQuality_IsMutableThroughData()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = FxaaPass.Create("fxaa", shader);

        pass.Data.SubpixelQuality = 0.5f;

        pass.Data.SubpixelQuality.Should().Be(0.5f);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPass_EdgeThreshold_IsMutableThroughData()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = FxaaPass.Create("fxaa", shader);

        pass.Data.EdgeThreshold = 0.2f;

        pass.Data.EdgeThreshold.Should().Be(0.2f);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPass_EdgeThresholdMin_IsMutableThroughData()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = FxaaPass.Create("fxaa", shader);

        pass.Data.EdgeThresholdMin = 0.05f;

        pass.Data.EdgeThresholdMin.Should().Be(0.05f);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPass_Validate_ThrowsWhenSubpixelQualityOutOfRange()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = FxaaPass.Create("fxaa", shader);
        pass.Data.SubpixelQuality = 1.5f;

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPass_Validate_ThrowsWhenSubpixelQualityNegative()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = FxaaPass.Create("fxaa", shader);
        pass.Data.SubpixelQuality = -0.1f;

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPass_Validate_ThrowsWhenEdgeThresholdNegative()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = FxaaPass.Create("fxaa", shader);
        pass.Data.EdgeThreshold = -0.1f;

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPass_Validate_ThrowsWhenEdgeThresholdMinNegative()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = FxaaPass.Create("fxaa", shader);
        pass.Data.EdgeThresholdMin = -0.01f;

        var action = () => pass.Validate();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);

        shader.Dispose();
    }

    [Fact]
    public void FxaaPass_Validate_DoesNotThrowWithValidConfig()
    {
        var shader = new GLProgram(_context, "vs", "fs");
        var pass = FxaaPass.Create("fxaa", shader,
            subpixelQuality: 0.75f,
            edgeThreshold: 0.125f,
            edgeThresholdMin: 0.0312f);

        var action = () => pass.Validate();

        action.Should().NotThrow();

        shader.Dispose();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
