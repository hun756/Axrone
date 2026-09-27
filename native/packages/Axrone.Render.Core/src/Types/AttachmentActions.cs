namespace Axrone.Render.Core;

/// <summary>
/// Describes how the previous contents of a render target attachment must be made
/// available to a render pass.
/// </summary>
/// <remarks>
/// The distinction matters most on tile-based deferred renderers (TBDR): on a
/// tile architecture the driver can either pull an attachment back from VRAM
/// (or a previous tile's memory) before the pass, or skip that read entirely when
/// the pass overwrites every texel. The latter is why <see cref="DontCare"/> is
/// the highest-value hint on such hardware. On immediate-mode renderers the
/// values are advisory only.
/// </remarks>
public enum AttachmentLoadAction : byte
{
    /// <summary>
    /// Preserve and load the existing contents. The attachment's previous value is
    /// made available to the pass, so the driver must fetch it into the render
    /// target (tile memory on TBDRs, framebuffer contents on immediate renderers).
    /// This is the default and preserves the legacy behavior: content written by an
    /// earlier pass is visible to the pass.
    /// </summary>
    Load = 0,

    /// <summary>
    /// Clear the attachment to its clear value instead of loading the previous
    /// contents. No read of the old value is required, so on TBDRs the tile can be
    /// initialized in place (fast clear) rather than resolved and re-fetched, but the
    /// result is still fully defined: the pass sees the clear value, not the old one.
    /// </summary>
    Clear = 1,

    /// <summary>
    /// Do not care about the existing contents. The pass writes every texel of the
    /// attachment, so the driver may skip the VRAM/tile read-back entirely and leave
    /// the previous value undefined from the pass's point of view. On TBDRs this
    /// removes a full attachment load, which is the main bandwidth cost of naive
    /// load/store attachment handling. Only valid when the pass actually writes the
    /// attachment for every pixel it renders.
    /// </summary>
    DontCare = 2
}

/// <summary>
/// Describes what must happen to a render target attachment's contents once a
/// render pass has finished writing it.
/// </summary>
/// <remarks>
/// The distinction matters most on tile-based deferred renderers (TBDR): when the
/// attachment is not needed after the pass, the driver can invalidate the tile and
/// skip the write-back, avoiding a full store to VRAM. Store operations also provide
/// the point at which a multisample attachment is resolved down to its single-sample
/// form.
/// </remarks>
public enum AttachmentStoreAction : byte
{
    /// <summary>
    /// Persist the attachment's contents. The final value is written back to the
    /// underlying render target so later passes and frames observe it. This is the
    /// default and preserves the legacy behavior: content survives the pass.
    /// </summary>
    Store = 0,

    /// <summary>
    /// Discard the attachment's contents. Nothing will read the attachment after the
    /// pass, so the driver may invalidate the tile and skip the write-back to VRAM.
    /// Reading the attachment afterwards yields undefined contents, so this must only
    /// be declared for transient targets whose lifetime ends with the pass.
    /// </summary>
    Discard = 1,

    /// <summary>
    /// Resolve the attachment while storing it. Multisample contents are resolved into
    /// the single-sample resolve target as they are written back, instead of requiring
    /// a separate resolve pass. Equivalent to storing the multisample attachment and
    /// performing a resolve afterwards, but without the extra pass and its bandwidth.
    /// </summary>
    Resolve = 2
}
