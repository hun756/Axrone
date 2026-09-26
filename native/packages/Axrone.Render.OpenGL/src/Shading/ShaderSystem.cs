using System.Collections.Frozen;

namespace Axrone.Render.OpenGL.Shading;

/// <summary>
/// High-performance shader program with compilation, linking, and reflection.
/// </summary>
public sealed class GLProgram : IGLResource, IDisposable
{
    private readonly GLContext _context;
    private int _isDisposed;
    private FrozenDictionary<string, int>? _uniformLocations;
    private FrozenDictionary<string, int>? _attributeLocations;

    /// <summary>
    /// Gets the program ID.
    /// </summary>
    public uint Id { get; private set; }

    /// <summary>
    /// Gets the vertex shader source.
    /// </summary>
    public string VertexSource { get; }

    /// <summary>
    /// Gets the fragment shader source.
    /// </summary>
    public string FragmentSource { get; }

    /// <summary>
    /// Gets a value indicating whether the program has been disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

    /// <inheritdoc/>
    public int RegistrySequence { get; set; }

    /// <inheritdoc/>
    public int RebuildPriority => 15;

    /// <summary>
    /// Gets the uniform locations (cached after first access).
    /// </summary>
    public FrozenDictionary<string, int> UniformLocations
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            _uniformLocations ??= ReflectUniforms();
            return _uniformLocations;
        }
    }

    /// <summary>
    /// Gets the attribute locations (cached after first access).
    /// </summary>
    public FrozenDictionary<string, int> AttributeLocations
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            _attributeLocations ??= ReflectAttributes();
            return _attributeLocations;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GLProgram"/> class.
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="vertexSource">The vertex shader source.</param>
    /// <param name="fragmentSource">The fragment shader source.</param>
    public GLProgram(GLContext context, string vertexSource, string fragmentSource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(vertexSource);
        ArgumentNullException.ThrowIfNull(fragmentSource);

        _context = context;
        VertexSource = vertexSource;
        FragmentSource = fragmentSource;

        Rebuild();
        _context.Registry.Register(this);
    }

    /// <summary>
    /// Gets the location of a uniform.
    /// </summary>
    /// <param name="name">The uniform name.</param>
    /// <returns>The uniform location, or -1 if not found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetUniformLocation(string name)
    {
        return UniformLocations.TryGetValue(name, out int location) ? location : -1;
    }

    /// <summary>
    /// Gets the location of an attribute.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <returns>The attribute location, or -1 if not found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetAttribLocation(string name)
    {
        return AttributeLocations.TryGetValue(name, out int location) ? location : -1;
    }

    private FrozenDictionary<string, int> ReflectUniforms()
    {
        if (IsDisposed)
            ThrowHelper.ThrowProgramDisposed();

        _context.GL.GetProgram(Id, GLConst.ActiveUniforms, out int count);
        if (count <= 0)
        {
            return new Dictionary<string, int>().ToFrozenDictionary();
        }

        _context.GL.GetProgram(Id, GLConst.ActiveUniformMaxLength, out int maxLength);
        if (maxLength <= 0)
        {
            maxLength = 1;
        }

        var locations = new Dictionary<string, int>(count, StringComparer.Ordinal);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            for (uint i = 0; (int)i < count; i++)
            {
                _context.GL.GetActiveUniform(Id, i, buffer.AsSpan(0, maxLength), out int length, out int size, out uint type);
                if (length <= 0)
                {
                    continue;
                }

                string name = System.Text.Encoding.UTF8.GetString(buffer.AsSpan(0, length));
                int location = _context.GL.GetUniformLocation(Id, name);
                locations[name] = location;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return locations.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private FrozenDictionary<string, int> ReflectAttributes()
    {
        if (IsDisposed)
            ThrowHelper.ThrowProgramDisposed();

        _context.GL.GetProgram(Id, GLConst.ActiveAttributes, out int count);
        if (count <= 0)
        {
            return new Dictionary<string, int>().ToFrozenDictionary();
        }

        _context.GL.GetProgram(Id, GLConst.ActiveAttributeMaxLength, out int maxLength);
        if (maxLength <= 0)
        {
            maxLength = 1;
        }

        var locations = new Dictionary<string, int>(count, StringComparer.Ordinal);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLength);
        try
        {
            for (uint i = 0; (int)i < count; i++)
            {
                _context.GL.GetActiveAttrib(Id, i, buffer.AsSpan(0, maxLength), out int length, out int size, out uint type);
                if (length <= 0)
                {
                    continue;
                }

                string name = System.Text.Encoding.UTF8.GetString(buffer.AsSpan(0, length));
                int location = _context.GL.GetAttribLocation(Id, name);
                locations[name] = location;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return locations.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public void Invalidate() => Id = 0;

    /// <inheritdoc/>
    public void Rebuild()
    {
        if (IsDisposed)
            return;

        // Compile vertex shader
        uint vs = _context.GL.CreateShader(GLConst.VertexShader);
        _context.GL.ShaderSource(vs, VertexSource);
        _context.GL.CompileShader(vs);
        _context.GL.GetShader(vs, GLConst.CompileStatus, out int vsSuccess);

        if (vsSuccess == 0)
        {
            string log = _context.GL.GetShaderInfoLog(vs);
            _context.GL.DeleteShader(vs);
            ThrowHelper.ThrowShaderCompileFailed(log);
        }

        // Compile fragment shader
        uint fs = _context.GL.CreateShader(GLConst.FragmentShader);
        _context.GL.ShaderSource(fs, FragmentSource);
        _context.GL.CompileShader(fs);
        _context.GL.GetShader(fs, GLConst.CompileStatus, out int fsSuccess);

        if (fsSuccess == 0)
        {
            string log = _context.GL.GetShaderInfoLog(fs);
            _context.GL.DeleteShader(fs);
            _context.GL.DeleteShader(vs);
            ThrowHelper.ThrowShaderCompileFailed(log);
        }

        // Link program
        Id = _context.GL.CreateProgram();
        _context.GL.AttachShader(Id, vs);
        _context.GL.AttachShader(Id, fs);
        _context.GL.LinkProgram(Id);

        _context.GL.GetProgram(Id, GLConst.LinkStatus, out int linkSuccess);

        // Detach and delete shaders
        _context.GL.DetachShader(Id, vs);
        _context.GL.DetachShader(Id, fs);
        _context.GL.DeleteShader(vs);
        _context.GL.DeleteShader(fs);

        if (linkSuccess == 0)
        {
            string log = _context.GL.GetProgramInfoLog(Id);
            _context.GL.DeleteProgram(Id);
            Id = 0;
            ThrowHelper.ThrowShaderLinkFailed(log);
        }

        // Clear cached reflection data
        _uniformLocations = null;
        _attributeLocations = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            if (Id != 0)
            {
                _context.GL.DeleteProgram(Id);
                Id = 0;
            }

            _uniformLocations = null;
            _attributeLocations = null;
            _context.Registry.Unregister(this);
        }
    }

    /// <inheritdoc/>
    public void OnContextLost() => Id = 0;

    /// <inheritdoc/>
    public void OnContextRestored() => Rebuild();

    /// <inheritdoc/>
    public override string ToString() => $"GLProgram: Id={Id}";
}

/// <summary>
/// High-performance uniform cache using open-addressed hash table.
/// Eliminates redundant uniform uploads by tracking last-set values.
/// </summary>
public sealed class UniformCache
{
    private const int Capacity = 8192;
    private const int Mask = Capacity - 1;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4)]
    private struct Entry
    {
        public uint ProgramId;
        public int Location;
        public uint ValueHash;
    }

    private readonly Entry[] _entries = new Entry[Capacity];

    /// <summary>
    /// Checks if a uniform value has changed and updates the cache.
    /// </summary>
    /// <param name="programId">The program ID.</param>
    /// <param name="location">The uniform location.</param>
    /// <param name="valueHash">The hash of the value.</param>
    /// <returns>True if the value changed (upload needed); false if unchanged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CheckAndSet(uint programId, int location, uint valueHash)
    {
        int index = (int)((((uint)location * 397) ^ programId) & Mask);
        ref var entry = ref _entries[index];

        if (entry.ProgramId == programId && entry.Location == location && entry.ValueHash == valueHash)
            return false;

        entry.ProgramId = programId;
        entry.Location = location;
        entry.ValueHash = valueHash;
        return true;
    }

    /// <summary>
    /// Clears the cache.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => Array.Clear(_entries);
}

