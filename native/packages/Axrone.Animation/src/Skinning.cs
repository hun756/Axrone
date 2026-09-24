namespace Axrone.Animation;

/// <summary>Mesh skinning palette: joint-world × inverse-bind per bone.</summary>
public static class SkinningPalette
{
    /// <summary>Composes the palette from precomputed joint world matrices.</summary>
    [SkipLocalsInit]
    public static void ComputePalette(ReadOnlySpan<float> jointWorldMatrices, ReadOnlySpan<float> inverseBindMatrices, ReadOnlySpan<float> inverseMeshMatrix, Span<float> outPalette)
    {
        int boneCount = jointWorldMatrices.Length / 16;
        Span<float> composed = stackalloc float[16];

        for (int b = 0; b < boneCount; b++)
        {
            int offset = b * 16;
            ReadOnlySpan<float> jointWorld = jointWorldMatrices.Slice(offset, 16);

            if (inverseBindMatrices.IsEmpty)
            {
                jointWorld.CopyTo(outPalette.Slice(offset, 16));
                continue;
            }

            ReadOnlySpan<float> bind = inverseBindMatrices.Slice(offset, 16);
            FastMath.MultiplyMatrix4x4(jointWorld, bind, composed);
            FastMath.MultiplyMatrix4x4(inverseMeshMatrix, composed, outPalette.Slice(offset, 16));
        }
    }

    /// <summary>Composes the palette directly from a world pose.</summary>
    [SkipLocalsInit]
    public static void ComputeFromWorldPose(Rig rig, ReadOnlySpan<Vector3> worldT, ReadOnlySpan<Quaternion> worldR, ReadOnlySpan<Vector3> worldS, Span<float> outPalette)
    {
        ArgumentNullException.ThrowIfNull(rig);
        Span<float> jointWorld = stackalloc float[16];

        for (int b = 0; b < rig.BoneCount; b++)
        {
            FastMath.ComposeTransformMatrix(worldT[b], worldR[b], worldS[b], jointWorld);
            jointWorld.CopyTo(outPalette.Slice(b * 16, 16));
        }
    }
}
