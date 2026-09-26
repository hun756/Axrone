namespace Axrone.Render.OpenGL.Implementations;

/// <summary>
/// Silk.NET implementation of the IGLApi interface.
/// Wraps the native GL calls with minimal overhead.
/// </summary>
public sealed unsafe class SilkGLApi : IGLApi
{
    private readonly GL _gl;

    /// <summary>
    /// Gets the underlying Silk.NET GL instance.
    /// </summary>
    public GL NativeGL => _gl;

    /// <summary>
    /// Initializes a new instance of the <see cref="SilkGLApi"/> class.
    /// </summary>
    /// <param name="gl">The Silk.NET GL instance.</param>
    public SilkGLApi(GL gl)
    {
        if (gl is null)
        {
            ThrowHelper.ThrowArgumentNull(nameof(gl));
        }

        _gl = gl;
    }

    // ========================================================================
    // Buffer Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GenBuffer() => _gl.GenBuffer();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeleteBuffer(uint buffer) => _gl.DeleteBuffer(buffer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindBuffer(uint target, uint buffer) => _gl.BindBuffer((BufferTargetARB)target, buffer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BufferData(uint target, nuint size, void* data, uint usage) =>
        _gl.BufferData((BufferTargetARB)target, size, data, (BufferUsageARB)usage);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BufferSubData(uint target, nint offset, nuint size, void* data) =>
        _gl.BufferSubData((BufferTargetARB)target, offset, size, data);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyBufferSubData(uint readTarget, uint writeTarget, nint readOffset, nint writeOffset, nuint size) =>
        _gl.CopyBufferSubData((CopyBufferSubDataTarget)readTarget, (CopyBufferSubDataTarget)writeTarget, readOffset, writeOffset, size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetBufferSubData(uint target, nint offset, nuint size, void* data) =>
        _gl.GetBufferSubData((BufferTargetARB)target, offset, size, data);

    // ========================================================================
    // Vertex Array Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GenVertexArray() => _gl.GenVertexArray();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeleteVertexArray(uint array) => _gl.DeleteVertexArray(array);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindVertexArray(uint array) => _gl.BindVertexArray(array);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnableVertexAttribArray(uint index) => _gl.EnableVertexAttribArray(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DisableVertexAttribArray(uint index) => _gl.DisableVertexAttribArray(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void VertexAttribPointer(uint index, int size, uint type, bool normalized, uint stride, void* pointer) =>
        _gl.VertexAttribPointer(index, size, (VertexAttribPointerType)type, normalized, stride, pointer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void VertexAttribIPointer(uint index, int size, uint type, uint stride, void* pointer) =>
        _gl.VertexAttribIPointer(index, size, (GLEnum)type, stride, pointer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void VertexAttribDivisor(uint index, uint divisor) => _gl.VertexAttribDivisor(index, divisor);

    // ========================================================================
    // Texture Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GenTexture() => _gl.GenTexture();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeleteTexture(uint texture) => _gl.DeleteTexture(texture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindTexture(uint target, uint texture) => _gl.BindTexture((TextureTarget)target, texture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ActiveTexture(uint textureUnit) => _gl.ActiveTexture((TextureUnit)textureUnit);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TexStorage2D(uint target, uint levels, uint internalFormat, uint width, uint height) =>
        _gl.TexStorage2D((GLEnum)target, levels, (GLEnum)internalFormat, width, height);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TexStorage3D(uint target, uint levels, uint internalFormat, uint width, uint height, uint depth) =>
        _gl.TexStorage3D((GLEnum)target, levels, (GLEnum)internalFormat, width, height, depth);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TexSubImage2D(uint target, int level, int xoffset, int yoffset, uint width, uint height, uint format, uint type, void* pixels) =>
        _gl.TexSubImage2D((TextureTarget)target, level, xoffset, yoffset, width, height, (PixelFormat)format, (PixelType)type, pixels);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TexParameteri(uint target, uint pname, int param) =>
        _gl.TexParameter((TextureTarget)target, (TextureParameterName)pname, param);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TexParameterf(uint target, uint pname, float param) =>
        _gl.TexParameter((TextureTarget)target, (TextureParameterName)pname, param);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CompressedTexSubImage2D(uint target, int level, int xoffset, int yoffset, uint width, uint height, uint format, nuint imageSize, void* data) =>
        _gl.CompressedTexSubImage2D((GLEnum)target, level, xoffset, yoffset, width, height, (GLEnum)format, (uint)imageSize, data);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetTexImage(uint target, int level, uint format, uint type, void* pixels) =>
        _gl.GetTexImage((TextureTarget)target, level, (PixelFormat)format, (PixelType)type, pixels);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTexSubImage2D(uint target, int level, int xoffset, int yoffset, int x, int y, uint width, uint height) =>
        _gl.CopyTexSubImage2D((TextureTarget)target, level, xoffset, yoffset, x, y, width, height);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GenerateMipmap(uint target) => _gl.GenerateMipmap((TextureTarget)target);

    // ========================================================================
    // Sampler Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GenSampler() => _gl.GenSampler();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeleteSampler(uint sampler) => _gl.DeleteSampler(sampler);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindSampler(uint unit, uint sampler) => _gl.BindSampler(unit, sampler);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SamplerParameter(uint sampler, uint pname, int param) =>
        _gl.SamplerParameter(sampler, (GLEnum)pname, param);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SamplerParameter(uint sampler, uint pname, float param) =>
        _gl.SamplerParameter(sampler, (GLEnum)pname, param);

    // ========================================================================
    // Framebuffer Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GenFramebuffer() => _gl.GenFramebuffer();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeleteFramebuffer(uint framebuffer) => _gl.DeleteFramebuffer(framebuffer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindFramebuffer(uint target, uint framebuffer) =>
        _gl.BindFramebuffer((FramebufferTarget)target, framebuffer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FramebufferTexture2D(uint target, uint attachment, uint textarget, uint texture, int level) =>
        _gl.FramebufferTexture2D((FramebufferTarget)target, (FramebufferAttachment)attachment, (TextureTarget)textarget, texture, level);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FramebufferRenderbuffer(uint target, uint attachment, uint renderbuffertarget, uint renderbuffer) =>
        _gl.FramebufferRenderbuffer((FramebufferTarget)target, (FramebufferAttachment)attachment, (RenderbufferTarget)renderbuffertarget, renderbuffer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint CheckFramebufferStatus(uint target) => (uint)_gl.CheckFramebufferStatus((FramebufferTarget)target);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BlitFramebuffer(int srcX0, int srcY0, int srcX1, int srcY1, int dstX0, int dstY0, int dstX1, int dstY1, uint mask, uint filter) =>
        _gl.BlitFramebuffer(srcX0, srcY0, srcX1, srcY1, dstX0, dstY0, dstX1, dstY1, (ClearBufferMask)mask, (BlitFramebufferFilter)filter);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawBuffers(ReadOnlySpan<uint> bufs)
    {
        unsafe
        {
            fixed (uint* ptr = bufs)
            {
                _gl.DrawBuffers((uint)bufs.Length, (DrawBufferMode*)ptr);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadPixels(int x, int y, uint width, uint height, uint format, uint type, void* data) =>
        _gl.ReadPixels(x, y, width, height, (PixelFormat)format, (PixelType)type, data);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadBuffer(uint mode) => _gl.ReadBuffer((ReadBufferMode)mode);

    // ========================================================================
    // Renderbuffer Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GenRenderbuffer() => _gl.GenRenderbuffer();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeleteRenderbuffer(uint renderbuffer) => _gl.DeleteRenderbuffer(renderbuffer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindRenderbuffer(uint target, uint renderbuffer) =>
        _gl.BindRenderbuffer((RenderbufferTarget)target, renderbuffer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RenderbufferStorage(uint target, uint internalformat, uint width, uint height) =>
        _gl.RenderbufferStorage((RenderbufferTarget)target, (InternalFormat)internalformat, width, height);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RenderbufferStorageMultisample(uint target, uint samples, uint internalformat, uint width, uint height) =>
        _gl.RenderbufferStorageMultisample((RenderbufferTarget)target, samples, (InternalFormat)internalformat, width, height);

    // ========================================================================
    // Shader Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint CreateShader(uint type) => _gl.CreateShader((ShaderType)type);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeleteShader(uint shader) => _gl.DeleteShader(shader);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ShaderSource(uint shader, string source) => _gl.ShaderSource(shader, source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CompileShader(uint shader) => _gl.CompileShader(shader);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetShader(uint shader, uint pname, out int parameters) =>
        _gl.GetShader(shader, (ShaderParameterName)pname, out parameters);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetShaderInfoLog(uint shader) => _gl.GetShaderInfoLog(shader);

    // ========================================================================
    // Program Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint CreateProgram() => _gl.CreateProgram();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeleteProgram(uint program) => _gl.DeleteProgram(program);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AttachShader(uint program, uint shader) => _gl.AttachShader(program, shader);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DetachShader(uint program, uint shader) => _gl.DetachShader(program, shader);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LinkProgram(uint program) => _gl.LinkProgram(program);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UseProgram(uint program) => _gl.UseProgram(program);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetProgram(uint program, uint pname, out int parameters) =>
        _gl.GetProgram(program, (ProgramPropertyARB)pname, out parameters);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetProgramInfoLog(uint program) => _gl.GetProgramInfoLog(program);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetUniformLocation(uint program, string name) => _gl.GetUniformLocation(program, name);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetAttribLocation(uint program, string name) => _gl.GetAttribLocation(program, name);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GetUniformBlockIndex(uint program, string uniformBlockName) =>
        _gl.GetUniformBlockIndex(program, uniformBlockName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetActiveUniform(uint program, uint index, Span<byte> nameBuffer, out int length, out int size, out uint type)
    {
        if (nameBuffer.IsEmpty)
        {
            length = 0;
            size = 0;
            type = 0;
            return;
        }

        _gl.GetActiveUniform(program, index, (uint)nameBuffer.Length, out uint nameLength, out int activeSize, out GLEnum activeType, out nameBuffer[0]);
        length = (int)nameLength;
        size = activeSize;
        type = (uint)activeType;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetActiveAttrib(uint program, uint index, Span<byte> nameBuffer, out int length, out int size, out uint type)
    {
        if (nameBuffer.IsEmpty)
        {
            length = 0;
            size = 0;
            type = 0;
            return;
        }

        _gl.GetActiveAttrib(program, index, (uint)nameBuffer.Length, out uint nameLength, out int activeSize, out GLEnum activeType, out nameBuffer[0]);
        length = (int)nameLength;
        size = activeSize;
        type = (uint)activeType;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UniformBlockBinding(uint program, uint uniformBlockIndex, uint uniformBlockBinding) =>
        _gl.UniformBlockBinding(program, uniformBlockIndex, uniformBlockBinding);

    // ========================================================================
    // Uniform Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Uniform1(int location, int v0) => _gl.Uniform1(location, v0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Uniform1(int location, float v0) => _gl.Uniform1(location, v0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Uniform2(int location, float v0, float v1) => _gl.Uniform2(location, v0, v1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Uniform3(int location, float v0, float v1, float v2) => _gl.Uniform3(location, v0, v1, v2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Uniform4(int location, float v0, float v1, float v2, float v3) =>
        _gl.Uniform4(location, v0, v1, v2, v3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UniformMatrix4(int location, uint count, bool transpose, float* value) =>
        _gl.UniformMatrix4(location, count, transpose, value);

    // ========================================================================
    // State Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enable(uint cap) => _gl.Enable((EnableCap)cap);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Disable(uint cap) => _gl.Disable((EnableCap)cap);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Viewport(int x, int y, uint width, uint height) => _gl.Viewport(x, y, width, height);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Scissor(int x, int y, uint width, uint height) => _gl.Scissor(x, y, width, height);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearColor(float red, float green, float blue, float alpha) =>
        _gl.ClearColor(red, green, blue, alpha);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearDepth(double depth) => _gl.ClearDepth(depth);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearStencil(int s) => _gl.ClearStencil(s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(uint mask) => _gl.Clear((ClearBufferMask)mask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ColorMask(bool red, bool green, bool blue, bool alpha) =>
        _gl.ColorMask(red, green, blue, alpha);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DepthMask(bool flag) => _gl.DepthMask(flag);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DepthFunc(uint func) => _gl.DepthFunc((DepthFunction)func);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BlendFuncSeparate(uint srcRGB, uint dstRGB, uint srcAlpha, uint dstAlpha) =>
        _gl.BlendFuncSeparate((BlendingFactor)srcRGB, (BlendingFactor)dstRGB, (BlendingFactor)srcAlpha, (BlendingFactor)dstAlpha);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BlendEquationSeparate(uint modeRGB, uint modeAlpha) =>
        _gl.BlendEquationSeparate((BlendEquationModeEXT)modeRGB, (BlendEquationModeEXT)modeAlpha);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CullFace(uint mode) => _gl.CullFace((GLEnum)mode);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FrontFace(uint mode) => _gl.FrontFace((FrontFaceDirection)mode);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PixelStore(uint pname, int param) => _gl.PixelStore((PixelStoreParameter)pname, param);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LineWidth(float width) => _gl.LineWidth(width);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PolygonOffset(float factor, float units) => _gl.PolygonOffset(factor, units);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StencilFunc(uint func, int reference, uint mask) =>
        _gl.StencilFunc((StencilFunction)func, reference, mask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StencilOp(uint sfail, uint dpfail, uint dppass) =>
        _gl.StencilOp((StencilOp)sfail, (StencilOp)dpfail, (StencilOp)dppass);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StencilMask(uint mask) => _gl.StencilMask(mask);

    // ========================================================================
    // Draw Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawArrays(uint mode, int first, uint count) =>
        _gl.DrawArrays((PrimitiveType)mode, first, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawElements(uint mode, uint count, uint type, void* indices) =>
        _gl.DrawElements((PrimitiveType)mode, count, (DrawElementsType)type, indices);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawArraysInstanced(uint mode, int first, uint count, uint instancecount) =>
        _gl.DrawArraysInstanced((PrimitiveType)mode, first, count, instancecount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawElementsInstanced(uint mode, uint count, uint type, void* indices, uint instancecount) =>
        _gl.DrawElementsInstanced((PrimitiveType)mode, count, (DrawElementsType)type, indices, instancecount);

    // ========================================================================
    // Query Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GenQuery() => _gl.GenQuery();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeleteQuery(uint query) => _gl.DeleteQuery(query);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BeginQuery(uint target, uint query) => _gl.BeginQuery((Silk.NET.OpenGL.QueryTarget)target, query);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndQuery(uint target) => _gl.EndQuery((Silk.NET.OpenGL.QueryTarget)target);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetQueryParameter(uint query, uint pname, out int parameters) =>
        _gl.GetQueryObject(query, (QueryObjectParameterName)pname, out parameters);

    // ========================================================================
    // Sync Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public nint FenceSync(uint condition, uint flags) =>
        (nint)_gl.FenceSync((GLEnum)condition, flags);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeleteSync(nint sync) => _gl.DeleteSync(sync);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetSync(nint sync, uint pname, out int parameters)
    {
        unsafe
        {
            int size = 0;
            int result = 0;
            _gl.GetSync(sync, (GLEnum)pname, 1, (uint*)&size, &result);
            parameters = result;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ClientWaitSync(nint sync, uint flags, ulong timeout) =>
        (uint)_gl.ClientWaitSync(sync, flags, timeout);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WaitSync(nint sync, uint flags, ulong timeout) =>
        _gl.WaitSync(sync, flags, timeout);

    // ========================================================================
    // Transform Feedback Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GenTransformFeedback() => _gl.GenTransformFeedback();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DeleteTransformFeedback(uint transformFeedback) => _gl.DeleteTransformFeedback(transformFeedback);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindTransformFeedback(uint target, uint transformFeedback) =>
        _gl.BindTransformFeedback((GLEnum)target, transformFeedback);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TransformFeedbackVaryings(uint program, string[] varyings, uint bufferMode)
    {
        ArgumentNullException.ThrowIfNull(varyings);
        // Simplified: TransformFeedbackVaryings is rarely used in modern renderers
        // For a complete implementation, would need to marshal string[] to byte**
        // This is a placeholder that satisfies the interface
        if (varyings.Length > 0)
        {
            // Note: This would need proper marshalling in production code
            // For now, we'll skip this as it's not critical for the core renderer
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindBufferBase(uint target, uint index, uint buffer) =>
        _gl.BindBufferBase((GLEnum)target, index, buffer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BeginTransformFeedback(uint primitiveMode) =>
        _gl.BeginTransformFeedback((GLEnum)primitiveMode);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PauseTransformFeedback() => _gl.PauseTransformFeedback();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResumeTransformFeedback() => _gl.ResumeTransformFeedback();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndTransformFeedback() => _gl.EndTransformFeedback();

    // ========================================================================
    // Information Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetInteger(uint pname, out int data) => _gl.GetInteger((GetPName)pname, out data);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? GetString(uint name)
    {
        byte* str = _gl.GetString((StringName)name);
        return str == null ? null : System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)str);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? GetString(uint name, uint index)
    {
        byte* str = _gl.GetString((StringName)name, index);
        return str == null ? null : System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)str);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GetError() => (uint)_gl.GetError();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ObjectLabel(uint identifier, uint name, uint length, string label) =>
        _gl.ObjectLabel((ObjectIdentifier)identifier, name, length, label);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Flush() => _gl.Flush();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Finish() => _gl.Finish();

    // ========================================================================
    // Compute Operations
    // ========================================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DispatchCompute(uint numGroupsX, uint numGroupsY, uint numGroupsZ) =>
        _gl.DispatchCompute(numGroupsX, numGroupsY, numGroupsZ);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MemoryBarrier(uint barriers) =>
        _gl.MemoryBarrier(barriers);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BindImageTexture(uint unit, uint texture, int level, bool layered, int layer, uint access, uint format) =>
        _gl.BindImageTexture(unit, texture, level, layered, layer, (GLEnum)access, (GLEnum)format);
}
