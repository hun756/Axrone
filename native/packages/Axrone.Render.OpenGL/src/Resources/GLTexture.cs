namespace Axrone.Render.OpenGL.Resources;

/// <summary>
/// GPU texture resource with immutable storage, mipmap support, and context-loss recovery.
/// Zero-allocation hot path, comprehensive format support.
/// </summary>
public sealed class GLTexture : IGLResource, IDisposable
{
    private readonly GLContext _context;
    private readonly IGLApi _gl;
    private int _disposed;

    /// <summary>Gets the texture handle.</summary>
    public uint Id { get; private set; }

    /// <summary>Gets the texture target.</summary>
    public uint Target { get; }

    /// <summary>Gets the texture format.</summary>
    public TextureFormat Format { get; }

    /// <summary>Gets the texture width.</summary>
    public int Width { get; }

    /// <summary>Gets the texture height.</summary>
    public int Height { get; }

    /// <summary>Gets the texture depth (for 3D/array textures).</summary>
    public int Depth { get; }

    /// <summary>Gets the number of mip levels.</summary>
    public int MipLevels { get; }

    /// <summary>Gets the debug label.</summary>
    public string Label { get; private set; }

    /// <inheritdoc/>
    public int RegistrySequence { get; set; }

    /// <inheritdoc/>
    public int RebuildPriority => 30;

    /// <summary>Gets a value indicating whether this texture has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Gets the format info for this texture.</summary>
    public TextureFormatInfo FormatInfo => TextureFormats.Get(Format);

    /// <summary>
    /// Initializes a new instance of the <see cref="GLTexture"/> class.
    /// </summary>
    public GLTexture(
        GLContext context,
        uint target,
        TextureFormat format,
        int width,
        int height,
        int depth = 1,
        int mipLevels = 1,
        string label = "texture")
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _gl = context.GL;
        Target = target;
        Format = format;
        Width = width;
        Height = height;
        Depth = depth;
        MipLevels = Math.Max(1, mipLevels);
        Label = label;

