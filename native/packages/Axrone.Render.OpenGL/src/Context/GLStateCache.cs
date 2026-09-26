namespace Axrone.Render.OpenGL.Context;

/// <summary>
/// High-performance GL state cache that eliminates redundant GL calls.
/// Tracks all relevant GL state and only issues calls when state actually changes.
/// </summary>
public sealed class GLStateCache
{
    private readonly IGLApi _gl;
    private bool _isInvalidated;

    // Buffer bindings
    /// <summary>Cached GL state value.</summary>
     public uint BoundArrayBuffer { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint BoundElementBuffer { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint BoundUniformBuffer { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint BoundCopyReadBuffer { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint BoundCopyWriteBuffer { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint BoundPixelPackBuffer { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint BoundPixelUnpackBuffer { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint BoundTransformFeedbackBuffer { get; set; }

    // Vertex array
    /// <summary>Cached GL state value.</summary>
     public uint BoundVertexArray { get; set; }

    // Program
    /// <summary>Cached GL state value.</summary>
     public uint BoundProgram { get; set; }

    // Framebuffers
    /// <summary>Cached GL state value.</summary>
     public uint BoundReadFramebuffer { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint BoundDrawFramebuffer { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint BoundRenderbuffer { get; set; }

    // Textures and samplers (per unit)
    /// <summary>Cached GL state value.</summary>
     public uint ActiveUnit { get; set; }
    private readonly uint[] _boundTextures2D = new uint[32];
    private readonly uint[] _boundSamplers = new uint[32];

    // Viewport and scissor
    /// <summary>Cached GL state value.</summary>
     public int ViewportX { get; set; }
    /// <summary>Cached GL state value.</summary>
     public int ViewportY { get; set; }
    /// <summary>Cached GL state value.</summary>
     public int ViewportWidth { get; set; }
    /// <summary>Cached GL state value.</summary>
     public int ViewportHeight { get; set; }
    /// <summary>Cached GL state value.</summary>
     public int ScissorX { get; set; }
    /// <summary>Cached GL state value.</summary>
     public int ScissorY { get; set; }
    /// <summary>Cached GL state value.</summary>
     public int ScissorWidth { get; set; }
    /// <summary>Cached GL state value.</summary>
     public int ScissorHeight { get; set; }

    // Depth state
    /// <summary>Cached GL state value.</summary>
     public bool DepthTestEnabled { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint DepthFunc { get; set; }
    /// <summary>Cached GL state value.</summary>
     public bool DepthMask { get; set; }
    /// <summary>Cached GL state value.</summary>
     public double ClearDepth { get; set; }

    // Blend state
    /// <summary>Cached GL state value.</summary>
     public bool BlendEnabled { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint BlendSrcRGB { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint BlendDstRGB { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint BlendSrcAlpha { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint BlendDstAlpha { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint BlendEquationRGB { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint BlendEquationAlpha { get; set; }
    /// <summary>Cached GL state value.</summary>
     public float BlendColorR { get; set; }
    /// <summary>Cached GL state value.</summary>
     public float BlendColorG { get; set; }
    /// <summary>Cached GL state value.</summary>
     public float BlendColorB { get; set; }
    /// <summary>Cached GL state value.</summary>
     public float BlendColorA { get; set; }

    // Stencil state (per face)
    /// <summary>Cached GL state value.</summary>
     public bool StencilTestEnabled { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint StencilFuncFront { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint StencilFuncBack { get; set; }
    /// <summary>Cached GL state value.</summary>
     public int StencilRefFront { get; set; }
    /// <summary>Cached GL state value.</summary>
     public int StencilRefBack { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint StencilMaskFront { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint StencilMaskBack { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint StencilSfailFront { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint StencilDpfailFront { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint StencilDppassFront { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint StencilSfailBack { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint StencilDpfailBack { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint StencilDppassBack { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint StencilWriteMask { get; set; }
    /// <summary>Cached GL state value.</summary>
     public int ClearStencil { get; set; }

    // Cull state
    /// <summary>Cached GL state value.</summary>
     public bool CullFaceEnabled { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint CullMode { get; set; }
    /// <summary>Cached GL state value.</summary>
     public uint FrontFace { get; set; }

    // Color state
    /// <summary>Cached GL state value.</summary>
     public bool ColorMaskR { get; set; }
    /// <summary>Cached GL state value.</summary>
     public bool ColorMaskG { get; set; }
    /// <summary>Cached GL state value.</summary>
     public bool ColorMaskB { get; set; }
    /// <summary>Cached GL state value.</summary>
     public bool ColorMaskA { get; set; }
    /// <summary>Cached GL state value.</summary>
     public float ClearColorR { get; set; }
    /// <summary>Cached GL state value.</summary>
     public float ClearColorG { get; set; }
    /// <summary>Cached GL state value.</summary>
     public float ClearColorB { get; set; }
    /// <summary>Cached GL state value.</summary>
     public float ClearColorA { get; set; }

    // Polygon offset
    /// <summary>Cached GL state value.</summary>
     public bool PolygonOffsetFillEnabled { get; set; }
    /// <summary>Cached GL state value.</summary>
     public float PolygonOffsetFactor { get; set; }
    /// <summary>Cached GL state value.</summary>
     public float PolygonOffsetUnits { get; set; }

    // Line width
    /// <summary>Cached GL state value.</summary>
     public float LineWidth { get; set; }

    // Pixel store
    /// <summary>Cached GL state value.</summary>
     public int UnpackAlignment { get; set; }
    /// <summary>Cached GL state value.</summary>
     public int PackAlignment { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GLStateCache"/> class.
    /// </summary>
    /// <param name="gl">The GL API.</param>
    public GLStateCache(IGLApi gl)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
        Invalidate();
    }

    /// <summary>
    /// Invalidates all cached state, forcing the next operation to issue GL calls.
    /// </summary>
    public void Invalidate()
    {
        _isInvalidated = true;

        // Set all cached values to sentinel values that won't match real state
        BoundArrayBuffer = uint.MaxValue;
        BoundElementBuffer = uint.MaxValue;
        BoundUniformBuffer = uint.MaxValue;
        BoundCopyReadBuffer = uint.MaxValue;
        BoundCopyWriteBuffer = uint.MaxValue;
        BoundPixelPackBuffer = uint.MaxValue;
        BoundPixelUnpackBuffer = uint.MaxValue;
        BoundTransformFeedbackBuffer = uint.MaxValue;
        BoundVertexArray = uint.MaxValue;
        BoundProgram = uint.MaxValue;
        BoundReadFramebuffer = uint.MaxValue;
        BoundDrawFramebuffer = uint.MaxValue;
        BoundRenderbuffer = uint.MaxValue;
        ActiveUnit = uint.MaxValue;

        Array.Fill(_boundTextures2D, uint.MaxValue);
        Array.Fill(_boundSamplers, uint.MaxValue);

        ViewportX = ViewportY = ViewportWidth = ViewportHeight = -1;
        ScissorX = ScissorY = ScissorWidth = ScissorHeight = -1;

        DepthTestEnabled = false;
        DepthFunc = uint.MaxValue;
        DepthMask = true;
        ClearDepth = -1.0;

        BlendEnabled = false;
        BlendSrcRGB = BlendDstRGB = BlendSrcAlpha = BlendDstAlpha = uint.MaxValue;
        BlendEquationRGB = BlendEquationAlpha = uint.MaxValue;
        BlendColorR = BlendColorG = BlendColorB = BlendColorA = -1.0f;

        StencilTestEnabled = false;
        StencilFuncFront = StencilFuncBack = uint.MaxValue;
        StencilRefFront = StencilRefBack = -1;
        StencilMaskFront = StencilMaskBack = uint.MaxValue;
        StencilSfailFront = StencilDpfailFront = StencilDppassFront = uint.MaxValue;
        StencilSfailBack = StencilDpfailBack = StencilDppassBack = uint.MaxValue;
        StencilWriteMask = uint.MaxValue;
        ClearStencil = -1;

        CullFaceEnabled = false;
        CullMode = uint.MaxValue;
        FrontFace = uint.MaxValue;

        ColorMaskR = ColorMaskG = ColorMaskB = ColorMaskA = true;
        ClearColorR = ClearColorG = ClearColorB = ClearColorA = -1.0f;

        PolygonOffsetFillEnabled = false;
        PolygonOffsetFactor = PolygonOffsetUnits = float.NaN;

        LineWidth = -1.0f;

        UnpackAlignment = -1;
        PackAlignment = -1;
    }

    /// <summary>
    /// Resets the cache to GL defaults.
    /// </summary>
    public void Reset()
    {
        Invalidate();
        _isInvalidated = false;
    }

    // ========================================================================
    // Buffer Binding Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindBuffer(uint target, uint buffer)
    {
        // General-purpose buffer bind — dispatches to the correct cached target
        if (target == 0x8892) { BindArrayBuffer(buffer); return; } // GL_ARRAY_BUFFER
        if (target == 0x8893) { BindElementBuffer(buffer); return; } // GL_ELEMENT_ARRAY_BUFFER
        if (target == 0x8A11) { BindUniformBuffer(buffer); return; } // GL_UNIFORM_BUFFER
        if (target == 0x8F36) { BindCopyReadBuffer(buffer); return; } // GL_COPY_READ_BUFFER
        if (target == 0x8F37) { BindCopyWriteBuffer(buffer); return; } // GL_COPY_WRITE_BUFFER

        // Fallback: uncached direct call for less common targets
        _gl.BindBuffer(target, buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindArrayBuffer(uint buffer)
    {
        if (_isInvalidated || BoundArrayBuffer != buffer)
        {
            BoundArrayBuffer = buffer;
            _gl.BindBuffer(0x8892, buffer); // GL_ARRAY_BUFFER
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindElementBuffer(uint buffer)
    {
        if (_isInvalidated || BoundElementBuffer != buffer)
        {
            BoundElementBuffer = buffer;
            _gl.BindBuffer(0x8893, buffer); // GL_ELEMENT_ARRAY_BUFFER
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindUniformBuffer(uint buffer)
    {
        if (_isInvalidated || BoundUniformBuffer != buffer)
        {
            BoundUniformBuffer = buffer;
            _gl.BindBuffer(0x8A11, buffer); // GL_UNIFORM_BUFFER
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindCopyReadBuffer(uint buffer)
    {
        if (_isInvalidated || BoundCopyReadBuffer != buffer)
        {
            BoundCopyReadBuffer = buffer;
            _gl.BindBuffer(0x8F36, buffer); // GL_COPY_READ_BUFFER
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindCopyWriteBuffer(uint buffer)
    {
        if (_isInvalidated || BoundCopyWriteBuffer != buffer)
        {
            BoundCopyWriteBuffer = buffer;
            _gl.BindBuffer(0x8F37, buffer); // GL_COPY_WRITE_BUFFER
        }
    }

    // ========================================================================
    // Vertex Array Operations
    // ========================================================================

}
