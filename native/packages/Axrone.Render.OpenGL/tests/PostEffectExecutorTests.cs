namespace Axrone.Render.OpenGL.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.FrameGraph.PassExecutors;
using Axrone.Render.OpenGL.Shading;

public class PostEffectExecutorTests
{
    private static GLContext CreateContext(out MockGLApi mock)
    {
        mock = new MockGLApi();
        return new GLContext(mock);
    }

    private static GLProgram CreateProgram(GLContext context) =>
        new(context, "void main() { }", "void main() { }");

    [Fact]
    public void Ssao_DeclaresDepthNormalInputs()
    {
        using var context = CreateContext(out _);
        var program = CreateProgram(context);
        var pass = new SsaoPassExecutor("ssao", program, "depth", "normal", "ao");

        pass.Kind.Should().Be(FramePassKind.PostProcess);
        pass.GetReadResources().ToArray().Should().BeEquivalentTo("depth", "normal");
        pass.GetWrittenResources().ToArray().Should().Equal("ao");

        Action validate = () => pass.Validate();
        validate.Should().NotThrow();

        program.Dispose();
        Action disposed = () => pass.Validate();
        disposed.Should().Throw<RenderException>()
            .Where(ex => ex.Code == RenderErrorCode.InvalidPassConfiguration);
    }

    [Fact]
    public void Dof_ValidatesFocusRanges()
    {
        using var context = CreateContext(out _);
        var program = CreateProgram(context);
        var pass = new DofPassExecutor("dof", program, "scene", "depth", "dof");

        pass.Kind.Should().Be(FramePassKind.PostProcess);

        Action validate = () => pass.Validate();
        validate.Should().NotThrow();

        program.Dispose();
    }

    [Fact]
    public void FilmGrain_ValidatesIntensity()
    {
        using var context = CreateContext(out _);
        var program = CreateProgram(context);
        var pass = new FilmGrainPassExecutor("grain", program, "scene", "grained")
            .WithIntensity(0.5f);

        pass.Kind.Should().Be(FramePassKind.PostProcess);

        Action validate = () => pass.Validate();
        validate.Should().NotThrow();

        pass.WithIntensity(2.0f);
        Action invalid = () => pass.Validate();
        invalid.Should().Throw<RenderException>()
            .Where(ex => ex.Code == RenderErrorCode.InvalidPassConfiguration);

        program.Dispose();
    }

    [Fact]
    public void Vignette_ValidatesRanges()
    {
        using var context = CreateContext(out _);
        var program = CreateProgram(context);
        var pass = new VignettePassExecutor("vignette", program, "scene", "out");

        Action validate = () => pass.Validate();
        validate.Should().NotThrow();

        pass.WithIntensity(1.5f);
        Action invalid = () => pass.Validate();
        invalid.Should().Throw<RenderException>()
            .Where(ex => ex.Code == RenderErrorCode.InvalidPassConfiguration);

        program.Dispose();
    }

    [Fact]
    public void ChromaticAberration_ValidatesOffset()
    {
        using var context = CreateContext(out _);
        var program = CreateProgram(context);
        var pass = new ChromaticAberrationPassExecutor("ca", program, "scene", "out");

        Action validate = () => pass.Validate();
        validate.Should().NotThrow();

        program.Dispose();
    }

    [Fact]
    public void ColorGrading_DefaultsAreNeutral()
    {
        using var context = CreateContext(out _);
        var program = CreateProgram(context);
        var pass = new ColorGradingPassExecutor("grade", program, "scene", "out");

        pass.Contrast.Should().BeApproximately(1.0f, 1e-6f);
        pass.Saturation.Should().BeApproximately(1.0f, 1e-6f);
        pass.Brightness.Should().BeApproximately(1.0f, 1e-6f);

        Action validate = () => pass.Validate();
        validate.Should().NotThrow();

        program.Dispose();
    }
}
