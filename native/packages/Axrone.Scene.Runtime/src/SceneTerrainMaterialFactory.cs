namespace Axrone.Scene.Runtime;

using Axrone.Scene.Assets;

/// <summary>
/// Registers the default terrain material and its shader. The shader consumes the standard
/// scene uniforms (model, view-projection, albedo and directional light) and leaves splat
/// layers to a caller-supplied material when one is provided.
/// </summary>
public static class SceneTerrainMaterialFactory
{
    /// <summary>Default shader asset name.</summary>
    public const string ShaderName = "terrain/default/shader";

    /// <summary>Default material asset name.</summary>
    public const string MaterialName = "terrain/default/material";

    private const string VertexSource = """
        #version 330 core

        in vec3 a_Position;
        in vec3 a_Normal;
        in vec2 a_UV0;

        uniform mat4 u_Model;
        uniform mat4 u_ViewProjection;

        out vec3 v_Normal;
        out vec2 v_UV;

        void main()
        {
            vec4 world = u_Model * vec4(a_Position, 1.0);
            gl_Position = u_ViewProjection * world;
            v_Normal = mat3(u_Model) * a_Normal;
            v_UV = a_UV0;
        }
        """;

    private const string FragmentSource = """
        #version 330 core

        in vec3 v_Normal;
        in vec2 v_UV;

        uniform vec4 u_Albedo;
        uniform vec3 u_LightDirection;
        uniform vec3 u_LightColor;
        uniform float u_LightIntensity;
        uniform vec3 u_AmbientLight;

        out vec4 o_FragColor;

        void main()
        {
            vec3 normal = normalize(v_Normal);
            vec3 lightDirection = length(u_LightDirection) > 1e-5
                ? normalize(u_LightDirection)
                : vec3(0.0, 1.0, 0.0);
            float diffuse = max(dot(normal, lightDirection), 0.0);
            vec3 light = u_AmbientLight + (u_LightColor * u_LightIntensity * diffuse);
            o_FragColor = vec4(u_Albedo.rgb * light, u_Albedo.a);
        }
        """;

    /// <summary>Ensures the default terrain shader and material exist.</summary>
    /// <param name="assets">Asset catalog to populate.</param>
    /// <returns>The default terrain material id.</returns>
    public static SceneAssetId<MaterialAssetTag> EnsureDefault(SceneAssetRegistry assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        SceneAssetId<ShaderAssetTag> shaderId = assets.CreateId<ShaderAssetTag>(ShaderName);
        if (!assets.Shaders.Contains(shaderId))
        {
            var attributes = new Dictionary<SceneMeshSemantic, string>
            {
                [SceneMeshSemantic.Position] = "a_Position",
                [SceneMeshSemantic.Normal] = "a_Normal",
                [SceneMeshSemantic.Uv0] = "a_UV0",
            };

            assets.Shaders.Register(new SceneShaderDefinition(
                shaderId,
                VertexSource,
                FragmentSource,
                uniforms:
                [
                    "u_Model",
                    "u_ViewProjection",
                    "u_Albedo",
                    "u_LightDirection",
                    "u_LightColor",
                    "u_LightIntensity",
                    "u_AmbientLight",
                ],
                attributes: attributes));
        }

        SceneAssetId<MaterialAssetTag> materialId = assets.CreateId<MaterialAssetTag>(MaterialName);
        if (!assets.Materials.Contains(materialId))
        {
            var surface = new SceneMaterialSurface
            {
                Albedo = new Vec4(0.36f, 0.52f, 0.28f, 1f),
                Roughness = 1f,
                Metallic = 0f,
            };

            assets.Materials.Register(new SceneMaterialDefinition(
                materialId,
                shaderId,
                surface,
                passes: [new SceneMaterialPass("main", cull: false)]));
        }

        return materialId;
    }
}
