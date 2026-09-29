namespace Axrone.Render.OpenGL.Tests;

using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Native;
using Axrone.Render.OpenGL.Resources;

/// <summary>
/// Tests for <see cref="GLFramebuffer"/> operations including attachment management,
/// completeness validation, clearing, blitting, and context-loss recovery.
/// </summary>
public sealed class GLFramebufferTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public GLFramebufferTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    [Fact]
    public void Constructor_CreatesFramebuffer()
    {
        var fbo = new GLFramebuffer(_context, 800, 600, "test_fbo");

        fbo.Id.Should().BeGreaterThan(0u);
        fbo.Width.Should().Be(800);
        fbo.Height.Should().Be(600);
        fbo.Label.Should().Be("test_fbo");
        fbo.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InvalidDimensions_Throws()
    {
        var action = () => new GLFramebuffer(_context, 0, 600);

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidValue);
    }

    [Fact]
    public void AttachColor_AttachesTexture()
    {
        var fbo = new GLFramebuffer(_context, 256, 256);
        var colorTex = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 256, 256);
        _mock.ClearCallLog();

        fbo.AttachColor(colorTex, 0);

        _mock.CallLog.Should().Contain(c => c.Contains("FramebufferTexture2D"));
        fbo.ColorAttachmentCount.Should().Be(1);
    }

    [Fact]
    public void AttachDepth_AttachesDepthTexture()
    {
        var fbo = new GLFramebuffer(_context, 256, 256);
        var depthTex = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Depth24, 256, 256);
        _mock.ClearCallLog();

        fbo.AttachDepth(depthTex);

        _mock.CallLog.Should().Contain(c => c.Contains("FramebufferTexture2D"));
        fbo.HasDepth.Should().BeTrue();
    }

    [Fact]
    public void AttachDepthStencil_AttachesTexture()
    {
        var fbo = new GLFramebuffer(_context, 256, 256);
        var dsTex = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Depth24Stencil8, 256, 256);
        _mock.ClearCallLog();

        fbo.AttachDepthStencil(dsTex);

        _mock.CallLog.Should().Contain(c => c.Contains("FramebufferTexture2D"));
        fbo.HasDepthStencil.Should().BeTrue();
    }

    [Fact]
    public void AttachDepth_WithNonDepthFormat_Throws()
    {
        var fbo = new GLFramebuffer(_context, 256, 256);
        var colorTex = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 256, 256);

        var action = () => fbo.AttachDepth(colorTex);

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidAttachment);
    }

    [Fact]
    public void ValidateCompleteness_ReturnsTrue_WhenComplete()
    {
        var fbo = new GLFramebuffer(_context, 256, 256);
        // MockGLApi.FramebufferStatus defaults to GL_FRAMEBUFFER_COMPLETE (0x8CD5)

        fbo.ValidateCompleteness().Should().BeTrue();
    }

    [Fact]
    public void EnsureComplete_ThrowsOnIncomplete()
    {
        var fbo = new GLFramebuffer(_context, 256, 256);
        _mock.FramebufferStatus = 0x8CD6; // GL_FRAMEBUFFER_INCOMPLETE

        var action = () => fbo.EnsureComplete();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.IncompleteFramebuffer);
    }

    [Fact]
    public void Clear_ClearsSelectedBuffers()
    {
        var fbo = new GLFramebuffer(_context, 256, 256);
        var colorTex = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 256, 256);
        fbo.AttachColor(colorTex, 0);
        _mock.ClearCallLog();

        fbo.Clear(color: true, depth: false, stencil: false);

        _mock.CallLog.Should().Contain(c => c.Contains("Clear"));
    }

    [Fact]
    public void BlitTo_BlitBetweenFramebuffers()
    {
        var src = new GLFramebuffer(_context, 256, 256, "src");
        var dst = new GLFramebuffer(_context, 256, 256, "dst");
        _mock.ClearCallLog();

        src.BlitTo(dst, 0, 0, 256, 256, 0, 0, 256, 256, GLConst.ColorBufferBit);

        _mock.CallLog.Should().Contain(c => c.Contains("BlitFramebuffer"));
    }

    [Fact]
    public void ContextLossRestore_GetsNewId()
    {
        var fbo = new GLFramebuffer(_context, 256, 256);
        uint originalId = fbo.Id;

        _context.NotifyContextLost();
        fbo.Id.Should().Be(0u);

        _context.NotifyContextRestored();
        fbo.Id.Should().BeGreaterThan(0u);
        fbo.Id.Should().NotBe(originalId);
    }

    [Fact]
    public void Dispose_SetsIdToZero_AndUnregisters()
    {
        var fbo = new GLFramebuffer(_context, 256, 256);
        int initialCount = _context.Registry.Count;

        fbo.Dispose();

        fbo.IsDisposed.Should().BeTrue();
        fbo.Id.Should().Be(0u);
        _context.Registry.Count.Should().Be(initialCount - 1);
    }

    [Fact]
    public void DisposedFramebuffer_AttachColor_Throws()
    {
        var fbo = new GLFramebuffer(_context, 256, 256);
        var colorTex = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 256, 256);
        fbo.Dispose();

        var action = () => fbo.AttachColor(colorTex, 0);

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidOperation);
    }

    [Fact]
    public void AttachColor_WithMismatchedSize_Throws()
    {
        var fbo = new GLFramebuffer(_context, 256, 256);
        var colorTex = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 128, 128);

        var action = () => fbo.AttachColor(colorTex, 0);

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidAttachment);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
