namespace Axrone.Render.OpenGL.Shading;

/// <summary>
/// Built-in GLSL 330 core shader sources for the standard unlit and lit pipelines.
/// </summary>
/// <remarks>
/// <para>
/// Names mirror the WebGL2 standard shader templates: attributes <c>a_Position</c> and
/// <c>a_TexCoord</c>, uniforms <c>u_MVPMatrix</c>, <c>u_Color</c> and <c>u_MainTexture</c>,
/// varying <c>v_TexCoord</c> and fragment output <c>o_FragColor</c>.
/// </para>
/// <para>
/// Attribute names are adapted to the native vertex layout from
/// <see cref="MeshGenerators.DefaultLayout"/>: position at location 0 (<c>a_Position</c>),
/// normal at location 1 (<c>a_Normal</c>) and UV at location 2 (<c>a_UV0</c>).
/// The native layout has no color attribute, so none is declared.
/// </para>
/// <para>
/// The lit (Standard) shaders add <c>u_ModelMatrix</c> (world-space normal transform,
/// assumes uniform scale), <c>u_LightDirection</c>, <c>u_LightColor</c> and
/// <c>u_AmbientColor</c> for simple diffuse lighting.
/// </para>
/// </remarks>
public static class StandardShaders
{
    /// <summary>
    /// Unlit vertex shader: MVP transform, passes UV to the fragment stage.
    /// </summary>
    public const string UnlitVertex = """
        #version 330 core

        in vec3 a_Position;
        in vec2 a_UV0;

        uniform mat4 u_MVPMatrix;

        out vec2 v_TexCoord;

        void main()
        {
            gl_Position = u_MVPMatrix * vec4(a_Position, 1.0);
            v_TexCoord = a_UV0;
        }
        """;

    /// <summary>
    /// Unlit fragment shader: vertex color modulated by the main texture.
    /// </summary>
    public const string UnlitFragment = """
        #version 330 core

        in vec2 v_TexCoord;

        uniform vec4 u_Color;
        uniform sampler2D u_MainTexture;

        out vec4 o_FragColor;

        void main()
        {
            o_FragColor = u_Color * texture(u_MainTexture, v_TexCoord);
        }
        """;

    /// <summary>
    /// Lit vertex shader: MVP transform, world-space normal and UV to the fragment stage.
    /// </summary>
    public const string StandardVertex = """
        #version 330 core

        in vec3 a_Position;
        in vec3 a_Normal;
        in vec2 a_UV0;

        uniform mat4 u_ModelMatrix;
        uniform mat4 u_MVPMatrix;

        out vec2 v_TexCoord;
        out vec3 v_Normal;

        void main()
        {
            gl_Position = u_MVPMatrix * vec4(a_Position, 1.0);
            v_Normal = mat3(u_ModelMatrix) * a_Normal;
            v_TexCoord = a_UV0;
        }
        """;

    /// <summary>
    /// Lit fragment shader: simple diffuse lighting, vertex color and main texture.
    /// </summary>
    public const string StandardFragment = """
        #version 330 core

        in vec2 v_TexCoord;
        in vec3 v_Normal;

        uniform vec4 u_Color;
        uniform sampler2D u_MainTexture;
        uniform vec3 u_LightDirection;
        uniform vec3 u_LightColor;
        uniform vec3 u_AmbientColor;

        out vec4 o_FragColor;

        void main()
        {
            vec3 normal = normalize(v_Normal);
            float diffuse = max(dot(normal, normalize(u_LightDirection)), 0.0);
            vec3 lighting = u_AmbientColor + u_LightColor * diffuse;
            o_FragColor = u_Color * vec4(lighting, 1.0) * texture(u_MainTexture, v_TexCoord);
        }
        """;
}
