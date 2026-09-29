namespace Axrone.Render.Core;

/// <summary>Viewport rectangle in framebuffer pixels (GL origin: lower-left, y-up).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ViewportRect(int X, int Y, int Width, int Height)
{
    /// <summary>Covers at least one pixel.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>Scissor rectangle in framebuffer pixels (GL origin: lower-left, y-up).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ScissorRect(int X, int Y, int Width, int Height)
{
    /// <summary>Covers at least one pixel.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
