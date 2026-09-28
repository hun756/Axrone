namespace Axrone.Render.Core.Abstractions;

/// <summary>
/// Hardware-agnostic render context interface for issuing rendering, binding,
/// state configuration, draw commands, and pass lifecycle operations.
/// </summary>
public interface IRenderContext
{
    /// <summary>Begins a render pass configured with the given descriptor.</summary>
    void BeginPass(in RenderPassDescriptor descriptor);

    /// <summary>Concludes the currently active render pass, applying store actions.</summary>
    void EndPass();

    /// <summary>Sets the active viewport rectangle in framebuffer coordinates.</summary>
    void SetViewport(in ViewportRect viewport);

    /// <summary>Sets the active scissor rectangle and enables/disables scissor testing.</summary>
    void SetScissor(in ScissorRect scissor, bool enabled = true);

    /// <summary>Configures the hardware blend state.</summary>
    void SetBlendState(bool enabled, uint srcRGB = 0, uint dstRGB = 0, uint srcAlpha = 0, uint dstAlpha = 0);

    /// <summary>Configures the RGB and alpha blend equations.</summary>
    void SetBlendEquation(uint equationRGB, uint equationAlpha);

    /// <summary>Configures hardware depth testing and writing.</summary>
    void SetDepthState(bool testEnabled, bool writeEnabled, uint depthFunc = 0);

    /// <summary>Configures hardware polygon face culling.</summary>
    void SetCullState(bool enabled, uint cullMode = 0, uint frontFace = 0);

    /// <summary>Configures the per-channel color write mask.</summary>
    void SetColorMask(bool red, bool green, bool blue, bool alpha);

    /// <summary>Binds a shader program for subsequent draw calls.</summary>
    void BindProgram(uint programId);

    /// <summary>Binds a texture to the specified texture unit slot.</summary>
    void BindTexture(uint slot, uint textureId, uint target = 0);

    /// <summary>Binds a sampler to the specified texture unit slot.</summary>
    void BindSampler(uint slot, uint samplerId);

    /// <summary>Binds a vertex array object.</summary>
    void BindVertexArray(uint vaoId);

    /// <summary>Sets a single-precision scalar float uniform.</summary>
    void SetUniform(int location, float value);

    /// <summary>Sets a 2D float vector uniform.</summary>
    void SetUniform(int location, float x, float y);

    /// <summary>Sets a 3D float vector uniform.</summary>
    void SetUniform(int location, float x, float y, float z);

    /// <summary>Sets a 4D float vector uniform.</summary>
    void SetUniform(int location, float x, float y, float z, float w);

    /// <summary>Sets a 32-bit signed integer uniform.</summary>
    void SetUniform(int location, int value);

    /// <summary>Sets a 4x4 matrix uniform from floating point data.</summary>
    void SetUniformMatrix4(int location, ReadOnlySpan<float> matrix, bool transpose = false);

    /// <summary>Renders non-indexed primitives from the currently bound vertex buffers.</summary>
    void Draw(uint primitiveMode, int first, uint count);

    /// <summary>Renders indexed primitives from the currently bound element array buffer.</summary>
    void DrawIndexed(uint primitiveMode, uint count, uint indexType, nint indicesOffset = 0);

    /// <summary>Renders instanced non-indexed primitives.</summary>
    void DrawInstanced(uint primitiveMode, int first, uint count, uint instanceCount);

    /// <summary>Renders instanced indexed primitives.</summary>
    void DrawIndexedInstanced(uint primitiveMode, uint count, uint indexType, nint indicesOffset, uint instanceCount);

    /// <summary>Renders a fullscreen triangle or quad for screen-space passes.</summary>
    void DrawFullscreenQuad();

    /// <summary>Blits (copies) pixel data between framebuffers.</summary>
    void Blit(uint sourceFbo, uint destinationFbo, in ViewportRect srcRect, in ViewportRect dstRect, uint mask, uint filter);

    /// <summary>Clears the current framebuffer targets according to the specified mask and values.</summary>
    void Clear(uint mask, in ClearColorValue color, float depth = 1f, int stencil = 0);

    /// <summary>Dispatches compute shader work groups.</summary>
    void DispatchCompute(uint numGroupsX, uint numGroupsY, uint numGroupsZ);

    /// <summary>Issues a memory barrier ordering GPU memory transactions.</summary>
    void MemoryBarrier(uint barriers);

    /// <summary>Executes a custom context action for escape hatches.</summary>
    void ExecuteCustom(Action<IRenderContext> callback);
}
