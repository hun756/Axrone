namespace Axrone.Render.OpenGL.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.FrameGraph.PassExecutors;
using Axrone.Render.OpenGL.Mesh;
using Axrone.Render.OpenGL.Native;
using Axrone.Render.OpenGL.Resources;
using Axrone.Render.OpenGL.Shading;

public class PresentDepthSkyboxTests
{
    private static GLContext CreateContext(out MockGLApi mock)
    {
        mock = new MockGLApi();
        return new GLContext(mock);
    }

    private static GLProgram CreateProgram(GLContext context) =>
        new(context, "void main() { }", "void main() { }");

    [Fact]
    public void Present_RoutesToDefaultFramebuffer()
    {
        var pass = new PresentPassExecutor("present", "scene");
        pass.Kind.Should().Be(FramePassKind.Present);
        pass.GetReadResources().ToArray().Should().Equal("scene");

        Action validate = () => pass.Validate();
        validate.Should().NotThrow();

        var bad = new PresentPassExecutor("present", "scene", -1, 0);
        Action invalid = () => bad.Validate();
        invalid.Should().Throw<RenderException>()
            .Where(ex => ex.Code == RenderErrorCode.InvalidPassConfiguration);
    }

    [Fact]
    public void DepthPrepass_ValidatesConfiguration()
    {
        using var context = CreateContext(out _);
        var program = CreateProgram(context);
        var pass = new DepthPrepassPassExecutor(
            "depth", program, "u_viewProj",
            new List<GLMesh>(), "depthTex");

        pass.Kind.Should().Be(FramePassKind.DepthPrepass);
        pass.MeshCount.Should().Be(0);

        Action validate = () => pass.Validate();
        validate.Should().NotThrow();

        program.Dispose();
        Action disposed = () => pass.Validate();
        disposed.Should().Throw<RenderException>()
            .Where(ex => ex.Code == RenderErrorCode.InvalidPassConfiguration);
    }

    [Fact]
    public void Skybox_RequiresCubemapTarget()
    {
        using var context = CreateContext(out _);
        var program = CreateProgram(context);
        var flat = new GLTexture(context, GLConst.Texture2D, TextureFormat.Rgba8, 4, 4);

        var bad = new SkyboxPassExecutor("sky", program, flat, "u_viewProj");
        Action invalid = () => bad.Validate();
        invalid.Should().Throw<RenderException>()
            .Where(ex => ex.Code == RenderErrorCode.InvalidPassConfiguration);

        var cube = new GLTexture(context, GLConst.TextureCubeMap, TextureFormat.Rgba8, 4, 4);
        var good = new SkyboxPassExecutor("sky", program, cube, "u_viewProj");
        good.Kind.Should().Be(FramePassKind.Skybox);

        Action validate = () => good.Validate();
        validate.Should().NotThrow();

        flat.Dispose();
        cube.Dispose();
        program.Dispose();
    }
}
