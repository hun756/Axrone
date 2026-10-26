namespace Axrone.Scene.Rendering;

/// <summary>
/// World-space bounds and matrix helpers shared by the 2D and 3D render paths
/// and by backend renderers.
/// </summary>
public static class SceneBounds
{
    /// <summary>
    /// Promotes a 2D affine matrix to the row-vector 4x4 layout used by the
    /// renderer, moving the translation from row 3 to row 4.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Mat4 FromAffine2D(in Mat3 affine) =>
        new(
            affine.M11, affine.M12, 0.0f, 0.0f,
            affine.M21, affine.M22, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f, 0.0f,
            affine.M31, affine.M32, 0.0f, 1.0f);

    /// <summary>Transforms a local sphere by a world matrix, scaling by the largest basis axis.</summary>
    public static BoundingSphere3 TransformSphere(in Mat4 worldMatrix, in BoundingSphere3 local)
    {
        Vec4 center = worldMatrix * new Vec4(local.Center, 1.0f);

        float scaleX = MathF.Sqrt(
            (worldMatrix.M11 * worldMatrix.M11) +
            (worldMatrix.M12 * worldMatrix.M12) +
            (worldMatrix.M13 * worldMatrix.M13));
        float scaleY = MathF.Sqrt(
            (worldMatrix.M21 * worldMatrix.M21) +
            (worldMatrix.M22 * worldMatrix.M22) +
            (worldMatrix.M23 * worldMatrix.M23));
        float scaleZ = MathF.Sqrt(
            (worldMatrix.M31 * worldMatrix.M31) +
            (worldMatrix.M32 * worldMatrix.M32) +
            (worldMatrix.M33 * worldMatrix.M33));

        float maxScale = MathF.Max(scaleX, MathF.Max(scaleY, scaleZ));
        return new BoundingSphere3(
            new Vec3(center.X, center.Y, center.Z),
            local.Radius * maxScale);
    }
}
