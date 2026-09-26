namespace Axrone.Render.OpenGL.Resources;

/// <summary>
/// GPU sampler object controlling texture sampling state (filtering, wrapping, LOD, comparison).
/// Samplers separate sampling state from texture objects for flexible reuse.
/// </summary>
public sealed class GLSampler : IGLResource, IDisposable
{
    private readonly GLContext _context;
    private readonly IGLApi _gl;
    private int _disposed;

    private uint _minFilter = GLConst.Linear;
    private uint _magFilter = GLConst.Linear;
    private uint _wrapS = GLConst.Repeat;
    private uint _wrapT = GLConst.Repeat;
    private uint _wrapR = GLConst.Repeat;
    private float _minLod = -1000.0f;
    private float _maxLod = 1000.0f;
    private float _lodBias;
    private uint _compareMode;
    private uint _compareFunc = GLConst.Lequal;
    private float _maxAnisotropy = 1.0f;
    private bool _compareEnabled;

    /// <summary>Gets the sampler handle.</summary>
    public uint Id { get; private set; }

    /// <summary>Gets the debug label.</summary>
    public string Label { get; }

    /// <inheritdoc/>
    public int RegistrySequence { get; set; }

    /// <inheritdoc/>
    public int RebuildPriority => 25;

    /// <summary>Gets a value indicating whether this sampler has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLSampler"/> class.
    /// </summary>
    public GLSampler(GLContext context, string label = "sampler")
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _gl = context.GL;
        Label = label;

        _context.AssertRenderThread();

        Id = _gl.GenSampler();
        _context.Registry.Register(this);
        Apply();

        if (_context.DebugLabelsEnabled)
        {
            _gl.ObjectLabel(GLConst.Sampler, Id, label);
        }
    }

    /// <summary>Sets min/mag filter modes.</summary>
    public GLSampler SetFilter(uint minFilter, uint magFilter)
    {
        _minFilter = minFilter;
        _magFilter = magFilter;
        return this;
    }

    /// <summary>Sets wrap modes for S, T, R coordinates.</summary>
    public GLSampler SetWrap(uint wrapS, uint wrapT, uint wrapR = GLConst.Repeat)
    {
        _wrapS = wrapS;
        _wrapT = wrapT;
        _wrapR = wrapR;
        return this;
    }

    /// <summary>Sets LOD range and bias.</summary>
    public GLSampler SetLod(float min, float max, float bias = 0)
    {
        _minLod = min;
        _maxLod = max;
        _lodBias = bias;
        return this;
    }

    /// <summary>Enables depth comparison mode.</summary>
    public GLSampler SetCompare(uint mode, uint func)
    {
        _compareMode = mode;
        _compareFunc = func;
        _compareEnabled = mode != GLConst.None;
        return this;
    }

    /// <summary>Sets maximum anisotropy level.</summary>
    public GLSampler SetAnisotropy(float maxAnisotropy)
    {
        _maxAnisotropy = maxAnisotropy;
        return this;
    }

    /// <summary>Applies all stored parameters to the GL sampler object.</summary>
    public void Apply()
    {
        if (Id == 0) return;

        _gl.SamplerParameter(Id, GLConst.TextureMinFilter, (int)_minFilter);
        _gl.SamplerParameter(Id, GLConst.TextureMagFilter, (int)_magFilter);
        _gl.SamplerParameter(Id, GLConst.TextureWrapS, (int)_wrapS);
        _gl.SamplerParameter(Id, GLConst.TextureWrapT, (int)_wrapT);
        _gl.SamplerParameter(Id, GLConst.TextureWrapR, (int)_wrapR);
        _gl.SamplerParameter(Id, GLConst.TextureMinLod, _minLod);
        _gl.SamplerParameter(Id, GLConst.TextureMaxLod, _maxLod);
        _gl.SamplerParameter(Id, GLConst.TextureLodBias, _lodBias);

        if (_compareEnabled)
        {
            _gl.SamplerParameter(Id, GLConst.TextureCompareMode, (int)_compareMode);
            _gl.SamplerParameter(Id, GLConst.TextureCompareFunc, (int)_compareFunc);
        }
    }

    /// <summary>Binds this sampler to a texture unit.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GLSampler Bind(uint unit)
    {
        EnsureAlive();
        _gl.BindSampler(unit, Id);
        return this;
    }

    /// <summary>Unbinds sampler from a texture unit.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GLSampler Unbind(uint unit)
    {
        _gl.BindSampler(unit, 0);
        return this;
    }

    /// <inheritdoc/>
    public void Invalidate() => Id = 0;

    /// <inheritdoc/>
    public void Rebuild() => OnContextRestored();

    /// <inheritdoc/>
    public void OnContextLost() => Id = 0;

    /// <inheritdoc/>
    public void OnContextRestored()
    {
        if (IsDisposed) return;

        Id = _gl.GenSampler();
        Apply();

        if (_context.DebugLabelsEnabled && !string.IsNullOrEmpty(Label))
        {
            _gl.ObjectLabel(GLConst.Sampler, Id, Label);
        }
    }

    private void EnsureAlive()
    {
        if (IsDisposed)
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "Sampler has been disposed", nameof(GLSampler));

        if (_context.Lifecycle.IsLost)
            ThrowHelper.Throw(RenderErrorCode.ContextLost, "GL context is lost", nameof(GLSampler));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        if (Id != 0)
        {
            _gl.DeleteSampler(Id);
            Id = 0;
        }

        _context.Registry.Unregister(this);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"GLSampler: Id={Id}, MinFilter={_minFilter}, MagFilter={_magFilter}, Label=\"{Label}\"";
}
