namespace Axrone.Simd;

/// <summary>Structure-of-arrays palette of column-major 4x4 skinning matrices.</summary>
public sealed class SoaMatrixPalette : SoaBuffer
{
    /// <summary>Creates an identity palette.</summary>
    /// <param name="boneCount">Number of matrices.</param>
    public SoaMatrixPalette(int boneCount)
        : base(boneCount, 16)
    {
        SetCount(boneCount);
        Span<float> data = FullSpan;
        for (var index = 0; index < boneCount; index++)
        {
            int offset = index * 16;
            data[offset] = 1f;
            data[offset + 5] = 1f;
            data[offset + 10] = 1f;
            data[offset + 15] = 1f;
        }
    }

    /// <summary>Gets one matrix as a writable slice.</summary>
    public Span<float> Slice(int index)
    {
        ValidateIndex(index);
        return FullSpan.Slice(index * 16, 16);
    }

    /// <summary>Composes every matrix from a pose's translation/rotation/scale buffers.</summary>
    public void ComposeFrom(SoaPoseBuffer pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        int count = Math.Min(Count, pose.BoneCount);
        Span<float> target = FullSpan;
        ReadOnlySpan<float> translations = pose.Translations.FullSpan;
        ReadOnlySpan<float> rotations = pose.Rotations.FullSpan;
        ReadOnlySpan<float> scales = pose.Scales.FullSpan;

        for (var index = 0; index < count; index++)
        {
            int vectorOffset = index * 3;
            int quaternionOffset = index * 4;
            SoaMat4Ops.Compose(
                target,
                index * 16,
                translations[vectorOffset],
                translations[vectorOffset + 1],
                translations[vectorOffset + 2],
                rotations[quaternionOffset],
                rotations[quaternionOffset + 1],
                rotations[quaternionOffset + 2],
                rotations[quaternionOffset + 3],
                scales[vectorOffset],
                scales[vectorOffset + 1],
                scales[vectorOffset + 2]);
        }
    }

    /// <summary>Multiplies every matrix by its parent matrix.</summary>
    public void MultiplyParent(SoaMatrixPalette parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        int count = Math.Min(Count, parent.Count);
        Span<float> target = FullSpan;
        ReadOnlySpan<float> parentData = parent.FullSpan;

        for (var index = 0; index < count; index++)
        {
            SoaMat4Ops.Multiply(target, index * 16, parentData, index * 16, target, index * 16);
        }
    }
}
