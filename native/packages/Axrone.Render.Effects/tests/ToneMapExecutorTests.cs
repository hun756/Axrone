namespace Axrone.Render.Effects.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Shading;

public class ToneMapExecutorTests
{
    private static GLContext CreateContext(out MockGLApi mock)
    {
        mock = new MockGLApi();
        return new GLContext(mock);
    }

    [Fact]
    public void Tonemap_OperatorsMapToWireIds()
    {
        using var context = CreateContext(out _);
        var program = new GLProgram(context, "void main() { }", "void main() { }");
        var pass = new ToneMapPassExecutor("tonemap", program);

        pass.WithOperator(ToneMapOperator.Filmic);
        pass.Operator.Should().Be(ToneMapOperator.Filmic);
        pass.OperatorId.Should().Be((int)ToneMapOperator.Filmic);

        pass.WithOperator(ToneMapOperator.AgX);
        pass.OperatorId.Should().Be((int)ToneMapOperator.AgX);

        program.Dispose();
    }

    [Fact]
    public void Tonemap_ExposureHistoryWiring()
    {
        using var context = CreateContext(out _);
        var program = new GLProgram(context, "void main() { }", "void main() { }");
        var adapt = new GLProgram(context, "void main() { }", "void main() { }");
        var pass = new ToneMapPassExecutor("tonemap", program, "hdr", "ldr", "expoRead", "expoWrite")
            .WithExposureHistory(adapt)
            .WithAdaptationSpeed(0.5f);

        pass.HasExposureHistory.Should().BeTrue();
        pass.AdaptationSpeed.Should().BeApproximately(0.5f, 1e-6f);

        Action validate = () => pass.Validate();
        validate.Should().NotThrow();

        var plain = new ToneMapPassExecutor("tonemap", program);
        plain.HasExposureHistory.Should().BeFalse();

        program.Dispose();
        adapt.Dispose();
    }
}
