namespace Axrone.Render.OpenGL.Tests;

/// <summary>
/// Tests for <see cref="Axrone.Render.Core.ThrowHelper"/> verifying each method throws the correct
/// exception type with the correct error code and message content.
/// </summary>
public sealed class ThrowHelperTests
{
    [Fact]
    public void ThrowContextLost_ThrowsGLException_WithContextLostCode()
    {
        var action = () => ThrowHelper.ThrowContextLost();

        action.Should().Throw<GLException>()
            .Where(e => e.Code == GLContextErrorCode.ContextLost);
    }

    [Fact]
    public void ThrowContextDisposed_ThrowsGLException_WithContextAlreadyDisposedCode()
    {
        var action = () => ThrowHelper.ThrowContextDisposed();

        action.Should().Throw<GLException>()
            .Where(e => e.Code == GLContextErrorCode.ContextAlreadyDisposed);
    }

    [Fact]
    public void ThrowInvalidOperation_ThrowsGLException_WithInvalidOperationCode()
    {
        var action = () => ThrowHelper.ThrowInvalidOperation("test message");

        action.Should().Throw<GLException>()
            .Where(e => e.Code == GLContextErrorCode.InvalidOperation);
    }

    [Fact]
    public void ThrowExtensionNotSupported_ThrowsGLException_WithExtensionNotSupportedCode()
    {
        var action = () => ThrowHelper.ThrowExtensionNotSupported("GL_TEST_EXT");

        action.Should().Throw<GLException>()
            .Where(e => e.Code == GLContextErrorCode.ExtensionNotSupported);
    }

    [Fact]
    public void ThrowOffsetNegative_ThrowsGLBufferException_WithInvalidOffsetCode()
    {
        var action = () => ThrowHelper.ThrowOffsetNegative();

        action.Should().Throw<GLBufferException>()
            .Where(e => e.BufferCode == GLBufferErrorCode.InvalidOffset);
    }

    [Fact]
    public void ThrowBufferBoundsExceeded_ThrowsGLBufferException_WithBoundsExceededCode()
    {
        var action = () => ThrowHelper.ThrowBufferBoundsExceeded();

        action.Should().Throw<GLBufferException>()
            .Where(e => e.BufferCode == GLBufferErrorCode.BoundsExceeded);
    }

    [Fact]
    public void ThrowBufferDisposed_ThrowsGLBufferException_WithBufferAlreadyDisposedCode()
    {
        var action = () => ThrowHelper.ThrowBufferDisposed();

        action.Should().Throw<GLBufferException>()
            .Where(e => e.BufferCode == GLBufferErrorCode.BufferAlreadyDisposed);
    }

    [Fact]
    public void ThrowObjectDisposed_ThrowsObjectDisposedException()
    {
        var action = () => ThrowHelper.ThrowObjectDisposed("TestObject");

        action.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Throw_WithRenderErrorCode_ThrowsRenderException()
    {
        var action = () => ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "test message", "TestContext");

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidOperation);
    }

    [Fact]
    public void Throw_IncludesContextInMessage()
    {
        var action = () => ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "test message", "TestContext");

        action.Should().Throw<RenderException>()
            .Where(e => e.Message.Contains("TestContext") && e.Message.Contains("test message"));
    }

    [Fact]
    public void ThrowInvalidValue_ThrowsGLException_WithInvalidValueCode()
    {
        var action = () => ThrowHelper.ThrowInvalidValue("bad value");

        action.Should().Throw<GLException>()
            .Where(e => e.Code == GLContextErrorCode.InvalidValue);
    }

    [Fact]
    public void ThrowInvalidArgument_ThrowsArgumentException()
    {
        var action = () => ThrowHelper.ThrowInvalidArgument("bad argument");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ThrowSourceRangeExceeded_ThrowsGLBufferException()
    {
        var action = () => ThrowHelper.ThrowSourceRangeExceeded();

        action.Should().Throw<GLBufferException>()
            .Where(e => e.BufferCode == GLBufferErrorCode.BoundsExceeded);
    }

    [Fact]
    public void ThrowDestRangeExceeded_ThrowsGLBufferException()
    {
        var action = () => ThrowHelper.ThrowDestRangeExceeded();

        action.Should().Throw<GLBufferException>()
            .Where(e => e.BufferCode == GLBufferErrorCode.BoundsExceeded);
    }

    [Fact]
    public void ThrowAlignmentViolation_ThrowsGLBufferException_WithAlignmentViolationCode()
    {
        var action = () => ThrowHelper.ThrowAlignmentViolation(5, 4);

        action.Should().Throw<GLBufferException>()
            .Where(e => e.BufferCode == GLBufferErrorCode.AlignmentViolation);
    }

    [Fact]
    public void ThrowShaderCompileFailed_ThrowsGLShaderException()
    {
        var action = () => ThrowHelper.ThrowShaderCompileFailed("error log");

        action.Should().Throw<GLShaderException>()
            .Where(e => e.ShaderCode == GLShaderErrorCode.ShaderCompileFailed);
    }

    [Fact]
    public void ThrowShaderLinkFailed_ThrowsGLShaderException()
    {
        var action = () => ThrowHelper.ThrowShaderLinkFailed("link error");

        action.Should().Throw<GLShaderException>()
            .Where(e => e.ShaderCode == GLShaderErrorCode.ShaderLinkFailed);
    }

    [Fact]
    public void ThrowIncompleteFramebuffer_ThrowsGLFramebufferException()
    {
        var action = () => ThrowHelper.ThrowIncompleteFramebuffer(0x8CD6);

        action.Should().Throw<GLFramebufferException>()
            .Where(e => e.FramebufferCode == GLFramebufferErrorCode.IncompleteFramebuffer);
    }

    [Fact]
    public void ThrowExecutorNotFound_ThrowsRenderException()
    {
        var action = () => ThrowHelper.ThrowExecutorNotFound("Custom");

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.ExecutorNotFound);
    }

    [Fact]
    public void ThrowOutOfMemory_ThrowsGLException()
    {
        var action = () => ThrowHelper.ThrowOutOfMemory("GPU OOM");

        action.Should().Throw<GLException>()
            .Where(e => e.Code == GLContextErrorCode.OutOfMemory);
    }
}
