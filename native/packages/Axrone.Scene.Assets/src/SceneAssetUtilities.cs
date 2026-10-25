namespace Axrone.Scene.Assets;

using System.Buffers.Binary;

/// <summary>
/// Resolves mesh bounds from the position attribute when a definition does not
/// declare them. Useful for procedural or imported meshes where authoring
/// bounds are unavailable but culling still needs a conservative sphere.
/// </summary>
public static class SceneMeshBounds
{
    private const int PositionFloatBytes = 12;

    /// <summary>Returns declared bounds, or computes them from vertex positions.</summary>
    public static BoundingSphere3? Resolve(SceneMeshDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Bounds is { } declared)
        {
            return declared;
        }

        return TryCompute(definition, out BoundingSphere3 computed) ? computed : null;
    }

    /// <summary>Computes a bounding sphere from the interleaved position attribute.</summary>
    public static bool TryCompute(SceneMeshDefinition definition, out BoundingSphere3 bounds)
    {
        ArgumentNullException.ThrowIfNull(definition);

        bounds = default;

        if (!TryFindPositionAttribute(definition.Attributes, out SceneVertexAttribute position))
        {
            return false;
        }

        int stride = definition.VertexStride;
        int offset = position.Offset;
        if (offset + PositionFloatBytes > stride)
        {
            return false;
        }

        int vertexCount = definition.VertexCount;
        ReadOnlySpan<byte> vertices = definition.Vertices.Span;
        long required = (long)vertexCount * stride;
        if (vertexCount <= 0 || vertices.Length < required)
        {
            return false;
        }

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float minZ = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        float maxZ = float.NegativeInfinity;

        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            ReadOnlySpan<byte> positionBytes = vertices.Slice(
                (vertexIndex * stride) + offset,
                PositionFloatBytes);

            float x = BinaryPrimitives.ReadSingleLittleEndian(positionBytes);
            float y = BinaryPrimitives.ReadSingleLittleEndian(positionBytes[4..]);
            float z = BinaryPrimitives.ReadSingleLittleEndian(positionBytes[8..]);

            minX = MathF.Min(minX, x);
            minY = MathF.Min(minY, y);
            minZ = MathF.Min(minZ, z);
            maxX = MathF.Max(maxX, x);
            maxY = MathF.Max(maxY, y);
            maxZ = MathF.Max(maxZ, z);
        }

        if (!float.IsFinite(minX) || !float.IsFinite(maxX) ||
            !float.IsFinite(minY) || !float.IsFinite(maxY) ||
            !float.IsFinite(minZ) || !float.IsFinite(maxZ))
        {
            return false;
        }

        var center = new Vec3(
            (minX + maxX) * 0.5f,
            (minY + maxY) * 0.5f,
            (minZ + maxZ) * 0.5f);

        // The AABB half-diagonal overestimates round meshes. One more pass
        // finds the farthest vertex so spheres and cylinders keep tight
        // culling bounds at authoring cost only.
        float radiusSquared = 0.0f;
        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            ReadOnlySpan<byte> positionBytes = vertices.Slice(
                (vertexIndex * stride) + offset,
                PositionFloatBytes);

            var vertex = new Vec3(
                BinaryPrimitives.ReadSingleLittleEndian(positionBytes),
                BinaryPrimitives.ReadSingleLittleEndian(positionBytes[4..]),
                BinaryPrimitives.ReadSingleLittleEndian(positionBytes[8..]));

            radiusSquared = MathF.Max(radiusSquared, Vec3.DistanceSquared(center, vertex));
        }

        bounds = new BoundingSphere3(center, MathF.Sqrt(radiusSquared));
        return true;
    }

    private static bool TryFindPositionAttribute(
        IReadOnlyList<SceneVertexAttribute> attributes,
        out SceneVertexAttribute position)
    {
        for (int i = 0; i < attributes.Count; i++)
        {
            SceneVertexAttribute attribute = attributes[i];
            if (attribute.Semantic == SceneMeshSemantic.Position &&
                attribute.ComponentCount >= 3 &&
                !attribute.Integer)
            {
                position = attribute;
                return true;
            }
        }

        position = default;
        return false;
    }
}

