namespace Axrone.Render.OpenGL.Mock;

/// <summary>
/// Mock implementation of IGLApi for unit testing without a real GL context.
/// Provides call recording, configurable capabilities, and state tracking.
/// </summary>
public sealed unsafe class MockGLApi : IGLApi
{
    private uint _idCounter = 1;
    private int _locationCounter;
    private readonly ConcurrentDictionary<uint, byte[]> _bufferStorage = new();
    private readonly ConcurrentDictionary<string, int> _locations = new(StringComparer.Ordinal);
    private readonly HashSet<string> _extensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _callLog = new(256);

    /// <summary>
    /// Gets the call log for verification.
    /// </summary>
    public IReadOnlyList<string> CallLog => _callLog;

    /// <summary>
    /// Gets the buffer storage for verification.
    /// </summary>
    public IReadOnlyDictionary<uint, byte[]> BufferStorage => _bufferStorage;

    /// <summary>
    /// Gets or sets the maximum texture size capability.
    /// </summary>
    public int MaxTextureSize { get; set; } = 16384;

    /// <summary>
    /// Gets or sets the maximum vertex attributes capability.
    /// </summary>
    public int MaxVertexAttribs { get; set; } = 16;

    /// <summary>
    /// Gets or sets the maximum combined texture units capability.
    /// </summary>
    public int MaxCombinedTextureUnits { get; set; } = 32;

    /// <summary>
    /// Gets or sets the maximum samples capability.
    /// </summary>
    public int MaxSamples { get; set; } = 8;

    /// <summary>
    /// Gets or sets the framebuffer status for CheckFramebufferStatus.
    /// </summary>
    public uint FramebufferStatus { get; set; } = 0x8CD5; // GL_FRAMEBUFFER_COMPLETE

