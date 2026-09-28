namespace Axrone.Render.Core;

/// <summary>
/// Classifies a render command packet for pump dispatch.
/// </summary>
public enum RenderCommandType : byte
{
    /// <summary>Framebuffer-to-framebuffer blit.</summary>
    Blit = 0,

    /// <summary>Color/depth/stencil clear of one framebuffer.</summary>
    Clear = 1,
}
