namespace Axrone.Render.Core;

/// <summary>One render-target attachment binding of a render pass.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct AttachmentDescriptor : IEquatable<AttachmentDescriptor>
{
    /// <summary>Maximum slot index the inline descriptor storage supports.</summary>
    public const int MaxSlot = 7;

    /// <summary>Logical attachment slot (0-7 for color targets).</summary>
    public readonly byte Slot;

    /// <summary>How previous attachment contents are made available.</summary>
    public readonly AttachmentLoadAction LoadAction;

    /// <summary>What happens to attachment contents after the pass.</summary>
    public readonly AttachmentStoreAction StoreAction;

    /// <summary>Color clear value applied on Clear load.</summary>
    public readonly ClearColorValue ClearColor;

    /// <summary>Depth clear value applied on Clear load.</summary>
    public readonly float ClearDepth;

    /// <summary>Stencil clear value applied on Clear load.</summary>
    public readonly int ClearStencil;

    /// <summary>Whether depth/stencil clear values are authoritative.</summary>
    public readonly bool IsDepthStencil;

    /// <summary>Optional resolve target framebuffer for multisample resolve actions.</summary>
    public readonly uint ResolveFramebuffer;

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

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AttachmentDescriptor other) =>
        Slot == other.Slot &&
        LoadAction == other.LoadAction &&
        StoreAction == other.StoreAction &&
        ClearColor == other.ClearColor &&
        ClearDepth == other.ClearDepth &&
        ClearStencil == other.ClearStencil &&
        IsDepthStencil == other.IsDepthStencil &&
        ResolveFramebuffer == other.ResolveFramebuffer;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is AttachmentDescriptor other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(
        Slot, (byte)LoadAction, (byte)StoreAction, ClearDepth, ClearStencil, IsDepthStencil, ClearColor, ResolveFramebuffer);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(AttachmentDescriptor left, AttachmentDescriptor right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(AttachmentDescriptor left, AttachmentDescriptor right) => !left.Equals(right);
}

