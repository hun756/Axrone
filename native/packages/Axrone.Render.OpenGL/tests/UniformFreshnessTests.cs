namespace Axrone.Render.OpenGL.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Shading;

public class UniformFreshnessTests
{
    private static int CountCalls(MockGLApi mock, string fragment) =>
        mock.CallLog.Count(c => c.Contains(fragment, StringComparison.Ordinal));

    [Fact]
    public void RebuildCount_TracksBuilds()
    {
        using var context = new GLContext(new MockGLApi());
        var program = new GLProgram(context, "void main() { }", "void main() { }");
        program.RebuildCount.Should().Be(1);

        program.Rebuild();
        program.RebuildCount.Should().Be(2);

        program.Dispose();
    }

    [Fact]
    public void Uniforms_ReuploadAfterContextRestore()
    {
        using var context = new GLContext(new MockGLApi());
        var mock = (MockGLApi)context.GL;
        var program = new GLProgram(context, "void main() { }", "void main() { }");
        var instance = new ShaderInstance(context, program, new UniformCache());

        instance.SetFloat(0, 1.0f);
        instance.SetFloat(0, 1.0f);
        CountCalls(mock, "Uniform1").Should().Be(1, "identical value must dedup");

        context.NotifyContextLost();
        context.NotifyContextRestored();
        program.RebuildCount.Should().Be(2);

        instance.SetFloat(0, 1.0f);
        CountCalls(mock, "Uniform1").Should().Be(2, "rebuild epoch must force re-upload");

        program.Dispose();
    }
}
