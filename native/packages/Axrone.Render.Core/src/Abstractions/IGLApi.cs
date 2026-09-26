namespace Axrone.Render.Core.Abstractions;

/// <summary>
/// Abstraction layer for OpenGL operations.
/// Provides a testable interface for all GL calls used by the renderer.
/// </summary>
public unsafe interface IGLApi
{
    // ========================================================================
    // Buffer Operations
    // ========================================================================

    /// <summary>Generates a buffer object name.</summary>
    uint GenBuffer();

    /// <summary>Deletes a buffer object.</summary>
    void DeleteBuffer(uint buffer);

    /// <summary>Binds a buffer to a target.</summary>
    void BindBuffer(uint target, uint buffer);

    /// <summary>Creates and initializes a buffer's data store.</summary>
    void BufferData(uint target, nuint size, void* data, uint usage);

    /// <summary>Updates a subset of a buffer's data store.</summary>
    void BufferSubData(uint target, nint offset, nuint size, void* data);

    /// <summary>Copies data between buffers.</summary>
    void CopyBufferSubData(uint readTarget, uint writeTarget, nint readOffset, nint writeOffset, nuint size);

    /// <summary>Reads data from a buffer.</summary>
    void GetBufferSubData(uint target, nint offset, nuint size, void* data);

    // ========================================================================
    // Vertex Array Operations
    // ========================================================================

    /// <summary>Generates a vertex array object name.</summary>
    uint GenVertexArray();

    /// <summary>Deletes a vertex array object.</summary>
    void DeleteVertexArray(uint array);

    /// <summary>Binds a vertex array object.</summary>
    void BindVertexArray(uint array);

    /// <summary>Enables a generic vertex attribute array.</summary>
    void EnableVertexAttribArray(uint index);

    /// <summary>Disables a generic vertex attribute array.</summary>
    void DisableVertexAttribArray(uint index);

    /// <summary>Defines the layout of a vertex attribute.</summary>
    void VertexAttribPointer(uint index, int size, uint type, bool normalized, uint stride, void* pointer);

    /// <summary>Defines the layout of an integer vertex attribute.</summary>
    void VertexAttribIPointer(uint index, int size, uint type, uint stride, void* pointer);

    /// <summary>Defines the divisor for instanced rendering.</summary>
    void VertexAttribDivisor(uint index, uint divisor);

    // ========================================================================
    // Texture Operations
    // ========================================================================

    /// <summary>Generates a texture object name.</summary>
    uint GenTexture();

    /// <summary>Deletes a texture object.</summary>
    void DeleteTexture(uint texture);

    /// <summary>Binds a texture to a target.</summary>
    void BindTexture(uint target, uint texture);

    /// <summary>Selects the active texture unit.</summary>
    void ActiveTexture(uint textureUnit);

    /// <summary>Establishes 2D texture storage.</summary>
    void TexStorage2D(uint target, uint levels, uint internalFormat, uint width, uint height);

    /// <summary>Establishes 3D texture storage.</summary>
    void TexStorage3D(uint target, uint levels, uint internalFormat, uint width, uint height, uint depth);

    /// <summary>Updates a 2D texture sub-region.</summary>
    void TexSubImage2D(uint target, int level, int xoffset, int yoffset, uint width, uint height, uint format, uint type, void* pixels);

    /// <summary>Sets a texture integer parameter.</summary>
    void TexParameteri(uint target, uint pname, int param);

    /// <summary>Sets a texture float parameter.</summary>
    void TexParameterf(uint target, uint pname, float param);

    /// <summary>Updates a 2D texture sub-region with compressed data.</summary>
    void CompressedTexSubImage2D(uint target, int level, int xoffset, int yoffset, uint width, uint height, uint format, nuint imageSize, void* data);

    /// <summary>Reads pixel data from a texture image.</summary>
    void GetTexImage(uint target, int level, uint format, uint type, void* pixels);

    /// <summary>Copies a sub-region of the current framebuffer to a texture.</summary>
    void CopyTexSubImage2D(uint target, int level, int xoffset, int yoffset, int x, int y, uint width, uint height);

    /// <summary>Generates mipmaps for a texture.</summary>
    void GenerateMipmap(uint target);

    // ========================================================================
    // Sampler Operations
    // ========================================================================

    /// <summary>Generates a sampler object name.</summary>
    uint GenSampler();

    /// <summary>Deletes a sampler object.</summary>
    void DeleteSampler(uint sampler);

    /// <summary>Binds a sampler to a texture unit.</summary>
    void BindSampler(uint unit, uint sampler);

