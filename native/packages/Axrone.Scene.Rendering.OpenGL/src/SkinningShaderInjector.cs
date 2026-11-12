namespace Axrone.Scene.Rendering.OpenGL;

/// <summary>
/// Injects GPU skinning into an existing material vertex shader. The material
/// keeps its own attributes, uniforms and fragment stage; only position reads
/// are routed through a bone-texture skinning helper.
/// </summary>
internal static class SkinningShaderInjector
{
    /// <summary>Texels occupied by one bone matrix in the bone texture.</summary>
    public const int BoneTexelsPerBone = 4;

    /// <summary>Uniform name of the bone texture sampler.</summary>
    public const string BoneTextureUniform = "u_SkinBones";

    /// <summary>Uniform name of the active bone count.</summary>
    public const string BoneCountUniform = "u_SkinBoneCount";

    private const string PositionDeclaration = "in vec3 a_Position";
    private const string NormalDeclaration = "in vec3 a_Normal";
    private const string TangentDeclaration = "in vec4 a_Tangent";
    private const string ProtectedPositionDeclaration = "__AXRONE_SKIN_POSITION__";
    private const string ProtectedNormalDeclaration = "__AXRONE_SKIN_NORMAL__";
    private const string ProtectedTangentDeclaration = "__AXRONE_SKIN_TANGENT__";

    private const string Prelude = """
        layout(location = 6) in ivec4 a_SkinJoints;
        layout(location = 7) in vec4 a_SkinWeights;
        uniform sampler2D u_SkinBones;
        uniform int u_SkinBoneCount;

        mat4 ReadSkinBone(int joint)
        {
            int base = clamp(joint, 0, u_SkinBoneCount - 1) * 4;
            return mat4(
                texelFetch(u_SkinBones, ivec2(base + 0, 0), 0),
                texelFetch(u_SkinBones, ivec2(base + 1, 0), 0),
                texelFetch(u_SkinBones, ivec2(base + 2, 0), 0),
                texelFetch(u_SkinBones, ivec2(base + 3, 0), 0));
        }

        mat4 ReadSkinMatrix()
        {
            return ReadSkinBone(a_SkinJoints.x) * a_SkinWeights.x
                 + ReadSkinBone(a_SkinJoints.y) * a_SkinWeights.y
                 + ReadSkinBone(a_SkinJoints.z) * a_SkinWeights.z
                 + ReadSkinBone(a_SkinJoints.w) * a_SkinWeights.w;
        }

        vec3 ApplySkin(vec3 position)
        {
            return (vec4(position, 1.0) * ReadSkinMatrix()).xyz;
        }

        vec3 ApplySkinNormal(vec3 normal)
        {
            mat3 normalBasis = transpose(inverse(mat3(ReadSkinMatrix())));
            return normalize(normal * normalBasis);
        }

        vec4 ApplySkinTangent(vec4 tangent)
        {
            return vec4(ApplySkinNormal(tangent.xyz), tangent.w);
        }
        """;

    /// <summary>Rewrites a material vertex shader into a skinned variant.</summary>
    public static bool TryInject(string vertexSource, out string injected) =>
        TryInject(vertexSource, out injected, out _);

    /// <summary>
    /// Rewrites a material vertex shader into a skinned variant and reports the
    /// exact incompatibility when the shader cannot be skinned safely.
    /// </summary>
    public static bool TryInject(
        string vertexSource,
        out string injected,
        out SkinningInjectionFailure failure)
    {
        injected = vertexSource;

        if (string.IsNullOrEmpty(vertexSource))
        {
            failure = SkinningInjectionFailure.EmptySource;
            return false;
        }

        int versionEnd = vertexSource.IndexOf('\n', StringComparison.Ordinal);
        if (versionEnd < 0 ||
            !vertexSource.AsSpan(0, versionEnd).TrimStart().StartsWith("#version", StringComparison.Ordinal))
        {
            failure = SkinningInjectionFailure.MissingVersionDirective;
            return false;
        }

        if (!vertexSource.Contains(PositionDeclaration, StringComparison.Ordinal))
        {
            failure = SkinningInjectionFailure.MissingPositionAttribute;
            return false;
        }

        if (vertexSource.Contains("a_SkinJoints", StringComparison.Ordinal) ||
            vertexSource.Contains("a_SkinWeights", StringComparison.Ordinal) ||
            vertexSource.Contains(BoneTextureUniform, StringComparison.Ordinal) ||
            vertexSource.Contains(BoneCountUniform, StringComparison.Ordinal))
        {
            failure = SkinningInjectionFailure.ExistingSkinningDeclarations;
            return false;
        }

        if (UsesAttributeLocation(vertexSource, 6) || UsesAttributeLocation(vertexSource, 7))
        {
            failure = SkinningInjectionFailure.AttributeLocationConflict;
            return false;
        }

        string rewritten = RewriteAttribute(
            vertexSource,
            PositionDeclaration,
            ProtectedPositionDeclaration,
            "a_Position",
            "ApplySkin(a_Position)");
        rewritten = RewriteAttribute(
            rewritten,
            NormalDeclaration,
            ProtectedNormalDeclaration,
            "a_Normal",
            "ApplySkinNormal(a_Normal)");
        rewritten = RewriteAttribute(
            rewritten,
            TangentDeclaration,
            ProtectedTangentDeclaration,
            "a_Tangent",
            "ApplySkinTangent(a_Tangent)");
        injected = rewritten.Insert(versionEnd + 1, Prelude + "\n");
        failure = SkinningInjectionFailure.None;
        return true;
    }

    private static string RewriteAttribute(
        string source,
        string declaration,
        string protectedDeclaration,
        string usage,
        string replacement)
    {
        if (!source.Contains(declaration, StringComparison.Ordinal))
        {
            return source;
        }

        string rewritten = source.Replace(declaration, protectedDeclaration, StringComparison.Ordinal);
        rewritten = rewritten.Replace(usage, replacement, StringComparison.Ordinal);
        return rewritten.Replace(protectedDeclaration, declaration, StringComparison.Ordinal);
    }

    private static bool UsesAttributeLocation(string source, int location)
    {
        int index = 0;
        while ((index = source.IndexOf("location", index, StringComparison.Ordinal)) >= 0)
        {
            int cursor = index + "location".Length;
            while (cursor < source.Length && char.IsWhiteSpace(source[cursor]))
            {
                cursor++;
            }

            if (cursor < source.Length && source[cursor] == '=')
            {
                cursor++;
                while (cursor < source.Length && char.IsWhiteSpace(source[cursor]))
                {
                    cursor++;
                }

                if (cursor < source.Length &&
                    source[cursor] == (char)('0' + location) &&
                    (cursor + 1 >= source.Length || !char.IsDigit(source[cursor + 1])))
                {
                    return true;
                }
            }

            index += "location".Length;
        }

        return false;
    }
}
