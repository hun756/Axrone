namespace Axrone.Render.Core;

/// <summary>
/// Inline storage for up to 8 color attachments without heap allocation.
/// </summary>
[InlineArray(MaxColorAttachments)]
public struct ColorAttachmentArray : IEquatable<ColorAttachmentArray>
{
    /// <summary>Maximum number of color attachments supported (matches GL MRT limit).</summary>
    public const int MaxColorAttachments = 8;
    private AttachmentDescriptor _element0;

    /// <inheritdoc/>
    public bool Equals(ColorAttachmentArray other)
    {
        for (int i = 0; i < MaxColorAttachments; i++)
        {
            if (this[i] != other[i]) return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is ColorAttachmentArray other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < MaxColorAttachments; i++)
        {
            hash.Add(this[i]);
        }
        return hash.ToHashCode();
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(in ColorAttachmentArray left, in ColorAttachmentArray right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(in ColorAttachmentArray left, in ColorAttachmentArray right) => !left.Equals(right);
}

/// <summary>
/// Hardware-agnostic specification of a render pass: target framebuffer, viewport,
/// scissor box, and attachment bindings (load/store actions and clear values).
/// Blittable-friendly zero-allocation value type.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct RenderPassDescriptor : IEquatable<RenderPassDescriptor>
{
    /// <summary>Maximum number of color attachments supported.</summary>
    public const int MaxColorAttachments = ColorAttachmentArray.MaxColorAttachments;

    /// <summary>Target framebuffer ID (0 for the default backbuffer).</summary>
    public readonly uint FramebufferId;

    /// <summary>Viewport rectangle in framebuffer pixels.</summary>
    public readonly ViewportRect Viewport;

    /// <summary>Scissor rectangle in framebuffer pixels.</summary>
    public readonly ScissorRect Scissor;

    /// <summary>Whether scissor testing is enabled for the pass.</summary>
    public readonly bool ScissorTest;

    /// <summary>Number of active color attachments in the pass.</summary>
    public readonly byte ColorAttachmentCount;

    /// <summary>Whether a depth/stencil attachment is bound.</summary>
    public readonly bool HasDepthStencil;

    /// <summary>Depth/stencil attachment binding.</summary>
    public readonly AttachmentDescriptor DepthStencilAttachment;

    private readonly ColorAttachmentArray _colorAttachments;

    /// <summary>Initializes a render pass descriptor with explicit viewport, scissor, and attachments.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public RenderPassDescriptor(
        uint framebufferId,
        in ViewportRect viewport,
        in ScissorRect scissor,
        bool scissorTest,
        ReadOnlySpan<AttachmentDescriptor> attachments)
    {
        FramebufferId = framebufferId;
        Viewport = viewport;
        Scissor = scissor;
        ScissorTest = scissorTest;
        _colorAttachments = default;
        ColorAttachmentCount = 0;
        HasDepthStencil = false;
        DepthStencilAttachment = default;

        int count = attachments.Length;
        for (int i = 0; i < count; i++)
        {
            ref readonly var att = ref attachments[i];
            if (att.IsDepthStencil)
            {
                DepthStencilAttachment = att;
                HasDepthStencil = true;
            }
            else
            {
                if (ColorAttachmentCount >= MaxColorAttachments)
                {
                    ThrowHelper.ThrowArgumentOutOfRange(nameof(attachments), count, $"Color attachment count exceeds physical maximum of {MaxColorAttachments}");
                }

                _colorAttachments[ColorAttachmentCount++] = att;
            }
        }
    }

    /// <summary>Initializes a render pass descriptor without scissor test.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public RenderPassDescriptor(
        uint framebufferId,
        in ViewportRect viewport,
        ReadOnlySpan<AttachmentDescriptor> attachments)
        : this(framebufferId, viewport, new ScissorRect(viewport.X, viewport.Y, viewport.Width, viewport.Height), scissorTest: false, attachments)
    {
    }

    /// <summary>Gets the color attachment at the specified index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AttachmentDescriptor GetColorAttachment(int index)
    {
        if ((uint)index >= (uint)ColorAttachmentCount)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(index), index, "Requested attachment index is outside the initialized boundaries.");
        }

        return _colorAttachments[index];
    }

    /// <summary>Returns the color attachment at the given index without bounds checking.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AttachmentDescriptor GetColorAttachmentUnchecked(int index) =>
        _colorAttachments[index];


    /// <summary>Gets a read-only span of active color attachments.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<AttachmentDescriptor> GetColorAttachments()
    {
        if (ColorAttachmentCount == 0)
        {
            return ReadOnlySpan<AttachmentDescriptor>.Empty;
        }

        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _colorAttachments[0]), ColorAttachmentCount);
    }

    /// <summary>Creates a default render pass descriptor targeting the backbuffer (FBO 0).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static RenderPassDescriptor CreateDefault(
        in ViewportRect viewport,
        ClearColorValue clearColor = default,
        float clearDepth = 1f,
        int clearStencil = 0)
    {
        Span<AttachmentDescriptor> attachments = stackalloc AttachmentDescriptor[2]
        {
            AttachmentDescriptor.Color(0, AttachmentLoadAction.Clear, AttachmentStoreAction.Store, clearColor),
            AttachmentDescriptor.DepthStencil(AttachmentLoadAction.Clear, AttachmentStoreAction.Store, clearDepth, clearStencil)
        };

        return new RenderPassDescriptor(0, viewport, attachments);
    }

    /// <summary>Creates a render pass descriptor with the given attachments.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static RenderPassDescriptor Create(
        uint framebufferId,
        in ViewportRect viewport,
        params ReadOnlySpan<AttachmentDescriptor> attachments) =>
        new(framebufferId, viewport, attachments);

    /// <summary>Creates a render pass descriptor with explicit scissor testing.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static RenderPassDescriptor Create(
        uint framebufferId,
        in ViewportRect viewport,
        in ScissorRect scissor,
        bool scissorTest,
        params ReadOnlySpan<AttachmentDescriptor> attachments) =>
        new(framebufferId, viewport, scissor, scissorTest, attachments);

    /// <inheritdoc/>
    public bool Equals(RenderPassDescriptor other)
    {
        if (FramebufferId != other.FramebufferId ||
            Viewport != other.Viewport ||
            Scissor != other.Scissor ||
            ScissorTest != other.ScissorTest ||
            ColorAttachmentCount != other.ColorAttachmentCount ||
            HasDepthStencil != other.HasDepthStencil)
        {
            return false;
        }

        if (HasDepthStencil && !DepthStencilAttachment.Equals(other.DepthStencilAttachment))
        {
            return false;
        }

        for (int i = 0; i < ColorAttachmentCount; i++)
        {
            if (!_colorAttachments[i].Equals(other._colorAttachments[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is RenderPassDescriptor other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FramebufferId);
        hash.Add(Viewport);
        hash.Add(Scissor);
        hash.Add(ScissorTest);
        hash.Add(ColorAttachmentCount);
        hash.Add(HasDepthStencil);
        if (HasDepthStencil) hash.Add(DepthStencilAttachment);
        for (int i = 0; i < ColorAttachmentCount; i++)
        {
            hash.Add(_colorAttachments[i]);
        }
        return hash.ToHashCode();
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(in RenderPassDescriptor left, in RenderPassDescriptor right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(in RenderPassDescriptor left, in RenderPassDescriptor right) => !left.Equals(right);
}