/// <summary>One surface feature and the shader keyword it toggles.</summary>
public readonly record struct SceneMaterialKeyword(
    SceneMaterialFeatures Feature,
    string Keyword);

/// <summary>
/// Maps material surface features to shader preprocessor keywords, mirroring
/// the scene authoring model so backend shader variants stay in sync.
/// </summary>
public static class SceneMaterialKeywordMap
{
    private static readonly SceneMaterialKeyword[] s_keywords =
    [
        new(SceneMaterialFeatures.VertexColor, "VERTEX_COLOR"),
        new(SceneMaterialFeatures.SecondUv, "SECOND_UV"),
        new(SceneMaterialFeatures.NormalMap, "NORMAL_MAPPING"),
        new(SceneMaterialFeatures.TwoSided, "TWO_SIDED"),
        new(SceneMaterialFeatures.AlbedoMap, "ALBEDO_MAP"),
        new(SceneMaterialFeatures.PbrMap, "PBR_MAP"),
        new(SceneMaterialFeatures.MetallicRoughnessMap, "METALLIC_ROUGHNESS_MAP"),
        new(SceneMaterialFeatures.OcclusionMap, "OCCLUSION"),
        new(SceneMaterialFeatures.EmissiveMap, "EMISSION"),
        new(SceneMaterialFeatures.Clearcoat, "CLEARCOAT"),
        new(SceneMaterialFeatures.ClearcoatMap, "CLEARCOAT_MAP"),
        new(SceneMaterialFeatures.ClearcoatRoughnessMap, "CLEARCOAT_ROUGHNESS_MAP"),
        new(SceneMaterialFeatures.ClearcoatNormalMap, "CLEARCOAT_NORMAL_MAP"),
        new(SceneMaterialFeatures.AlphaTest, "ALPHA_TEST"),
        new(SceneMaterialFeatures.Anisotropy, "ANISOTROPY"),
        new(SceneMaterialFeatures.Sheen, "SHEEN"),
        new(SceneMaterialFeatures.Subsurface, "SUBSURFACE"),
        new(SceneMaterialFeatures.Transmission, "TRANSMISSION"),
        new(SceneMaterialFeatures.Iridescence, "IRIDESCENCE")
    ];

    /// <summary>Every feature/keyword pair in declaration order.</summary>
    public static ReadOnlySpan<SceneMaterialKeyword> Keywords => s_keywords;

    /// <summary>Appends the keywords enabled by a feature mask to a collection.</summary>
    public static void Collect(SceneMaterialFeatures features, ICollection<string> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        for (int i = 0; i < s_keywords.Length; i++)
        {
            SceneMaterialKeyword entry = s_keywords[i];
            if ((features & entry.Feature) != 0)
            {
                destination.Add(entry.Keyword);
            }
        }
    }

    /// <summary>Returns the keywords enabled by a feature mask.</summary>
    public static string[] Resolve(SceneMaterialFeatures features)
    {
        int count = 0;
        for (int i = 0; i < s_keywords.Length; i++)
        {
            if ((features & s_keywords[i].Feature) != 0)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return [];
        }

        var result = new string[count];
        int index = 0;
        for (int i = 0; i < s_keywords.Length; i++)
        {
            SceneMaterialKeyword entry = s_keywords[i];
            if ((features & entry.Feature) != 0)
            {
                result[index++] = entry.Keyword;
            }
        }

        return result;
    }

    /// <summary>Resolves a keyword name back to its feature flag.</summary>
    public static bool TryGetFeature(string keyword, out SceneMaterialFeatures feature)
    {
        ArgumentNullException.ThrowIfNull(keyword);

        for (int i = 0; i < s_keywords.Length; i++)
        {
            if (string.Equals(s_keywords[i].Keyword, keyword, StringComparison.Ordinal))
            {
                feature = s_keywords[i].Feature;
                return true;
            }
        }

        feature = SceneMaterialFeatures.None;
        return false;
    }
}