    /// <summary>Sets a sampler integer parameter.</summary>
    void SamplerParameter(uint sampler, uint pname, int param);

    /// <summary>Sets a sampler float parameter.</summary>
    void SamplerParameter(uint sampler, uint pname, float param);

    // ========================================================================
    // Framebuffer Operations
    // ========================================================================

    /// <summary>Generates a framebuffer object name.</summary>
    uint GenFramebuffer();

    /// <summary>Deletes a framebuffer object.</summary>
    void DeleteFramebuffer(uint framebuffer);

    /// <summary>Binds a framebuffer to a target.</summary>
    void BindFramebuffer(uint target, uint framebuffer);

    /// <summary>Attaches a texture to a framebuffer.</summary>
    void FramebufferTexture2D(uint target, uint attachment, uint textarget, uint texture, int level);

    /// <summary>Attaches a renderbuffer to a framebuffer.</summary>
    void FramebufferRenderbuffer(uint target, uint attachment, uint renderbuffertarget, uint renderbuffer);

    /// <summary>Checks framebuffer completeness.</summary>
    uint CheckFramebufferStatus(uint target);

    /// <summary>Copies a block of pixels between framebuffers.</summary>
    void BlitFramebuffer(int srcX0, int srcY0, int srcX1, int srcY1, int dstX0, int dstY0, int dstX1, int dstY1, uint mask, uint filter);

    /// <summary>Specifies color buffers for drawing.</summary>
    void DrawBuffers(ReadOnlySpan<uint> bufs);

    /// <summary>Reads pixels from a framebuffer.</summary>
    void ReadPixels(int x, int y, uint width, uint height, uint format, uint type, void* data);

    /// <summary>Selects a color buffer for reading.</summary>
    void ReadBuffer(uint mode);

    // ========================================================================
    // Renderbuffer Operations
    // ========================================================================

    /// <summary>Generates a renderbuffer object name.</summary>
    uint GenRenderbuffer();

    /// <summary>Deletes a renderbuffer object.</summary>
    void DeleteRenderbuffer(uint renderbuffer);

    /// <summary>Binds a renderbuffer.</summary>
    void BindRenderbuffer(uint target, uint renderbuffer);

    /// <summary>Establishes renderbuffer storage.</summary>
    void RenderbufferStorage(uint target, uint internalformat, uint width, uint height);

    /// <summary>Establishes multisample renderbuffer storage.</summary>
    void RenderbufferStorageMultisample(uint target, uint samples, uint internalformat, uint width, uint height);

    // ========================================================================
    // Shader Operations
    // ========================================================================

    /// <summary>Creates a shader object.</summary>
    uint CreateShader(uint type);

    /// <summary>Deletes a shader object.</summary>
    void DeleteShader(uint shader);

    /// <summary>Sets the source code for a shader.</summary>
    void ShaderSource(uint shader, string source);

    /// <summary>Compiles a shader object.</summary>
    void CompileShader(uint shader);

    /// <summary>Gets a shader parameter.</summary>
    void GetShader(uint shader, uint pname, out int parameters);

    /// <summary>Gets the shader info log.</summary>
    string GetShaderInfoLog(uint shader);

    // ========================================================================
    // Program Operations
    // ========================================================================

    /// <summary>Creates a program object.</summary>
    uint CreateProgram();

    /// <summary>Deletes a program object.</summary>
    void DeleteProgram(uint program);

    /// <summary>Attaches a shader to a program.</summary>
    void AttachShader(uint program, uint shader);

    /// <summary>Detaches a shader from a program.</summary>
    void DetachShader(uint program, uint shader);

    /// <summary>Links a program object.</summary>
    void LinkProgram(uint program);

    /// <summary>Installs a program as part of the rendering state.</summary>
    void UseProgram(uint program);

    /// <summary>Gets a program parameter.</summary>
    void GetProgram(uint program, uint pname, out int parameters);

    /// <summary>Gets the program info log.</summary>
    string GetProgramInfoLog(uint program);

    /// <summary>Gets the location of a uniform variable.</summary>
    int GetUniformLocation(uint program, string name);

    /// <summary>Gets the location of an attribute variable.</summary>
    int GetAttribLocation(uint program, string name);

    /// <summary>Gets the index of a uniform block.</summary>
    uint GetUniformBlockIndex(uint program, string uniformBlockName);

    /// <summary>Binds a uniform block to a binding point.</summary>
    void UniformBlockBinding(uint program, uint uniformBlockIndex, uint uniformBlockBinding);

    // ========================================================================
}
