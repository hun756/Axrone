namespace Axrone.Simd;

/// <summary>Allocation-free handle to one bone inside a <see cref="SoaPoseBuffer"/>.</summary>
public readonly struct SoaBoneHandle
{
    private readonly SoaPoseBuffer _pose;

    internal SoaBoneHandle(SoaPoseBuffer pose, int index)
    {
        _pose = pose;
        Index = index;
    }

    /// <summary>Gets the bone index.</summary>
    public int Index { get; }

    /// <summary>Gets the pose buffer.</summary>
    public SoaPoseBuffer Pose => _pose;

    /// <summary>Writes the local translation.</summary>
    public void SetTranslation(float x, float y, float z) => _pose.Translations.Set(Index, x, y, z);

    /// <summary>Writes the local rotation.</summary>
    public void SetRotation(float x, float y, float z, float w) => _pose.Rotations.Set(Index, x, y, z, w);

    /// <summary>Writes the local scale.</summary>
    public void SetScale(float x, float y, float z) => _pose.Scales.Set(Index, x, y, z);

    /// <summary>Copies another bone's local transform.</summary>
    public void CopyFrom(in SoaBoneHandle source)
    {
        _pose.Translations.CopyFrom(Index, source._pose.Translations, source.Index);
        _pose.Rotations.CopyFrom(Index, source._pose.Rotations, source.Index);
        _pose.Scales.CopyFrom(Index, source._pose.Scales, source.Index);
    }
}

/// <summary>
/// Structure-of-arrays pose: translations, rotations and scales for a fixed bone count.
/// The three buffers share the bone index, so a bone handle addresses all of them at once.
/// </summary>
public sealed class SoaPoseBuffer : IDisposable
{
    /// <summary>Creates a rest pose.</summary>
    /// <param name="boneCount">Number of bones.</param>
    public SoaPoseBuffer(int boneCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(boneCount);

        BoneCount = boneCount;
        Translations = new SoaVec3Buffer(boneCount);
        Rotations = new SoaQuatBuffer(boneCount);
        Scales = new SoaVec3Buffer(boneCount);
        Translations.SetCount(boneCount);
        Rotations.SetCount(boneCount);
        Scales.SetCount(boneCount);
        ResetToRest();
    }

    /// <summary>Gets the bone count.</summary>
    public int BoneCount { get; }

    /// <summary>Gets the translation buffer.</summary>
    public SoaVec3Buffer Translations { get; }

    /// <summary>Gets the rotation buffer.</summary>
    public SoaQuatBuffer Rotations { get; }

    /// <summary>Gets the scale buffer.</summary>
    public SoaVec3Buffer Scales { get; }

    /// <summary>Gets a bone handle.</summary>
    /// <param name="index">Bone index.</param>
    /// <returns>A stack-only handle.</returns>
    public SoaBoneHandle Bone(int index)
    {
        if ((uint)index >= (uint)BoneCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return new SoaBoneHandle(this, index);
    }

    /// <summary>Resets every bone to the rest transform.</summary>
    public void ResetToRest()
    {
        Translations.Clear();
        for (var index = 0; index < BoneCount; index++)
        {
            Rotations.SetIdentity(index);
            Scales.Set(index, 1f, 1f, 1f);
        }
    }

    /// <summary>Interpolates three pose buffers.</summary>
    public void LerpAll(SoaPoseBuffer left, SoaPoseBuffer right, float t)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        Translations.LerpAll(left.Translations, right.Translations, t);
        Rotations.SlerpAll(left.Rotations, right.Rotations, t);
        Scales.LerpAll(left.Scales, right.Scales, t);
    }

    /// <summary>Copies another pose.</summary>
    public void CopyFrom(SoaPoseBuffer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Translations.CopyAllFrom(source.Translations);
        Rotations.CopyAllFrom(source.Rotations);
        Scales.CopyAllFrom(source.Scales);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Translations.Dispose();
        Rotations.Dispose();
        Scales.Dispose();
        GC.SuppressFinalize(this);
    }
}
