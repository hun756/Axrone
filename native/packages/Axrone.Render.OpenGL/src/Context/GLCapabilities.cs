using Axrone.Render.Core.Abstractions;

namespace Axrone.Render.OpenGL.Context;

/// <summary>
/// Immutable snapshot of GL capabilities queried at context creation.
/// </summary>
public sealed class GLCapabilities
{
    /// <summary>Gets the maximum texture size.</summary>
    public int MaxTextureSize { get; }

    /// <summary>Gets the maximum cube map texture size.</summary>
    public int MaxCubeMapTextureSize { get; }

    /// <summary>Gets the maximum 3D texture size.</summary>
    public int Max3DTextureSize { get; }

    /// <summary>Gets the maximum array texture layers.</summary>
    public int MaxArrayTextureLayers { get; }

    /// <summary>Gets the maximum vertex attributes.</summary>
    public int MaxVertexAttribs { get; }

    /// <summary>Gets the maximum combined texture image units.</summary>
    public int MaxCombinedTextureImageUnits { get; }

    /// <summary>Gets the maximum texture image units.</summary>
    public int MaxTextureImageUnits { get; }

    /// <summary>Gets the maximum renderbuffer size.</summary>
    public int MaxRenderbufferSize { get; }

    /// <summary>Gets the maximum viewport dimensions.</summary>
    public (int Width, int Height) MaxViewportDims { get; }

    /// <summary>Gets the maximum samples for multisampling.</summary>
    public int MaxSamples { get; }

    /// <summary>Gets the vendor string.</summary>
    public string Vendor { get; }

    /// <summary>Gets the renderer string.</summary>
    public string Renderer { get; }

    /// <summary>Gets the version string.</summary>
    public string Version { get; }

    /// <summary>Gets the GLSL version string.</summary>
    public string ShadingLanguageVersion { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GLCapabilities"/> class.
    /// </summary>
    /// <param name="gl">The GL API.</param>
    public GLCapabilities(IGLApi gl)
    {
        ArgumentNullException.ThrowIfNull(gl);

        gl.GetInteger(0x0D33, out int maxTex); // GL_MAX_TEXTURE_SIZE
        MaxTextureSize = Math.Max(0, maxTex);

        gl.GetInteger(0x851C, out int maxCube); // GL_MAX_CUBE_MAP_TEXTURE_SIZE
        MaxCubeMapTextureSize = Math.Max(0, maxCube);

        gl.GetInteger(0x8073, out int max3D); // GL_MAX_3D_TEXTURE_SIZE
        Max3DTextureSize = Math.Max(0, max3D);

        gl.GetInteger(0x88FF, out int maxArray); // GL_MAX_ARRAY_TEXTURE_LAYERS
        MaxArrayTextureLayers = Math.Max(0, maxArray);

        gl.GetInteger(0x8869, out int maxAttribs); // GL_MAX_VERTEX_ATTRIBS
        MaxVertexAttribs = Math.Max(0, maxAttribs);

        gl.GetInteger(0x8B4D, out int maxCombined); // GL_MAX_COMBINED_TEXTURE_IMAGE_UNITS
        MaxCombinedTextureImageUnits = Math.Max(0, maxCombined);

        gl.GetInteger(0x8872, out int maxTexUnits); // GL_MAX_TEXTURE_IMAGE_UNITS
        MaxTextureImageUnits = Math.Max(0, maxTexUnits);

        gl.GetInteger(0x84E8, out int maxRB); // GL_MAX_RENDERBUFFER_SIZE
        MaxRenderbufferSize = Math.Max(0, maxRB);

        gl.GetInteger(0x0D3A, out int maxVPW); // GL_MAX_VIEWPORT_DIMS
        gl.GetInteger(0x0D3B, out int maxVPH);
        MaxViewportDims = (Math.Max(0, maxVPW), Math.Max(0, maxVPH));

        gl.GetInteger(0x8D57, out int maxSamples); // GL_MAX_SAMPLES
        MaxSamples = Math.Max(0, maxSamples);

        Vendor = gl.GetString(0x1F00) ?? "Unknown"; // GL_VENDOR
        Renderer = gl.GetString(0x1F01) ?? "Unknown"; // GL_RENDERER
        Version = gl.GetString(0x1F02) ?? "Unknown"; // GL_VERSION
        ShadingLanguageVersion = gl.GetString(0x8B8C) ?? "Unknown"; // GL_SHADING_LANGUAGE_VERSION
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"GLCapabilities: {Version} ({Renderer}), MaxTex={MaxTextureSize}, MaxAttribs={MaxVertexAttribs}";
}
