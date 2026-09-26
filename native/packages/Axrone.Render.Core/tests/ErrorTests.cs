namespace Axrone.Render.Core.Tests;

using Xunit;
using FluentAssertions;

public class ErrorTests
{
    [Fact]
    public void CodedExceptions_CarryCodes()
    {
        var render = new RenderException("boom", RenderErrorCode.BackendFailed);
        render.Code.Should().Be(RenderErrorCode.BackendFailed);

        var gl = new GLException("lost", GLContextErrorCode.ContextLost);
        gl.Code.Should().Be(GLContextErrorCode.ContextLost);

        var buffer = new GLBufferException("bounds", GLBufferErrorCode.BoundsExceeded);
        buffer.BufferCode.Should().Be(GLBufferErrorCode.BoundsExceeded);

        var shader = new GLShaderException("compile", GLShaderErrorCode.ShaderCompileFailed, "log");
        shader.ShaderCode.Should().Be(GLShaderErrorCode.ShaderCompileFailed);
        shader.InfoLog.Should().Be("log");

        var framebuffer = new GLFramebufferException("incomplete", GLFramebufferErrorCode.IncompleteFramebuffer, 0x8CDD);
        framebuffer.FramebufferCode.Should().Be(GLFramebufferErrorCode.IncompleteFramebuffer);
        framebuffer.Status.Should().Be(0x8CDD);

        var texture = new GLTextureException("unknown", TextureErrorCode.FormatNotSupported);
        texture.TextureCode.Should().Be(TextureErrorCode.FormatNotSupported);
    }

    [Fact]
    public void ThrowHelper_DoesNotReturn()
    {
        Action contextLost = () => ThrowHelper.ThrowContextLost();
        contextLost.Should().Throw<GLException>()
            .Where(ex => ex.Code == GLContextErrorCode.ContextLost);

        Action bounds = () => ThrowHelper.ThrowBufferBoundsExceeded();
        bounds.Should().Throw<GLBufferException>()
            .Where(ex => ex.BufferCode == GLBufferErrorCode.BoundsExceeded);

        Action executor = () => ThrowHelper.ThrowExecutorNotFound("Shadow");
        executor.Should().Throw<RenderException>()
            .Where(ex => ex.Code == RenderErrorCode.ExecutorNotFound);
    }

    [Fact]
    public void ThrowUnknownTextureFormat_ThrowsCodedException()
    {
        Action unknownFormat = () => ThrowHelper.ThrowUnknownTextureFormat("Bogus");

        unknownFormat.Should().Throw<GLTextureException>()
            .Where(ex => ex.TextureCode == TextureErrorCode.FormatNotSupported);
    }
}