    /// <summary>
    /// Gets or sets a value indicating whether to log calls.
    /// </summary>
    public bool EnableCallLogging { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockGLApi"/> class.
    /// </summary>
    public MockGLApi()
    {
        _extensions.Add("GL_EXT_texture_compression_s3tc");
        _extensions.Add("GL_KHR_parallel_shader_compile");
        _extensions.Add("GL_KHR_debug");
        _extensions.Add("GL_ARB_texture_storage");
        _extensions.Add("GL_ARB_sampler_objects");
    }

    /// <summary>
    /// Clears the call log.
    /// </summary>
    public void ClearCallLog() => _callLog.Clear();

    /// <summary>
    /// Adds an extension to the supported list.
    /// </summary>
    public void AddExtension(string extension) => _extensions.Add(extension);

    private void Log(string call)
    {
        if (EnableCallLogging)
            _callLog.Add(call);
    }

    private uint NextId() => Interlocked.Increment(ref _idCounter);

    // ========================================================================
    // Buffer Operations
    // ========================================================================

    public uint GenBuffer()
    {
        uint id = NextId();
        Log($"GenBuffer() -> {id}");
        return id;
    }

    public void DeleteBuffer(uint buffer)
    {
        _bufferStorage.TryRemove(buffer, out _);
        Log($"DeleteBuffer({buffer})");
    }

    public void BindBuffer(uint target, uint buffer) => Log($"BindBuffer({target}, {buffer})");

    public void BufferData(uint target, nuint size, void* data, uint usage)
    {
        Log($"BufferData({target}, {size}, {usage})");
        if (data != null && size > 0)
        {
            byte[] arr = new byte[size];
            fixed (byte* dst = arr)
            {
                Buffer.MemoryCopy(data, dst, size, size);
            }
            _bufferStorage[_idCounter] = arr;
        }
    }

    public void BufferSubData(uint target, nint offset, nuint size, void* data) =>
        Log($"BufferSubData({target}, {offset}, {size})");

    public void CopyBufferSubData(uint readTarget, uint writeTarget, nint readOffset, nint writeOffset, nuint size) =>
        Log($"CopyBufferSubData({readTarget}, {writeTarget}, {readOffset}, {writeOffset}, {size})");

    public void GetBufferSubData(uint target, nint offset, nuint size, void* data)
    {
        Log($"GetBufferSubData({target}, {offset}, {size})");
        if (data != null && size > 0)
        {
            Unsafe.InitBlock(data, 0, (uint)size);
        }
    }

    // ========================================================================
    // Vertex Array Operations
    // ========================================================================

    public uint GenVertexArray()
    {
        uint id = NextId();
        Log($"GenVertexArray() -> {id}");
        return id;
    }

    public void DeleteVertexArray(uint array) => Log($"DeleteVertexArray({array})");
    public void BindVertexArray(uint array) => Log($"BindVertexArray({array})");
    public void EnableVertexAttribArray(uint index) => Log($"EnableVertexAttribArray({index})");
    public void DisableVertexAttribArray(uint index) => Log($"DisableVertexAttribArray({index})");

    public void VertexAttribPointer(uint index, int size, uint type, bool normalized, uint stride, void* pointer) =>
        Log($"VertexAttribPointer({index}, {size}, {type}, {normalized}, {stride}, {(nint)pointer})");

    public void VertexAttribIPointer(uint index, int size, uint type, uint stride, void* pointer) =>
        Log($"VertexAttribIPointer({index}, {size}, {type}, {stride}, {(nint)pointer})");

    public void VertexAttribDivisor(uint index, uint divisor) => Log($"VertexAttribDivisor({index}, {divisor})");

    // ========================================================================
    // Texture Operations
    // ========================================================================

    public uint GenTexture()
    {
        uint id = NextId();
        Log($"GenTexture() -> {id}");
        return id;
    }

    public void DeleteTexture(uint texture) => Log($"DeleteTexture({texture})");
    public void BindTexture(uint target, uint texture) => Log($"BindTexture({target}, {texture})");
    public void ActiveTexture(uint textureUnit) => Log($"ActiveTexture({textureUnit})");

    public void TexStorage2D(uint target, uint levels, uint internalFormat, uint width, uint height) =>
        Log($"TexStorage2D({target}, {levels}, {internalFormat}, {width}, {height})");

    public void TexStorage3D(uint target, uint levels, uint internalFormat, uint width, uint height, uint depth) =>
        Log($"TexStorage3D({target}, {levels}, {internalFormat}, {width}, {height}, {depth})");

    public void TexSubImage2D(uint target, int level, int xoffset, int yoffset, uint width, uint height, uint format, uint type, void* pixels) =>
        Log($"TexSubImage2D({target}, {level}, {xoffset}, {yoffset}, {width}, {height})");

    public void TexParameteri(uint target, uint pname, int param) => Log($"TexParameteri({target}, {pname}, {param})");
    public void TexParameterf(uint target, uint pname, float param) => Log($"TexParameterf({target}, {pname}, {param})");

    public void CompressedTexSubImage2D(uint target, int level, int xoffset, int yoffset, uint width, uint height, uint format, nuint imageSize, void* data) =>
        Log($"CompressedTexSubImage2D({target}, {level}, {xoffset}, {yoffset}, {width}, {height})");

    public void GetTexImage(uint target, int level, uint format, uint type, void* pixels) =>
        Log($"GetTexImage({target}, {level}, {format}, {type})");

    public void CopyTexSubImage2D(uint target, int level, int xoffset, int yoffset, int x, int y, uint width, uint height) =>
        Log($"CopyTexSubImage2D({target}, {level}, {xoffset}, {yoffset}, {x}, {y}, {width}, {height})");

    public void GenerateMipmap(uint target) => Log($"GenerateMipmap({target})");

    // ========================================================================
    // Sampler Operations
    // ========================================================================

    public uint GenSampler()
    {
        uint id = NextId();
        Log($"GenSampler() -> {id}");
        return id;
    }

    public void DeleteSampler(uint sampler) => Log($"DeleteSampler({sampler})");
    public void BindSampler(uint unit, uint sampler) => Log($"BindSampler({unit}, {sampler})");
    public void SamplerParameter(uint sampler, uint pname, int param) => Log($"SamplerParameter({sampler}, {pname}, {param})");
    public void SamplerParameter(uint sampler, uint pname, float param) => Log($"SamplerParameter({sampler}, {pname}, {param})");

    // ========================================================================
    // Framebuffer Operations
    // ========================================================================

    public uint GenFramebuffer()
    {
        uint id = NextId();
        Log($"GenFramebuffer() -> {id}");
        return id;
    }

    public void DeleteFramebuffer(uint framebuffer) => Log($"DeleteFramebuffer({framebuffer})");
    public void BindFramebuffer(uint target, uint framebuffer) => Log($"BindFramebuffer({target}, {framebuffer})");

    public void FramebufferTexture2D(uint target, uint attachment, uint textarget, uint texture, int level) =>
        Log($"FramebufferTexture2D({target}, {attachment}, {textarget}, {texture}, {level})");

    public void FramebufferRenderbuffer(uint target, uint attachment, uint renderbuffertarget, uint renderbuffer) =>
        Log($"FramebufferRenderbuffer({target}, {attachment}, {renderbuffertarget}, {renderbuffer})");

    public uint CheckFramebufferStatus(uint target)
    {
        Log($"CheckFramebufferStatus({target}) -> {FramebufferStatus}");
        return FramebufferStatus;
    }

    public void BlitFramebuffer(int srcX0, int srcY0, int srcX1, int srcY1, int dstX0, int dstY0, int dstX1, int dstY1, uint mask, uint filter) =>
        Log($"BlitFramebuffer({srcX0}, {srcY0}, {srcX1}, {srcY1}, {dstX0}, {dstY0}, {dstX1}, {dstY1}, {mask}, {filter})");

    public void DrawBuffers(ReadOnlySpan<uint> bufs) => Log($"DrawBuffers({bufs.Length})");
    public void ReadPixels(int x, int y, uint width, uint height, uint format, uint type, void* data) =>
        Log($"ReadPixels({x}, {y}, {width}, {height})");

    public void ReadBuffer(uint mode) => Log($"ReadBuffer({mode})");

    // ========================================================================
    // Renderbuffer Operations
    // ========================================================================

    public uint GenRenderbuffer()
    {
        uint id = NextId();
        Log($"GenRenderbuffer() -> {id}");
        return id;
    }

    public void DeleteRenderbuffer(uint renderbuffer) => Log($"DeleteRenderbuffer({renderbuffer})");
    public void BindRenderbuffer(uint target, uint renderbuffer) => Log($"BindRenderbuffer({target}, {renderbuffer})");

    public void RenderbufferStorage(uint target, uint internalformat, uint width, uint height) =>
        Log($"RenderbufferStorage({target}, {internalformat}, {width}, {height})");

    public void RenderbufferStorageMultisample(uint target, uint samples, uint internalformat, uint width, uint height) =>
        Log($"RenderbufferStorageMultisample({target}, {samples}, {internalformat}, {width}, {height})");

    // ========================================================================
    // Shader Operations
    // ========================================================================

    public uint CreateShader(uint type)
    {
        uint id = NextId();
        Log($"CreateShader({type}) -> {id}");
        return id;
    }

    public void DeleteShader(uint shader) => Log($"DeleteShader({shader})");
    public void ShaderSource(uint shader, string source) => Log($"ShaderSource({shader})");
    public void CompileShader(uint shader) => Log($"CompileShader({shader})");

    public void GetShader(uint shader, uint pname, out int parameters)
    {
        Log($"GetShader({shader}, {pname})");
        parameters = 1; // Success
    }

    public string GetShaderInfoLog(uint shader)
    {
        Log($"GetShaderInfoLog({shader})");
        return string.Empty;
    }

    // ========================================================================
    // Program Operations
    // ========================================================================

    public uint CreateProgram()
    {
        uint id = NextId();
        Log($"CreateProgram() -> {id}");
        return id;
    }

    public void DeleteProgram(uint program) => Log($"DeleteProgram({program})");
    public void AttachShader(uint program, uint shader) => Log($"AttachShader({program}, {shader})");
    public void DetachShader(uint program, uint shader) => Log($"DetachShader({program}, {shader})");
    public void LinkProgram(uint program) => Log($"LinkProgram({program})");
    public void UseProgram(uint program) => Log($"UseProgram({program})");

    public void GetProgram(uint program, uint pname, out int parameters)
    {
        Log($"GetProgram({program}, {pname})");
        parameters = 1; // Success
    }

    public string GetProgramInfoLog(uint program)
    {
        Log($"GetProgramInfoLog({program})");
        return string.Empty;
    }

    /// <summary>
    /// Deterministic location allocator: stable, ordinal-compared ids per name.
    /// Unlike string hashing, results are identical across processes and runs.
    /// </summary>
    private int AllocateLocation(string name) =>
        _locations.GetOrAdd(name, static (_, self) => Interlocked.Increment(ref self._locationCounter), this);

    public int GetUniformLocation(uint program, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        int loc = AllocateLocation("u:" + name);
        Log($"GetUniformLocation({program}, {name}) -> {loc}");
        return loc;
    }

    public int GetAttribLocation(uint program, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        int loc = AllocateLocation("a:" + name);
        Log($"GetAttribLocation({program}, {name}) -> {loc}");
        return loc;
    }

    public uint GetUniformBlockIndex(uint program, string uniformBlockName)
    {
        ArgumentNullException.ThrowIfNull(uniformBlockName);
        uint index = (uint)AllocateLocation("b:" + uniformBlockName);
        Log($"GetUniformBlockIndex({program}, {uniformBlockName}) -> {index}");
        return index;
    }

    public void UniformBlockBinding(uint program, uint uniformBlockIndex, uint uniformBlockBinding) =>
        Log($"UniformBlockBinding({program}, {uniformBlockIndex}, {uniformBlockBinding})");

    // ========================================================================
    // Uniform Operations
    // ========================================================================

    public void Uniform1(int location, int v0) => Log($"Uniform1({location}, {v0})");
    public void Uniform1(int location, float v0) => Log($"Uniform1({location}, {v0})");
    public void Uniform2(int location, float v0, float v1) => Log($"Uniform2({location}, {v0}, {v1})");
    public void Uniform3(int location, float v0, float v1, float v2) => Log($"Uniform3({location}, {v0}, {v1}, {v2})");
    public void Uniform4(int location, float v0, float v1, float v2, float v3) =>
        Log($"Uniform4({location}, {v0}, {v1}, {v2}, {v3})");

    public void UniformMatrix4(int location, uint count, bool transpose, float* value) =>
        Log($"UniformMatrix4({location}, {count}, {transpose})");

    // ========================================================================
    // State Operations
    // ========================================================================

    public void Enable(uint cap) => Log($"Enable({cap})");
    public void Disable(uint cap) => Log($"Disable({cap})");
    public void Viewport(int x, int y, uint width, uint height) => Log($"Viewport({x}, {y}, {width}, {height})");
    public void Scissor(int x, int y, uint width, uint height) => Log($"Scissor({x}, {y}, {width}, {height})");
    public void ClearColor(float red, float green, float blue, float alpha) => Log($"ClearColor({red}, {green}, {blue}, {alpha})");
    public void ClearDepth(double depth) => Log($"ClearDepth({depth})");
    public void ClearStencil(int s) => Log($"ClearStencil({s})");
    public void Clear(uint mask) => Log($"Clear({mask})");
    public void ColorMask(bool red, bool green, bool blue, bool alpha) => Log($"ColorMask({red}, {green}, {blue}, {alpha})");
    public void DepthMask(bool flag) => Log($"DepthMask({flag})");
    public void DepthFunc(uint func) => Log($"DepthFunc({func})");

    public void BlendFuncSeparate(uint srcRGB, uint dstRGB, uint srcAlpha, uint dstAlpha) =>
        Log($"BlendFuncSeparate({srcRGB}, {dstRGB}, {srcAlpha}, {dstAlpha})");

    public void BlendEquationSeparate(uint modeRGB, uint modeAlpha) =>
        Log($"BlendEquationSeparate({modeRGB}, {modeAlpha})");

    public void CullFace(uint mode) => Log($"CullFace({mode})");
    public void FrontFace(uint mode) => Log($"FrontFace({mode})");
    public void PixelStore(uint pname, int param) => Log($"PixelStore({pname}, {param})");
    public void LineWidth(float width) => Log($"LineWidth({width})");
    public void PolygonOffset(float factor, float units) => Log($"PolygonOffset({factor}, {units})");
    public void StencilFunc(uint func, int reference, uint mask) => Log($"StencilFunc({func}, {reference}, {mask})");
    public void StencilOp(uint sfail, uint dpfail, uint dppass) => Log($"StencilOp({sfail}, {dpfail}, {dppass})");
    public void StencilMask(uint mask) => Log($"StencilMask({mask})");

    // ========================================================================
    // Draw Operations
    // ========================================================================

    public void DrawArrays(uint mode, int first, uint count) => Log($"DrawArrays({mode}, {first}, {count})");

    public void DrawElements(uint mode, uint count, uint type, void* indices) =>
        Log($"DrawElements({mode}, {count}, {type})");

    public void DrawArraysInstanced(uint mode, int first, uint count, uint instancecount) =>
        Log($"DrawArraysInstanced({mode}, {first}, {count}, {instancecount})");

    public void DrawElementsInstanced(uint mode, uint count, uint type, void* indices, uint instancecount) =>
        Log($"DrawElementsInstanced({mode}, {count}, {type}, {instancecount})");

    // ========================================================================
    // Query Operations
    // ========================================================================

    public uint GenQuery()
    {
        uint id = NextId();
        Log($"GenQuery() -> {id}");
        return id;
    }

    public void DeleteQuery(uint query) => Log($"DeleteQuery({query})");
    public void BeginQuery(uint target, uint query) => Log($"BeginQuery({target}, {query})");
    public void EndQuery(uint target) => Log($"EndQuery({target})");

    public void GetQueryParameter(uint query, uint pname, out int parameters)
    {
        Log($"GetQueryParameter({query}, {pname})");
        parameters = 1;
    }

    // ========================================================================
    // Sync Operations
    // ========================================================================

    public nint FenceSync(uint condition, uint flags)
    {
        nint sync = (nint)NextId();
        Log($"FenceSync({condition}, {flags}) -> {sync}");
        return sync;
    }

    public void DeleteSync(nint sync) => Log($"DeleteSync({sync})");

    public void GetSync(nint sync, uint pname, out int parameters)
    {
        Log($"GetSync({sync}, {pname})");
        parameters = 0x9119; // GL_SIGNALED
    }

    public uint ClientWaitSync(nint sync, uint flags, ulong timeout)
    {
        Log($"ClientWaitSync({sync}, {flags}, {timeout})");
        return 0x9119; // GL_CONDITION_SATISFIED
    }

    public void WaitSync(nint sync, uint flags, ulong timeout) => Log($"WaitSync({sync}, {flags}, {timeout})");

    // ========================================================================
    // Transform Feedback Operations
    // ========================================================================

    public uint GenTransformFeedback()
    {
        uint id = NextId();
        Log($"GenTransformFeedback() -> {id}");
        return id;
    }

    public void DeleteTransformFeedback(uint transformFeedback) => Log($"DeleteTransformFeedback({transformFeedback})");
    public void BindTransformFeedback(uint target, uint transformFeedback) => Log($"BindTransformFeedback({target}, {transformFeedback})");
    public void TransformFeedbackVaryings(uint program, string[] varyings, uint bufferMode) =>
        Log($"TransformFeedbackVaryings({program}, [{string.Join(", ", varyings)}], {bufferMode})");

    public void BindBufferBase(uint target, uint index, uint buffer) => Log($"BindBufferBase({target}, {index}, {buffer})");
    public void BeginTransformFeedback(uint primitiveMode) => Log($"BeginTransformFeedback({primitiveMode})");
    public void PauseTransformFeedback() => Log("PauseTransformFeedback()");
    public void ResumeTransformFeedback() => Log("ResumeTransformFeedback()");
    public void EndTransformFeedback() => Log("EndTransformFeedback()");

    // ========================================================================
    // Information Operations
    // ========================================================================

    public void GetInteger(uint pname, out int data)
    {
        data = pname switch
        {
            0x0D33 => MaxTextureSize, // GL_MAX_TEXTURE_SIZE
            0x8869 => MaxVertexAttribs, // GL_MAX_VERTEX_ATTRIBS
            0x8B4D => MaxCombinedTextureUnits, // GL_MAX_COMBINED_TEXTURE_IMAGE_UNITS
            0x8D57 => MaxSamples, // GL_MAX_SAMPLES
            _ => 4096
        };
        Log($"GetInteger({pname}) -> {data}");
    }

    public string? GetString(uint name)
    {
        string result = name switch
        {
            0x1F00 => "Axrone Mock Vendor", // GL_VENDOR
            0x1F01 => "Axrone Mock Renderer", // GL_RENDERER
            0x1F02 => "4.6.0 Mock", // GL_VERSION
            0x8B8C => "4.60 Mock", // GL_SHADING_LANGUAGE_VERSION
            _ => string.Empty
        };
        Log($"GetString({name}) -> {result}");
        return result;
    }

    public string? GetString(uint name, uint index)
    {
        if (index < _extensions.Count)
        {
            string ext = _extensions.ElementAt((int)index);
            Log($"GetString({name}, {index}) -> {ext}");
            return ext;
        }
        Log($"GetString({name}, {index}) -> null");
        return null;
    }

    public uint GetError()
    {
        Log("GetError() -> 0 (GL_NO_ERROR)");
        return 0;
    }

    public void ObjectLabel(uint identifier, uint name, uint length, string label) =>
        Log($"ObjectLabel({identifier}, {name}, {label})");

    public void Flush() => Log("Flush()");
    public void Finish() => Log("Finish()");

    public void DispatchCompute(uint numGroupsX, uint numGroupsY, uint numGroupsZ) =>
        Log($"DispatchCompute({numGroupsX}, {numGroupsY}, {numGroupsZ})");

    public void MemoryBarrier(uint barriers) => Log($"MemoryBarrier({barriers})");

    public void BindImageTexture(uint unit, uint texture, int level, bool layered, int layer, uint access, uint format) =>
        Log($"BindImageTexture({unit}, {texture}, {level}, {layered}, {layer}, {access}, {format})");
}
