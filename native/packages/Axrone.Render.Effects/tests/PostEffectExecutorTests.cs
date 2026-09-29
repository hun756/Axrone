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
        var pass = SsaoPass.Create("ssao", program, "depth", "normal", "ao");

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
        var pass = DofPass.Create("dof", program, "scene", "depth", "dof");

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
        var pass = FilmGrainPass.Create("grain", program, "scene", "grained", intensity: 0.5f);

        pass.Kind.Should().Be(FramePassKind.PostProcess);

        Action validate = () => pass.Validate();
        validate.Should().NotThrow();

        pass.Data.Intensity = 2.0f;
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
        var pass = VignettePass.Create("vignette", program, "scene", "out");

        Action validate = () => pass.Validate();
        validate.Should().NotThrow();

        pass.Data.Intensity = 1.5f;
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
        var pass = VignettePass.Create("vignette", program, "scene", "out");

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
    public void Vignette_WithoutPassContext_FailsClosed()
    {
        using var context = CreateContext(out _);
        var program = CreateProgram(context);
        var pass = VignettePass.Create("vignette", program, "scene", "out");

        // A GLRenderContext without a configured PassContext cannot resolve the pass
        // resources, so the pass must refuse to run instead of silently no-op.
        var renderContext = new GLRenderContext(context);

        var action = () => ((IRenderPass)pass).Execute(renderContext);

        action.Should().Throw<RenderException>()
            .Where(ex => ex.Code == RenderErrorCode.InvalidOperation);
    }

    [Fact]
    public void ChromaticAberration_ValidatesOffset()
    {
        using var context = CreateContext(out _);
        var program = CreateProgram(context);
        var pass = ChromaticAberrationPass.Create("ca", program, "scene", "out");

        Action validate = () => pass.Validate();
        validate.Should().NotThrow();

        program.Dispose();
    }

    [Fact]
    public void Phases_FollowHdrVersusDisplaySplit()
    {
        using var context = CreateContext(out _);
        var program = CreateProgram(context);

        var ssao = SsaoPass.Create("a", program, "d", "n", "o");
        var dof = DofPass.Create("b", program, "i", "d", "o");
        var grain = FilmGrainPass.Create("c", program, "i", "o");
        var vignette = VignettePass.Create("d", program, "i", "o");
        var aberration = ChromaticAberrationPass.Create("e", program, "i", "o");
        var grading = ColorGradingPass.Create("f", program, "i", "o");

        ssao.Data.Phase.Should().Be(PostProcessPhase.BeforeTonemap);
        dof.Data.Phase.Should().Be(PostProcessPhase.BeforeTonemap);
        grain.Data.Phase.Should().Be(PostProcessPhase.AfterTonemap);
        vignette.Data.Phase.Should().Be(PostProcessPhase.AfterTonemap);
        aberration.Data.Phase.Should().Be(PostProcessPhase.AfterTonemap);
        grading.Data.Phase.Should().Be(PostProcessPhase.AfterTonemap);

        program.Dispose();
    }

    [Fact]
    public void ColorGrading_DefaultsAreNeutral()
    {
        using var context = CreateContext(out _);
        var program = CreateProgram(context);
        var pass = ColorGradingPass.Create("grade", program, "scene", "out");

        pass.Data.Contrast.Should().BeApproximately(1.0f, 1e-6f);
        pass.Data.Saturation.Should().BeApproximately(1.0f, 1e-6f);
        pass.Data.Brightness.Should().BeApproximately(1.0f, 1e-6f);

        Action validate = () => pass.Validate();
        validate.Should().NotThrow();

        program.Dispose();
    }
}
