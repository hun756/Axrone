namespace Axrone.Render.OpenGL.Tests;

using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Native;
using Axrone.Render.OpenGL.Resources;

/// <summary>
/// Tests for <see cref="GLTexture"/> operations including creation, sub-data upload,
/// mipmap generation, binding, disposal, and context-loss recovery.
/// </summary>
public sealed class GLTextureTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public GLTextureTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    [Fact]
    public void Constructor_CreatesTexture_WithCorrectProperties()
    {
        var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 256, 256);

        texture.Id.Should().BeGreaterThan(0u);
        texture.Width.Should().Be(256);
        texture.Height.Should().Be(256);
        texture.Format.Should().Be(TextureFormat.Rgba8);
        texture.Target.Should().Be(GLConst.Texture2D);
        texture.MipLevels.Should().Be(1);
        texture.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithZeroWidth_Throws()
    {
        var action = () => new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 0, 256);

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidValue);
    }

    [Fact]
    public void Constructor_WithNegativeHeight_Throws()
    {
        var action = () => new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 256, -1);

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidValue);
    }

    [Fact]
    public void UploadSubData2D_UploadsData()
    {
        var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 64, 64);
        _mock.ClearCallLog();

        byte[] pixels = new byte[64 * 64 * 4]; // RGBA8
        texture.UploadSubData2D(pixels, 0, 0, 64, 64);

        _mock.CallLog.Should().Contain(c => c.Contains("TexSubImage2D"));
    }

    [Fact]
    public void GenerateMipmaps_CallsGL_WhenMultipleMipLevels()
    {
        var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 256, 256, mipLevels: 4);
        _mock.ClearCallLog();

        texture.GenerateMipmaps();

        _mock.CallLog.Should().Contain(c => c.Contains("GenerateMipmap"));
    }

    [Fact]
    public void GenerateMipmaps_DoesNotCallGL_WhenSingleMipLevel()
    {
        var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 256, 256, mipLevels: 1);
        _mock.ClearCallLog();

        texture.GenerateMipmaps();

        _mock.CallLog.Should().NotContain(c => c.Contains("GenerateMipmap"));
    }

    [Fact]
    public void Bind_BindsToCorrectUnit()
    {
        var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 64, 64);
        _mock.ClearCallLog();

        texture.Bind(unit: 3);

        _mock.CallLog.Should().Contain(c => c.Contains("ActiveTexture"));
        _mock.CallLog.Should().Contain(c => c.Contains("BindTexture"));
    }

    [Fact]
    public void Unbind_BindsZeroToUnit()
    {
        var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 64, 64);
        _mock.ClearCallLog();

        texture.Unbind(unit: 0);

        _mock.CallLog.Should().Contain(c => c.Contains("BindTexture"));
    }

    [Fact]
    public void ContextLossRestore_GetsNewId()
    {
        var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 64, 64);
        uint originalId = texture.Id;

        _context.NotifyContextLost();
        texture.Id.Should().Be(0u);

        _context.NotifyContextRestored();
        texture.Id.Should().BeGreaterThan(0u);
        texture.Id.Should().NotBe(originalId);
    }

    [Fact]
    public void DisposedTexture_Bind_Throws()
    {
        var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 64, 64);
        texture.Dispose();

        var action = () => texture.Bind();

        action.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidOperation);
    }

    [Fact]
    public void DisposedTexture_UploadSubData2D_Throws()
    {
        var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 64, 64);
        texture.Dispose();

        byte[] pixels = new byte[4];
        var action = () => texture.UploadSubData2D(pixels, 0, 0, 1, 1);

        action.Should().Throw<RenderException>();
    }

    [Fact]
    public void Dispose_SetsIdToZero_AndUnregisters()
    {
        var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 64, 64);
        int initialCount = _context.Registry.Count;

        texture.Dispose();

        texture.IsDisposed.Should().BeTrue();
        texture.Id.Should().Be(0u);
        _context.Registry.Count.Should().Be(initialCount - 1);
    }

    [Fact]
    public void Constructor_WithDepth_HasCorrectDepth()
    {
        var texture = new GLTexture(_context, GLConst.Texture3D, TextureFormat.Rgba8, 64, 64, depth: 8);

        texture.Depth.Should().Be(8);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
