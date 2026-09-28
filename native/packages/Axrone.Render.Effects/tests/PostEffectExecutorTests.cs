namespace Axrone.Render.Effects.Tests;

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
    public void Vignette_ExecutesThroughGenericRenderContext()
    {
        using var context = CreateContext(out var mock);
        var program = CreateProgram(context);
        var pass = new VignettePassExecutor("vignette", program, "scene", "out");

        var execCtx = new PassExecutionContext(context);
        using var input = new GLTexture(context, GLConst.Texture2D, TextureFormat.Rgba16f, 64, 32, label: "scene");
        using var output = new GLTexture(context, GLConst.Texture2D, TextureFormat.Rgba16f, 64, 32, label: "out");
        execCtx.SetResource("scene", input);
        execCtx.SetResource("out", output);

        var renderContext = new GLRenderContext(context, execCtx);
        mock.ClearCallLog();

        ((IRenderPass)pass).Execute(renderContext);

        mock.CallLog.Should().Contain(c => c.Contains("UseProgram", StringComparison.Ordinal));
        mock.CallLog.Should().Contain(c => c.Contains("DrawArrays", StringComparison.Ordinal));
        renderContext.IsPassActive.Should().BeFalse();
    }

    [Fact]
    public void Vignette_LegacyGlContextBridge_FailsClosed()
    {
        using var context = CreateContext(out _);
        var program = CreateProgram(context);
        var pass = new VignettePassExecutor("vignette", program, "scene", "out");

        var action = () => pass.Execute(context, new PassExecutionContext(context));

        action.Should().Throw<RenderException>()
            .Where(ex => ex.Code == RenderErrorCode.InvalidOperation);
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
    public void Phases_FollowHdrVersusDisplaySplit()
    {
        using var context = CreateContext(out _);
        var program = CreateProgram(context);

        new SsaoPassExecutor("a", program, "d", "n", "o").Phase.Should().Be(PostProcessPhase.BeforeTonemap);
        new DofPassExecutor("b", program, "i", "d", "o").Phase.Should().Be(PostProcessPhase.BeforeTonemap);
        new FilmGrainPassExecutor("c", program, "i", "o").Phase.Should().Be(PostProcessPhase.AfterTonemap);
        new VignettePassExecutor("d", program, "i", "o").Phase.Should().Be(PostProcessPhase.AfterTonemap);
        new ChromaticAberrationPassExecutor("e", program, "i", "o").Phase.Should().Be(PostProcessPhase.AfterTonemap);
        new ColorGradingPassExecutor("f", program, "i", "o").Phase.Should().Be(PostProcessPhase.AfterTonemap);

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