        if (width <= 0 || height <= 0)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidValue, "Texture dimensions must be positive", nameof(GLTexture));
        }

        if (depth <= 0)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidValue, "Texture depth must be positive", nameof(GLTexture));
        }

        Id = _gl.GenTexture();
        AllocateStorage();
        context.Registry.Register(this);

        if (context.DebugLabelsEnabled)
        {
            _gl.ObjectLabel(GLConst.Texture, Id, label);
        }
    }

    private void AllocateStorage()
    {
        var info = FormatInfo;
        _context.State.BindTexture(Target, Id);

        if (Target == GLConst.Texture2D)
        {
            _gl.TexStorage2D(Target, (uint)MipLevels, info.InternalFormat, (uint)Width, (uint)Height);
        }
        else if (Target == GLConst.Texture3D || Target == GLConst.Texture2DArray)
        {
            _gl.TexStorage3D(Target, (uint)MipLevels, info.InternalFormat, (uint)Width, (uint)Height, (uint)Depth);
        }
        else if (Target == GLConst.TextureCubeMap)
        {
            _gl.TexStorage2D(Target, (uint)MipLevels, info.InternalFormat, (uint)Width, (uint)Height);
        }

        // Set TEXTURE_MAX_LEVEL for mipmap filtering (REQ-TEX invariant)
        if (MipLevels > 1)
        {
            _gl.TexParameteri(Target, GLConst.TextureMaxLevel, MipLevels - 1);
            _gl.TexParameteri(Target, GLConst.TextureBaseLevel, 0);
        }
    }

    /// <summary>
    /// Uploads pixel data to a 2D texture sub-region.
    /// </summary>
    /// <param name="pixels">The pixel data.</param>
    /// <param name="x">The x offset.</param>
    /// <param name="y">The y offset.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="mipLevel">The mip level.</param>
    public unsafe void UploadSubData2D(ReadOnlySpan<byte> pixels, int x, int y, int width, int height, int mipLevel = 0)
    {
        EnsureAlive();
        _context.AssertRenderThread();

        var info = FormatInfo;

        if (info.IsCompressed)
        {
            ThrowHelper.Throw(RenderErrorCode.UnsupportedOperation, "Use UploadCompressedSubData2D for compressed textures", nameof(GLTexture));
        }

        _context.State.BindTexture(Target, Id);
        _context.State.SetUnpackAlignment(1); // REQ-TEX-9 fix

        fixed (byte* ptr = pixels)
        {
            _gl.TexSubImage2D(Target, mipLevel, x, y, (uint)width, (uint)height, info.Format, info.Type, ptr);
        }
    }

    /// <summary>
    /// Uploads compressed pixel data to a 2D texture.
    /// </summary>
    /// <param name="pixels">The compressed pixel data.</param>
    /// <param name="x">The x offset.</param>
    /// <param name="y">The y offset.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="mipLevel">The mip level.</param>
    public unsafe void UploadCompressedSubData2D(ReadOnlySpan<byte> pixels, int x, int y, int width, int height, int mipLevel = 0)
    {
        EnsureAlive();
        _context.AssertRenderThread();

        var info = FormatInfo;

        if (!info.IsCompressed)
        {
            ThrowHelper.Throw(RenderErrorCode.UnsupportedOperation, "Use UploadSubData2D for uncompressed textures", nameof(GLTexture));
        }

        _context.State.BindTexture(Target, Id);

        fixed (byte* ptr = pixels)
        {
            _gl.CompressedTexSubImage2D(Target, mipLevel, x, y, (uint)width, (uint)height, info.InternalFormat, (nuint)pixels.Length, ptr);
        }
    }

    /// <summary>
    /// Generates mipmaps.
    /// </summary>
    public void GenerateMipmaps()
    {
        EnsureAlive();
        _context.AssertRenderThread();

        if (MipLevels <= 1)
        {
            return;
        }

        _context.State.BindTexture(Target, Id);
        _gl.GenerateMipmap(Target);
    }

    /// <summary>
    /// Sets a texture parameter.
    /// </summary>
    /// <param name="pname">The parameter name.</param>
    /// <param name="param">The parameter value.</param>
    public void SetParameter(uint pname, int param)
    {
        EnsureAlive();
        _context.State.BindTexture(Target, Id);
        _gl.TexParameteri(Target, pname, param);
    }

    /// <summary>
    /// Sets a texture parameter.
    /// </summary>
    /// <param name="pname">The parameter name.</param>
    /// <param name="param">The parameter value.</param>
    public void SetParameter(uint pname, float param)
    {
        EnsureAlive();
        _context.State.BindTexture(Target, Id);
        _gl.TexParameterf(Target, pname, param);
    }

    /// <summary>
    /// Binds this texture to a texture unit.
    /// </summary>
    /// <param name="unit">The texture unit.</param>
    /// <returns>This texture for fluent chaining.</returns>
    public GLTexture Bind(int unit = 0)
    {
        EnsureAlive();
        _context.State.ActiveTexture((uint)unit);
        _context.State.BindTexture(Target, Id);
        return this;
    }

    /// <summary>
    /// Unbinds this texture from a texture unit.
    /// </summary>
    /// <param name="unit">The texture unit.</param>
    /// <returns>This texture for fluent chaining.</returns>
    public GLTexture Unbind(int unit = 0)
    {
        _context.State.ActiveTexture((uint)unit);
        _context.State.BindTexture(Target, 0);
        return this;
    }

    /// <summary>
    /// Reads pixel data from the texture.
    /// </summary>
    /// <param name="pixels">The output buffer.</param>
    /// <param name="mipLevel">The mip level.</param>
    public unsafe void GetImageData(Span<byte> pixels, int mipLevel = 0)
    {
        EnsureAlive();
        _context.AssertRenderThread();

        var info = FormatInfo;

        if (info.IsCompressed)
        {
            ThrowHelper.Throw(RenderErrorCode.UnsupportedOperation, "Cannot read back compressed textures", nameof(GLTexture));
        }

        _context.State.BindTexture(Target, Id);

        fixed (byte* ptr = pixels)
        {
            _gl.GetTexImage(Target, mipLevel, info.Format, info.Type, ptr);
        }
    }

    /// <summary>
    /// Copies a sub-region from another texture.
    /// </summary>
    /// <param name="source">The source texture.</param>
    /// <param name="x">The x offset.</param>
    /// <param name="y">The y offset.</param>
    /// <param name="srcX">The source x.</param>
    /// <param name="srcY">The source y.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="mipLevel">The mip level.</param>
    public void CopySubImage(GLTexture source, int x, int y, int srcX, int srcY, int width, int height, int mipLevel = 0)
    {
        EnsureAlive();
        _context.AssertRenderThread();

        _context.State.BindTexture(Target, Id);
        _gl.CopyTexSubImage2D(Target, mipLevel, x, y, srcX, srcY, (uint)width, (uint)height);
    }

    /// <inheritdoc/>
    public void OnContextLost()
    {
        Id = 0;
    }

    /// <inheritdoc/>
    public void Invalidate() => Id = 0;

    /// <inheritdoc/>
    public void Rebuild() => OnContextRestored();

    /// <inheritdoc/>
    public void OnContextRestored()
    {
        if (IsDisposed) return;

        Id = _gl.GenTexture();
        AllocateStorage();

        if (_context.DebugLabelsEnabled && !string.IsNullOrEmpty(Label))
        {
            _gl.ObjectLabel(GLConst.Texture, Id, Label);
        }
    }

    private void EnsureAlive()
    {
        if (IsDisposed)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "Texture has been disposed", nameof(GLTexture));
        }

        if (_context.Lifecycle.IsLost)
        {
            ThrowHelper.Throw(RenderErrorCode.ContextLost, "GL context is lost", nameof(GLTexture));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        if (Id != 0)
        {
            _gl.DeleteTexture(Id);
            Id = 0;
        }

        _context.Registry.Unregister(this);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"GLTexture: Id={Id}, Format={Format}, Size={Width}x{Height}x{Depth}, Mips={MipLevels}, Label=\"{Label}\"";
}
