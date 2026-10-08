namespace Axrone.Render.Core;

/// <summary>Viewport rectangle in framebuffer pixels (GL origin: lower-left, y-up).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ViewportRect(int X, int Y, int Width, int Height) : IDescriptor<ViewportRect>
{
    /// <summary>Covers at least one pixel.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Validate(in ViewportRect descriptor)
    {
        if (descriptor.Width < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(descriptor), descriptor.Width, "Viewport width cannot be negative.");
        }

        if (descriptor.Height < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(descriptor), descriptor.Height, "Viewport height cannot be negative.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Describe(in ViewportRect descriptor) => descriptor.ToString();
}

/// <summary>Scissor rectangle in framebuffer pixels (GL origin: lower-left, y-up).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ScissorRect(int X, int Y, int Width, int Height) : IDescriptor<ScissorRect>
{
    /// <summary>Covers at least one pixel.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Validate(in ScissorRect descriptor)
    {
        if (descriptor.Width < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(descriptor), descriptor.Width, "Scissor width cannot be negative.");
        }

        if (descriptor.Height < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(descriptor), descriptor.Height, "Scissor height cannot be negative.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Describe(in ScissorRect descriptor) => descriptor.ToString();
}
