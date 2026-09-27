namespace Axrone.Render.OpenGL.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Render.OpenGL.Batch;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.FrameGraph.PassExecutors;
using Axrone.Render.OpenGL.Mesh;
using Axrone.Render.OpenGL.Shading;

public class TonemapPoolOcclusionTests
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

    [Fact]
    public void ProgramPool_ReusesAndEvicts()
    {
        using var context = CreateContext(out _);
        using var pool = new GLProgramPool(1);

        GLProgram first = pool.GetOrCreate(context, "vs1", "fs1");
        pool.GetOrCreate(context, "vs1", "fs1").Should().BeSameAs(first);
        pool.Count.Should().Be(1);

        pool.GetOrCreate(context, "vs2", "fs2");
        pool.Count.Should().Be(1);
        first.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void StandardShaders_BuildThroughPool()
    {
        using var context = CreateContext(out _);
        using var pool = new GLProgramPool();

        GLProgram unlit = pool.GetOrCreate(context, StandardShaders.UnlitVertex, StandardShaders.UnlitFragment);
        unlit.Id.Should().NotBe(0u);

        GLProgram standard = pool.GetOrCreate(context, StandardShaders.StandardVertex, StandardShaders.StandardFragment);
        standard.Id.Should().NotBe(0u);
        standard.Should().NotBeSameAs(unlit);
    }

    [Fact]
    public void Occlusion_FirstFrameIssuesQuery()
    {
        using var context = CreateContext(out MockGLApi mock);
        using var culler = new OcclusionCuller(context, 4);
        var mesh = MeshGenerators.CreatePlane(context, 1.0f, 1.0f);

        culler.BeginFrame();
        culler.BeginOcclusion(mesh).Should().BeTrue();
        culler.EndOcclusion();
        culler.IsVisible(mesh).Should().BeTrue();
        culler.EndFrame();

        mock.CallLog.Should().Contain(c => c.Contains("BeginQuery", StringComparison.Ordinal));
    }
}
