namespace Axrone.Render.OpenGL.Context;

/// <summary>
/// OpenGL backend implementation of <see cref="IRenderContext"/>.
/// Manages render pass lifecycles (FBO binding, MRT draw buffers, load/store actions, tile discards, MSAA resolves)
/// and routes all rendering state changes through <see cref="GLStateCache"/> to eliminate redundant GL calls.
/// </summary>
public sealed class GLRenderContext : IRenderContext
{
    private readonly GLContext _context;
    private PassExecutionContext? _passContext;
    private RenderPassDescriptor _activePass;
    private bool _isPassActive;
    private bool _scissorEnabled;

    /// <summary>Gets the underlying OpenGL context.</summary>
    public GLContext GLContext => _context;

    /// <summary>Gets or sets the execution context containing named transient resources.</summary>
    public PassExecutionContext? PassContext
    {
        get => _passContext;
        set => _passContext = value;
    }

    /// <summary>Gets whether a render pass is currently active.</summary>
    public bool IsPassActive => _isPassActive;

    /// <summary>Gets the descriptor of the active pass, or default if no pass is active.</summary>
    public ref readonly RenderPassDescriptor ActivePass => ref _activePass;

    /// <summary>Initializes a new instance of <see cref="GLRenderContext"/>.</summary>
    public GLRenderContext(GLContext context, PassExecutionContext? passContext = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _passContext = passContext;
    }

