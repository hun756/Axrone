using System.Collections.Frozen;

namespace Axrone.Render.OpenGL.Context;

/// <summary>
/// Registry of GL extensions with WEBGL→desktop name mapping.
/// </summary>
public sealed class GLExtensionRegistry
{
    /// <summary>
    /// Mapping of WebGL extension names to their desktop OpenGL equivalents.
    /// </summary>
    private static readonly FrozenDictionary<string, string> WebGlToDesktopMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["WEBGL_compressed_texture_s3tc"] = "GL_EXT_texture_compression_s3tc",
        ["WEBGL_compressed_texture_s3tc_srgb"] = "GL_EXT_texture_compression_s3tc_srgb",
        ["WEBGL_compressed_texture_etc"] = "GL_ARB_ES3_compatibility",
        ["WEBGL_compressed_texture_etc1"] = "GL_OES_compressed_ETC1_RGB8_texture",
        ["WEBGL_compressed_texture_pvrtc"] = "GL_IMG_texture_compression_pvrtc",
        ["WEBGL_compressed_texture_astc"] = "GL_KHR_texture_compression_astc_ldr",
        ["WEBGL_compressed_texture_atc"] = "GL_AMD_compressed_ATC_texture",
        ["WEBGL_depth_texture"] = "GL_ARB_depth_texture",
        ["WEBGL_draw_buffers"] = "GL_ARB_draw_buffers",
        ["EXT_texture_filter_anisotropic"] = "GL_EXT_texture_filter_anisotropic",
        ["EXT_color_buffer_float"] = "GL_ARB_color_buffer_float",
        ["EXT_color_buffer_half_float"] = "GL_ARB_half_float_pixel",
        ["EXT_disjoint_timer_query"] = "GL_EXT_timer_query",
        ["EXT_disjoint_timer_query_webgl2"] = "GL_EXT_query_counter",
        ["OES_texture_float_linear"] = "GL_ARB_texture_float",
        ["OES_texture_half_float_linear"] = "GL_ARB_texture_float",
        ["OES_standard_derivatives"] = "GL_ARB_fragment_shader",
        ["OES_element_index_uint"] = "GL_OES_element_index_uint",
        ["KHR_parallel_shader_compile"] = "GL_KHR_parallel_shader_compile",
        ["KHR_debug"] = "GL_KHR_debug"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _supported = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="GLExtensionRegistry"/> class.
    /// </summary>
    /// <param name="gl">The GL API.</param>
    public GLExtensionRegistry(IGLApi gl)
    {
        ArgumentNullException.ThrowIfNull(gl);

        // Query all supported extensions
        gl.GetInteger(0x821D, out int numExtensions); // GL_NUM_EXTENSIONS
        for (uint i = 0; i < (uint)numExtensions; i++)
        {
            string? ext = gl.GetString(0x1F03, i); // GL_EXTENSIONS
            if (!string.IsNullOrEmpty(ext))
            {
                _supported.Add(ext);
            }
        }
    }

    /// <summary>
    /// Checks if an extension is supported.
    /// </summary>
    /// <param name="extensionName">The extension name (WEBGL or desktop GL name).</param>
    /// <returns>True if supported; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSupported(string extensionName)
    {
        if (_supported.Contains(extensionName))
            return true;

        // Try WEBGL→desktop mapping
        if (WebGlToDesktopMap.TryGetValue(extensionName, out var desktopName))
            return _supported.Contains(desktopName);

        return false;
    }

    /// <summary>
    /// Requires an extension, throwing if not supported.
    /// </summary>
    /// <param name="extensionName">The extension name.</param>
    /// <exception cref="GLException">Thrown when the extension is not supported.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Require(string extensionName)
    {
        if (!IsSupported(extensionName))
            ThrowHelper.ThrowExtensionNotSupported(extensionName);
    }

    /// <summary>
    /// Gets all supported extensions.
    /// </summary>
    public IEnumerable<string> SupportedExtensions => _supported;

    /// <summary>
    /// Gets the count of supported extensions.
    /// </summary>
    public int Count => _supported.Count;

    /// <inheritdoc/>
    public override string ToString() => $"GLExtensionRegistry: {_supported.Count} extensions";
}
