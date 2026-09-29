namespace Axrone.Render.OpenGL.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Render.OpenGL.Batch;
using Axrone.Render.OpenGL.Context;
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
        pool.EvictionCount.Should().Be(1);
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

    [Fact]
    public void Occlusion_PruneDropsUnseenMeshes()
    {
        using var context = CreateContext(out _);
        using var culler = new OcclusionCuller(context, 4);
        var meshA = MeshGenerators.CreatePlane(context, 1.0f, 1.0f);
        var meshB = MeshGenerators.CreatePlane(context, 1.0f, 1.0f);

        culler.BeginFrame();
        culler.BeginOcclusion(meshA).Should().BeTrue();
        culler.EndOcclusion();
        culler.BeginOcclusion(meshB).Should().BeTrue();
        culler.EndOcclusion();
        culler.EndFrame();
        culler.TrackedMeshCount.Should().Be(2);

        for (int i = 0; i < 3; i++)
        {
            culler.BeginFrame();
            culler.IsVisible(meshA).Should().BeTrue();
            culler.EndFrame();
        }

        culler.PruneStaleMeshes(1);
        culler.TrackedMeshCount.Should().Be(1);
        culler.IsVisible(meshA).Should().BeTrue();
    }
}
