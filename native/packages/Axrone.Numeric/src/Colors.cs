namespace Axrone.Numeric;

/// <summary>Enterprise predefined palette.</summary>
public static class Colors
{
    /// <summary>Transparent black.</summary>
    public static Rgba32 Transparent => new(0, 0, 0, 0);

    /// <summary>Opaque black.</summary>
    public static Rgba32 Black => new(0, 0, 0, 255);

    /// <summary>Opaque white.</summary>
    public static Rgba32 White => new(255, 255, 255, 255);

    /// <summary>Opaque red.</summary>
    public static Rgba32 Red => new(255, 0, 0, 255);

    /// <summary>Opaque green.</summary>
    public static Rgba32 Green => new(0, 128, 0, 255);

    /// <summary>Opaque lime.</summary>
    public static Rgba32 Lime => new(0, 255, 0, 255);

    /// <summary>Opaque blue.</summary>
    public static Rgba32 Blue => new(0, 0, 255, 255);

    /// <summary>Opaque yellow.</summary>
    public static Rgba32 Yellow => new(255, 255, 0, 255);

    /// <summary>Opaque cyan.</summary>
    public static Rgba32 Cyan => new(0, 255, 255, 255);

    /// <summary>Opaque magenta.</summary>
    public static Rgba32 Magenta => new(255, 0, 255, 255);

    /// <summary>Opaque gray.</summary>
    public static Rgba32 Gray => new(128, 128, 128, 255);

    /// <summary>Opaque light gray.</summary>
    public static Rgba32 LightGray => new(211, 211, 211, 255);

    /// <summary>Opaque dark gray.</summary>
    public static Rgba32 DarkGray => new(169, 169, 169, 255);

    /// <summary>Opaque orange.</summary>
    public static Rgba32 Orange => new(255, 165, 0, 255);

    /// <summary>Opaque purple.</summary>
    public static Rgba32 Purple => new(128, 0, 128, 255);
}
