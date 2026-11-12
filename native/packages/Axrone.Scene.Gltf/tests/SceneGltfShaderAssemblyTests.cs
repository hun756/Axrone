using System.Text.RegularExpressions;
using Axrone.Scene.Assets;
using Axrone.Scene.Gltf;
using FluentAssertions;

namespace Axrone.Scene.Gltf.Tests;

/// <summary>
/// Regression locks for the built-in glTF shader assembly: desktop GL rejects
/// two-argument <c>clamp</c>, and the ambient term must not be gated by
/// view-dependent Fresnel or grazing faces render black.
/// </summary>
public sealed class SceneGltfShaderAssemblyTests
{
    private static readonly Regex ClampCall = new(@"clamp\(", RegexOptions.Compiled);

    [Fact]
    public void PbrFragment_AppliesAmbientWithoutFresnelGating()
    {
        SceneShaderDefinition shader = SceneGltfShaders.CreatePbr();

        shader.FragmentSource.Should().Contain(
            "u_AmbientLight * albedo * (1.0 - metallic)",
            "ambient diffuse must not be multiplied by a Fresnel term");
    }

    [Fact]
    public void PbrFragment_OffersTheMatteEditorPreviewLighting()
    {
        SceneShaderDefinition shader = SceneGltfShaders.CreatePbr();

        shader.FragmentSource.Should().Contain("u_EditorPreview == 1");
        shader.FragmentSource.Should().Contain("mix(u_GroundLight, u_SkyLight, hemiFactor)");
        shader.FragmentSource.Should().Contain("u_AmbientLight * 0.45");
        shader.Uniforms.Should().Contain(["u_EditorPreview", "u_SkyLight", "u_GroundLight"]);
    }

    [Theory]
    [InlineData("gltf/pbr")]
    [InlineData("gltf/unlit")]
    [InlineData("gltf/toon")]
    public void BuiltInShaders_ApplyTheFrameOutputGamma(string shaderId)
    {
        SceneGltfShaders.TryCreate(shaderId, names: null, out SceneShaderDefinition? shader)
            .Should().BeTrue();

        shader!.FragmentSource.Should().Contain("u_OutputGamma");
        shader.FragmentSource.Should().Contain("vec3(u_OutputGamma)");
    }

    [Theory]
    [InlineData("gltf/pbr")]
    [InlineData("gltf/unlit")]
    [InlineData("gltf/toon")]
    [InlineData("gltf/pbr/blend")]
    [InlineData("gltf/pbr/double-sided")]
    [InlineData("gltf/unlit/blend/double-sided")]
    public void AssembledSources_NeverCallTwoArgumentClamp(string shaderId)
    {
        SceneGltfShaders.TryCreate(shaderId, names: null, out SceneShaderDefinition? shader)
            .Should().BeTrue();

        foreach (string source in new[] { shader!.VertexSource, shader.FragmentSource })
        {
            foreach (Match match in ClampCall.Matches(source))
            {
                CountArguments(source, match.Index + match.Length - 1)
                    .Should().Be(3, $"{shaderId} must use the GLSL three-argument clamp");
            }
        }
    }

    private static int CountArguments(string source, int openParenthesisIndex)
    {
        int depth = 0;
        int arguments = 1;

        for (int index = openParenthesisIndex; index < source.Length; index++)
        {
            char character = source[index];

            if (character == '(')
            {
                depth++;
            }
            else if (character == ')')
            {
                depth--;

                if (depth == 0)
                {
                    return arguments;
                }
            }
            else if (character == ',' && depth == 1)
            {
                arguments++;
            }
        }

        return arguments;
    }
}
