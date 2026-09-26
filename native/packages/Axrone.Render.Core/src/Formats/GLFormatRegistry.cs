using System.Collections.Frozen;

namespace Axrone.Render.Core;

/// <summary>
/// Describes a texture format with its GL mapping and properties.
/// </summary>
/// <param name="InternalFormat">The GL internal format.</param>
/// <param name="Format">The GL pixel format.</param>
/// <param name="Type">The GL pixel type.</param>
/// <param name="BytesPerPixel">Bytes per pixel.</param>
/// <param name="HasDepth">Whether this format has a depth component.</param>
/// <param name="HasStencil">Whether this format has a stencil component.</param>
public readonly record struct FormatDescriptor(
    uint InternalFormat,
    uint Format,
    uint Type,
    byte BytesPerPixel,
    bool HasDepth,
    bool HasStencil);

/// <summary>
/// Registry of GL format descriptors for texture allocation.
/// </summary>
public static class GLFormatRegistry
{
    /// <summary>
    /// Gets the allocator format descriptors keyed by format name.
    /// </summary>
    public static FrozenDictionary<string, FormatDescriptor> AllocatorFormats { get; }

    static GLFormatRegistry()
    {
        // GL constants (matching OpenGL 3.3+ core profile)
        const uint GL_RED = 0x1903;
        const uint GL_RG = 0x8227;
        const uint GL_RGB = 0x1907;
        const uint GL_RGBA = 0x1908;
        const uint GL_DEPTH_COMPONENT = 0x1902;
        const uint GL_DEPTH_STENCIL = 0x84F9;

        const uint GL_R11F_G11F_B10F = 0x8C3A;
        const uint GL_RGBA8 = 0x8058;
        const uint GL_RGBA16F = 0x881A;
        const uint GL_RGBA32F = 0x8814;
        const uint GL_RG16F = 0x822F;
        const uint GL_RG32F = 0x8230;
        const uint GL_R16F = 0x822D;
        const uint GL_R32F = 0x822E;
        const uint GL_DEPTH_COMPONENT24 = 0x81A6;
        const uint GL_DEPTH_COMPONENT32F = 0x8CAC;
        const uint GL_DEPTH24_STENCIL8 = 0x88F0;

        const uint GL_UNSIGNED_BYTE = 0x1401;
        const uint GL_FLOAT = 0x1406;
        const uint GL_HALF_FLOAT = 0x140B;
        const uint GL_UNSIGNED_INT = 0x1405;
        const uint GL_UNSIGNED_INT_24_8 = 0x84FA;

        var dict = new Dictionary<string, FormatDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            ["r11g11b10f"] = new(GL_R11F_G11F_B10F, GL_RGB, GL_FLOAT, 4, false, false),
            ["rgba8"] = new(GL_RGBA8, GL_RGBA, GL_UNSIGNED_BYTE, 4, false, false),
            ["rgba16f"] = new(GL_RGBA16F, GL_RGBA, GL_HALF_FLOAT, 8, false, false),
            ["rgba32f"] = new(GL_RGBA32F, GL_RGBA, GL_FLOAT, 16, false, false),
            ["rg16f"] = new(GL_RG16F, GL_RG, GL_HALF_FLOAT, 4, false, false),
            ["rg32f"] = new(GL_RG32F, GL_RG, GL_FLOAT, 8, false, false),
            ["r16f"] = new(GL_R16F, GL_RED, GL_HALF_FLOAT, 2, false, false),
            ["r32f"] = new(GL_R32F, GL_RED, GL_FLOAT, 4, false, false),
            ["depth24"] = new(GL_DEPTH_COMPONENT24, GL_DEPTH_COMPONENT, GL_UNSIGNED_INT, 4, true, false),
            ["depth32f"] = new(GL_DEPTH_COMPONENT32F, GL_DEPTH_COMPONENT, GL_FLOAT, 4, true, false),
            ["depth24-stencil8"] = new(GL_DEPTH24_STENCIL8, GL_DEPTH_STENCIL, GL_UNSIGNED_INT_24_8, 4, true, true)
        };

        AllocatorFormats = dict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tries to get a format descriptor by name.
    /// </summary>
    /// <param name="formatName">The format name.</param>
    /// <param name="descriptor">The format descriptor if found.</param>
    /// <returns>True if the format was found; otherwise, false.</returns>
    public static bool TryGetFormat(string formatName, out FormatDescriptor descriptor)
    {
        return AllocatorFormats.TryGetValue(formatName, out descriptor);
    }

    /// <summary>
    /// Gets a format descriptor by name, throwing if not found.
    /// </summary>
    /// <param name="formatName">The format name.</param>
    /// <returns>The format descriptor.</returns>
    /// <exception cref="ArgumentException">Thrown when the format is not found.</exception>
    public static FormatDescriptor GetFormat(string formatName)
    {
        if (!TryGetFormat(formatName, out var descriptor))
            ThrowHelper.ThrowInvalidArgument($"Unknown format: {formatName}");

        return descriptor;
    }

    /// <summary>
    /// Checks if a format name is supported.
    /// </summary>
    /// <param name="formatName">The format name.</param>
    /// <returns>True if the format is supported; otherwise, false.</returns>
    public static bool IsFormatSupported(string formatName)
    {
        return AllocatorFormats.ContainsKey(formatName);
    }

    /// <summary>
    /// Gets all supported format names.
    /// </summary>
    public static IEnumerable<string> SupportedFormats => AllocatorFormats.Keys;
}
