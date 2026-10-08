namespace Axrone.Render.Core;

/// <summary>One render-target attachment binding of a render pass.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AttachmentDescriptor : IDescriptor<AttachmentDescriptor>
{
    /// <summary>Maximum slot index the inline descriptor storage supports.</summary>
    public const int MaxSlot = 7;

    /// <summary>Logical attachment slot (0-7 for color targets).</summary>
    public byte Slot { get; init; }

    /// <summary>How previous attachment contents are made available.</summary>
    public AttachmentLoadAction LoadAction { get; init; }

    /// <summary>What happens to attachment contents after the pass.</summary>
    public AttachmentStoreAction StoreAction { get; init; }

    /// <summary>Color clear value applied on Clear load.</summary>
    public ClearColorValue ClearColor { get; init; }

    /// <summary>Depth clear value applied on Clear load.</summary>
    public float ClearDepth { get; init; }

    /// <summary>Stencil clear value applied on Clear load.</summary>
    public int ClearStencil { get; init; }

    /// <summary>Whether depth/stencil clear values are authoritative.</summary>
    public bool IsDepthStencil { get; init; }

    /// <summary>Optional resolve target framebuffer for multisample resolve actions.</summary>
    public uint ResolveFramebuffer { get; init; }

    /// <summary>Initializes a new attachment descriptor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public AttachmentDescriptor(
        byte slot,
        AttachmentLoadAction loadAction = AttachmentLoadAction.Load,
        AttachmentStoreAction storeAction = AttachmentStoreAction.Store,
        ClearColorValue clearColor = default,
        float clearDepth = 1f,
        int clearStencil = 0,
        bool isDepthStencil = false,
        uint resolveFramebuffer = 0)
    {
        if (slot > MaxSlot)
        {
            ThrowHelper.ThrowInvalidArgument($"Attachment slot must be in [0, {MaxSlot}]");
        }

        Slot = slot;
        LoadAction = loadAction;
        StoreAction = storeAction;
        ClearColor = clearColor;
        ClearDepth = clearDepth;
        ClearStencil = clearStencil;
        IsDepthStencil = isDepthStencil;
        ResolveFramebuffer = resolveFramebuffer;
    }

    /// <summary>Creates a color attachment descriptor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static AttachmentDescriptor Color(
        byte slot = 0,
        AttachmentLoadAction loadAction = AttachmentLoadAction.Load,
        AttachmentStoreAction storeAction = AttachmentStoreAction.Store,
        ClearColorValue clearColor = default,
        uint resolveFramebuffer = 0) =>
        new(slot, loadAction, storeAction, clearColor, 1f, 0, isDepthStencil: false, resolveFramebuffer);

    /// <summary>Creates a depth/stencil attachment descriptor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static AttachmentDescriptor DepthStencil(
        AttachmentLoadAction loadAction = AttachmentLoadAction.Load,
        AttachmentStoreAction storeAction = AttachmentStoreAction.Store,
        float clearDepth = 1f,
        int clearStencil = 0,
        uint resolveFramebuffer = 0) =>
        new(0, loadAction, storeAction, default, clearDepth, clearStencil, isDepthStencil: true, resolveFramebuffer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Validate(in AttachmentDescriptor descriptor)
    {
        if (descriptor.Slot > MaxSlot)
        {
            ThrowHelper.ThrowInvalidArgument($"Attachment slot must be in [0, {MaxSlot}]");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Describe(in AttachmentDescriptor descriptor) => descriptor.ToString();
}