/// <summary>
/// Shader instance with uniform setting and caching.
/// </summary>
public sealed class ShaderInstance
{
    private readonly GLContext _context;
    private readonly UniformCache _cache;

    /// <summary>
    /// Gets the program.
    /// </summary>
    public GLProgram Program { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShaderInstance"/> class.
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="program">The program.</param>
    /// <param name="cache">The uniform cache.</param>
    public ShaderInstance(GLContext context, GLProgram program, UniformCache cache)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(cache);

        _context = context;
        Program = program;
        _cache = cache;
    }

    /// <summary>
    /// Sets a float uniform.
    /// </summary>
    /// <param name="location">The uniform location.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetFloat(int location, float value)
    {
        if (location < 0)
            return;

        uint hash = (uint)BitConverter.SingleToInt32Bits(value);
        if (_cache.CheckAndSet(Program.Id, location, hash))
        {
            _context.GL.Uniform1(location, value);
        }
    }

    /// <summary>
    /// Sets an integer uniform.
    /// </summary>
    /// <param name="location">The uniform location.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetInt(int location, int value)
    {
        if (location < 0)
            return;

        if (_cache.CheckAndSet(Program.Id, location, (uint)value))
        {
            _context.GL.Uniform1(location, value);
        }
    }

    /// <summary>
    /// Sets a mat4 uniform.
    /// </summary>
    /// <param name="location">The uniform location.</param>
    /// <param name="matrix">The matrix.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void SetMatrix4x4(int location, in System.Numerics.Matrix4x4 matrix)
    {
        if (location < 0)
            return;

        fixed (System.Numerics.Matrix4x4* ptr = &matrix)
        {
            float* f = (float*)ptr;

            // Compute FNV-1a hash
            uint hash = 2166136261u;
            for (int i = 0; i < 16; i++)
            {
                hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(f[i])) * 16777619u;
            }

            if (_cache.CheckAndSet(Program.Id, location, hash))
            {
                _context.GL.UniformMatrix4(location, 1, false, f);
            }
        }
    }

    /// <summary>
    /// Binds the program.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Bind()
    {
        _context.State.UseProgram(Program.Id);
    }

    /// <summary>
    /// Unbinds the program.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unbind()
    {
        _context.State.UseProgram(0);
    }
}
