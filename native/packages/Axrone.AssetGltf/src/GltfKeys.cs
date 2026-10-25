namespace Axrone.AssetGltf;

/// <summary>Canonical asset keys shared by the import pipeline.</summary>
internal static class GltfKeys
{
    /// <summary>Builds the stable key for one mesh primitive.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Mesh(int meshIndex, int primitiveIndex) =>
        string.Create(CultureInfo.InvariantCulture, $"gltf/mesh/{meshIndex}:{primitiveIndex}");
}
