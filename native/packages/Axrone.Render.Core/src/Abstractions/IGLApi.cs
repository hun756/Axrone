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

}