    /// <inheritdoc/>
    public void BeginPass(in RenderPassDescriptor descriptor)
    {
        _context.AssertRenderThread();

        if (_isPassActive)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "Cannot begin a render pass while another pass is already active.", nameof(GLRenderContext));
        }

        _activePass = descriptor;
        _isPassActive = true;

        // 1. Framebuffer Binding
        _context.State.BindFramebuffer(GLConst.Framebuffer, descriptor.FramebufferId);

        // 2. Draw Buffer MRT Configuration
        if (descriptor.FramebufferId != 0)
        {
            if (descriptor.ColorAttachmentCount > 0)
            {
                Span<uint> bufs = stackalloc uint[descriptor.ColorAttachmentCount];
                for (int i = 0; i < descriptor.ColorAttachmentCount; i++)
                {
                    bufs[i] = GLConst.ColorAttachment0 + descriptor.GetColorAttachmentUnchecked(i).Slot;
                }

                _context.GL.DrawBuffers(bufs);
            }
            else
            {
                Span<uint> none = stackalloc uint[1] { GLConst.None };
                _context.GL.DrawBuffers(none);
            }
        }

        // 3. Viewport & Scissor Setup
        _context.State.SetViewport(descriptor.Viewport.X, descriptor.Viewport.Y, descriptor.Viewport.Width, descriptor.Viewport.Height);

        if (descriptor.ScissorTest)
        {
            if (!_scissorEnabled)
            {
                _context.GL.Enable(GLConst.ScissorTest);
                _scissorEnabled = true;
            }

            _context.State.SetScissor(descriptor.Scissor.X, descriptor.Scissor.Y, descriptor.Scissor.Width, descriptor.Scissor.Height);
        }
        else if (_scissorEnabled)
        {
            _context.GL.Disable(GLConst.ScissorTest);
            _scissorEnabled = false;
        }

        // 4. Execute Load Pipeline (DontCare invalidation hints, then clears)
        Span<uint> dontCare = stackalloc uint[RenderPassDescriptor.MaxColorAttachments];
        int dontCareCount = 0;
        for (int i = 0; i < descriptor.ColorAttachmentCount; i++)
        {
            var hint = descriptor.GetColorAttachmentUnchecked(i);
            if (hint.LoadAction == AttachmentLoadAction.DontCare)
            {
                dontCare[dontCareCount++] = descriptor.FramebufferId == 0
                    ? GLConst.Color
                    : GLConst.ColorAttachment0 + hint.Slot;
            }
        }

        if (dontCareCount > 0)
        {
            _context.GL.InvalidateFramebuffer(GLConst.Framebuffer, dontCare.Slice(0, dontCareCount));
        }

        Span<float> colorBuffer = stackalloc float[4];
        for (int i = 0; i < descriptor.ColorAttachmentCount; i++)
        {
            var att = descriptor.GetColorAttachmentUnchecked(i);
            if (att.LoadAction == AttachmentLoadAction.Clear)
            {
                colorBuffer[0] = att.ClearColor.R;
                colorBuffer[1] = att.ClearColor.G;
                colorBuffer[2] = att.ClearColor.B;
                colorBuffer[3] = att.ClearColor.A;

                _context.GL.ClearBufferfv(GLConst.Color, att.Slot, colorBuffer);
            }
        }

        if (descriptor.HasDepthStencil)
        {
            var ds = descriptor.DepthStencilAttachment;
            if (ds.LoadAction == AttachmentLoadAction.Clear)
            {
                _context.GL.ClearBufferfi(GLConst.DepthStencil, 0, ds.ClearDepth, ds.ClearStencil);
            }
        }
    }

    /// <inheritdoc/>
    public void EndPass()
    {
        _context.AssertRenderThread();

        if (!_isPassActive)
        {
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "No render pass is currently active to end.", nameof(GLRenderContext));
        }

        // Execute Store Pipeline (Discard / Invalidate, Resolve blits)
        Span<uint> invalidations = stackalloc uint[RenderPassDescriptor.MaxColorAttachments + 2];
        int invCount = 0;

        for (int i = 0; i < _activePass.ColorAttachmentCount; i++)
        {
            var att = _activePass.GetColorAttachmentUnchecked(i);
            if (att.StoreAction == AttachmentStoreAction.Discard)
            {
                uint token = _activePass.FramebufferId == 0 ? GLConst.Color : GLConst.ColorAttachment0 + att.Slot;
                invalidations[invCount++] = token;
            }
            else if (att.StoreAction == AttachmentStoreAction.Resolve && att.ResolveFramebuffer != 0)
            {
                _context.State.BindFramebuffer(GLConst.ReadFramebuffer, _activePass.FramebufferId);
                _context.State.BindFramebuffer(GLConst.DrawFramebuffer, att.ResolveFramebuffer);

                _context.GL.BlitFramebuffer(
                    _activePass.Viewport.X, _activePass.Viewport.Y, _activePass.Viewport.X + _activePass.Viewport.Width, _activePass.Viewport.Y + _activePass.Viewport.Height,
                    _activePass.Viewport.X, _activePass.Viewport.Y, _activePass.Viewport.X + _activePass.Viewport.Width, _activePass.Viewport.Y + _activePass.Viewport.Height,
                    GLConst.ColorBufferBit, GLConst.NearestFilter);

                _context.State.BindFramebuffer(GLConst.Framebuffer, _activePass.FramebufferId);
            }
        }

        if (_activePass.HasDepthStencil)
        {
            var ds = _activePass.DepthStencilAttachment;
            if (ds.StoreAction == AttachmentStoreAction.Discard)
            {
                uint token = _activePass.FramebufferId == 0 ? GLConst.DepthBufferBit : GLConst.DepthStencilAttachment;
                invalidations[invCount++] = token;
            }
            else if (ds.StoreAction == AttachmentStoreAction.Resolve && ds.ResolveFramebuffer != 0)
            {
                _context.State.BindFramebuffer(GLConst.ReadFramebuffer, _activePass.FramebufferId);
                _context.State.BindFramebuffer(GLConst.DrawFramebuffer, ds.ResolveFramebuffer);

                _context.GL.BlitFramebuffer(
                    _activePass.Viewport.X, _activePass.Viewport.Y, _activePass.Viewport.X + _activePass.Viewport.Width, _activePass.Viewport.Y + _activePass.Viewport.Height,
                    _activePass.Viewport.X, _activePass.Viewport.Y, _activePass.Viewport.X + _activePass.Viewport.Width, _activePass.Viewport.Y + _activePass.Viewport.Height,
                    GLConst.DepthBufferBit | GLConst.StencilBufferBit, GLConst.NearestFilter);

                _context.State.BindFramebuffer(GLConst.Framebuffer, _activePass.FramebufferId);
            }
        }

        if (invCount > 0)
        {
            _context.GL.InvalidateFramebuffer(GLConst.Framebuffer, invalidations.Slice(0, invCount));
        }

        _isPassActive = false;
    }

    /// <inheritdoc/>
    public void SetViewport(in ViewportRect viewport)
    {
        _context.AssertRenderThread();
        _context.State.SetViewport(viewport.X, viewport.Y, viewport.Width, viewport.Height);
    }

    /// <inheritdoc/>
    public void SetScissor(in ScissorRect scissor, bool enabled = true)
    {
        _context.AssertRenderThread();

        if (enabled)
        {
            if (!_scissorEnabled)
            {
                _context.GL.Enable(GLConst.ScissorTest);
                _scissorEnabled = true;
            }

            _context.State.SetScissor(scissor.X, scissor.Y, scissor.Width, scissor.Height);
        }
        else if (_scissorEnabled)
        {
            _context.GL.Disable(GLConst.ScissorTest);
            _scissorEnabled = false;
        }
    }

    /// <inheritdoc/>
    public void SetBlendState(bool enabled, uint srcRGB = 0, uint dstRGB = 0, uint srcAlpha = 0, uint dstAlpha = 0)
    {
        _context.AssertRenderThread();
        if (enabled && srcRGB != 0)
        {
            _context.State.SetBlendFuncSeparate(srcRGB, dstRGB, srcAlpha, dstAlpha);
        }

        _context.State.SetBlend(enabled);
    }

    /// <inheritdoc/>
    public void SetBlendEquation(uint equationRGB, uint equationAlpha)
    {
        _context.AssertRenderThread();
        _context.State.SetBlendEquationSeparate(equationRGB, equationAlpha);
    }

    /// <inheritdoc/>
    public void SetDepthState(bool testEnabled, bool writeEnabled, uint depthFunc = 0)
    {
        _context.AssertRenderThread();
        _context.State.SetDepthTest(testEnabled);
        _context.State.SetDepthMask(writeEnabled);
        if (depthFunc != 0)
        {
            _context.State.SetDepthFunc(depthFunc);
        }
    }

    /// <inheritdoc/>
    public void SetCullState(bool enabled, uint cullMode = 0, uint frontFace = 0)
    {
        _context.AssertRenderThread();
        _context.State.SetCullFace(enabled);
        if (cullMode != 0)
        {
            _context.State.SetCullMode(cullMode);
        }

        if (frontFace != 0)
        {
            _context.State.SetFrontFace(frontFace);
        }
    }

    /// <inheritdoc/>
    public void SetColorMask(bool red, bool green, bool blue, bool alpha)
    {
        _context.AssertRenderThread();
        _context.State.SetColorMask(red, green, blue, alpha);
    }

    /// <inheritdoc/>
    public void BindProgram(uint programId)
    {
        _context.AssertRenderThread();
        _context.State.UseProgram(programId);
    }

    /// <inheritdoc/>
    public void BindTexture(uint slot, uint textureId, uint target = 0)
    {
        _context.AssertRenderThread();

        // Honor the caller's target. A cube map / 3D / 2D-array texture object cannot be bound
        // to TEXTURE_2D (GL_INVALID_OPERATION), so only the 2D path uses the cached per-unit
        // BindTexture2D; other targets bind directly on the selected unit.
        if (target == 0 || target == GLConst.Texture2D)
        {
            _context.State.BindTexture2D(slot, textureId);
        }
        else
        {
            _context.State.ActiveTexture(slot);
            _context.State.BindTexture(target, textureId);
        }
    }

    /// <inheritdoc/>
    public void BindSampler(uint slot, uint samplerId)
    {
        _context.AssertRenderThread();
        _context.State.BindSampler(slot, samplerId);
    }

    /// <inheritdoc/>
    public void BindVertexArray(uint vaoId)
    {
        _context.AssertRenderThread();
        _context.State.BindVertexArray(vaoId);
    }

    /// <inheritdoc/>
    public void SetUniform(int location, float value)
    {
        _context.AssertRenderThread();
        _context.GL.Uniform1(location, value);
    }

    /// <inheritdoc/>
    public void SetUniform(int location, float x, float y)
    {
        _context.AssertRenderThread();
        _context.GL.Uniform2(location, x, y);
    }

    /// <inheritdoc/>
    public void SetUniform(int location, float x, float y, float z)
    {
        _context.AssertRenderThread();
        _context.GL.Uniform3(location, x, y, z);
    }

    /// <inheritdoc/>
    public void SetUniform(int location, float x, float y, float z, float w)
    {
        _context.AssertRenderThread();
        _context.GL.Uniform4(location, x, y, z, w);
    }

    /// <inheritdoc/>
    public void SetUniform(int location, int value)
    {
        _context.AssertRenderThread();
        _context.GL.Uniform1(location, value);
    }

    /// <inheritdoc/>
    public unsafe void SetUniformMatrix4(int location, ReadOnlySpan<float> matrix, bool transpose = false)
    {
        _context.AssertRenderThread();
        fixed (float* ptr = matrix)
        {
            _context.GL.UniformMatrix4(location, 1, transpose, ptr);
        }
    }

    /// <inheritdoc/>
    public void Draw(uint primitiveMode, int first, uint count)
    {
        _context.AssertRenderThread();
        _context.GL.DrawArrays(primitiveMode, first, (uint)count);
    }

    /// <inheritdoc/>
    public unsafe void DrawIndexed(uint primitiveMode, uint count, uint indexType, nint indicesOffset = 0)
    {
        _context.AssertRenderThread();
        _context.GL.DrawElements(primitiveMode, count, indexType, (void*)indicesOffset);
    }

    /// <inheritdoc/>
    public void DrawInstanced(uint primitiveMode, int first, uint count, uint instanceCount)
    {
        _context.AssertRenderThread();
        _context.GL.DrawArraysInstanced(primitiveMode, first, (uint)count, instanceCount);
    }

    /// <inheritdoc/>
    public unsafe void DrawIndexedInstanced(uint primitiveMode, uint count, uint indexType, nint indicesOffset, uint instanceCount)
    {
        _context.AssertRenderThread();
        _context.GL.DrawElementsInstanced(primitiveMode, count, indexType, (void*)indicesOffset, instanceCount);
    }

    /// <inheritdoc/>
    public void DrawFullscreenQuad()
    {
        _context.AssertRenderThread();
        _context.GL.DrawArrays(GLConst.Triangles, 0, 3);
    }

    /// <inheritdoc/>
    public void Blit(uint sourceFbo, uint destinationFbo, in ViewportRect srcRect, in ViewportRect dstRect, uint mask, uint filter)
    {
        _context.AssertRenderThread();
        _context.State.BindFramebuffer(GLConst.ReadFramebuffer, sourceFbo);
        _context.State.BindFramebuffer(GLConst.DrawFramebuffer, destinationFbo);

        _context.GL.BlitFramebuffer(
            srcRect.X, srcRect.Y, srcRect.X + srcRect.Width, srcRect.Y + srcRect.Height,
            dstRect.X, dstRect.Y, dstRect.X + dstRect.Width, dstRect.Y + dstRect.Height,
            mask, filter);

        _context.State.BindFramebuffer(GLConst.Framebuffer, _isPassActive ? _activePass.FramebufferId : 0);
    }

    /// <inheritdoc/>
    public void Clear(uint mask, in ClearColorValue color, float depth = 1f, int stencil = 0)
    {
        _context.AssertRenderThread();
        if ((mask & GLConst.ColorBufferBit) != 0)
        {
            _context.State.SetClearColor(color.R, color.G, color.B, color.A);
        }

        if ((mask & GLConst.DepthBufferBit) != 0)
        {
            _context.State.SetClearDepth(depth);
        }

        if ((mask & GLConst.StencilBufferBit) != 0)
        {
            _context.State.SetClearStencil(stencil);
        }

        if (mask != 0)
        {
            _context.GL.Clear(mask);
        }
    }

    /// <inheritdoc/>
    public void DispatchCompute(uint numGroupsX, uint numGroupsY, uint numGroupsZ)
    {
        _context.AssertRenderThread();
        _context.GL.DispatchCompute(numGroupsX, numGroupsY, numGroupsZ);
    }

    /// <inheritdoc/>
    public void MemoryBarrier(uint barriers)
    {
        _context.AssertRenderThread();
        _context.GL.MemoryBarrier(barriers);
    }

    /// <inheritdoc/>
    public void ExecuteCustom(Action<IRenderContext> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _context.AssertRenderThread();
        callback(this);
    }
}
