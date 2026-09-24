namespace Axrone.Animation;

/// <summary>Translation retarget policy.</summary>
public enum RetargetTranslationMode
{
    /// <summary>Leave target translation untouched.</summary>
    None = 0,

    /// <summary>Copy source translation verbatim.</summary>
    Absolute = 1,

    /// <summary>Copy scaled by the rest-length ratio.</summary>
    Scaled = 2,
}

/// <summary>Rotation retarget policy.</summary>
public enum RetargetRotationMode
{
    /// <summary>Copy source rotation verbatim.</summary>
    Copy = 0,

    /// <summary>Apply through the rest-pose offset.</summary>
    Offset = 1,
}

/// <summary>
/// Precomputed cross-rig mapping: name (or explicit) bone pairs with rotation
/// offsets and rest-length ratios resolved once at construction.
/// </summary>
public sealed class RetargetProfile
{
    private readonly int[] _sourceToTargetMap;
    private readonly Quaternion[] _rotationOffsets;
    private readonly float[] _lengthRatios;

    /// <summary>Source rig.</summary>
    public Rig SourceRig { get; }

    /// <summary>Target rig.</summary>
    public Rig TargetRig { get; }

    /// <summary>Source-to-target bone map, -1 when unmapped.</summary>
    public ReadOnlySpan<int> SourceToTargetMap => _sourceToTargetMap;

    /// <summary>Translation policy.</summary>
    public RetargetTranslationMode TranslationMode { get; init; } = RetargetTranslationMode.Scaled;

    /// <summary>Rotation policy.</summary>
    public RetargetRotationMode RotationMode { get; init; } = RetargetRotationMode.Offset;

    /// <summary>Builds a profile with explicit or name-matched mappings.</summary>
    public RetargetProfile(Rig sourceRig, Rig targetRig, (string Source, string Target)[]? explicitMappings = null)
    {
        ArgumentNullException.ThrowIfNull(sourceRig);
        ArgumentNullException.ThrowIfNull(targetRig);
        SourceRig = sourceRig;
        TargetRig = targetRig;

        _sourceToTargetMap = new int[sourceRig.BoneCount];
        Array.Fill(_sourceToTargetMap, -1);
        _rotationOffsets = new Quaternion[sourceRig.BoneCount];
        _lengthRatios = new float[sourceRig.BoneCount];

        if (explicitMappings != null && explicitMappings.Length > 0)
        {
            foreach ((string source, string target) in explicitMappings)
            {
                int sourceIndex = sourceRig.FindBoneIndex(source);
                int targetIndex = targetRig.FindBoneIndex(target);
                if (sourceIndex != -1 && targetIndex != -1)
                {
                    _sourceToTargetMap[sourceIndex] = targetIndex;
                }
            }
        }
        else
        {
            for (int i = 0; i < sourceRig.BoneCount; i++)
            {
                _sourceToTargetMap[i] = targetRig.FindBoneIndex(sourceRig.BoneNames[i].ToString());
            }
        }

        int mappedCount = 0;
        ReadOnlySpan<float> sourceRest = sourceRig.RestPoseBuffer;
        ReadOnlySpan<float> targetRest = targetRig.RestPoseBuffer;

        for (int s = 0; s < sourceRig.BoneCount; s++)
        {
            int t = _sourceToTargetMap[s];
            if (t == -1)
            {
                continue;
            }

            mappedCount++;

            int sRot = (sourceRig.BoneCount * 3) + (s * 4);
            int tRot = (targetRig.BoneCount * 3) + (t * 4);
            Quaternion sourceRestR = new(sourceRest[sRot], sourceRest[sRot + 1], sourceRest[sRot + 2], sourceRest[sRot + 3]);
            Quaternion targetRestR = new(targetRest[tRot], targetRest[tRot + 1], targetRest[tRot + 2], targetRest[tRot + 3]);
            _rotationOffsets[s] = Quaternion.Normalize(targetRestR * Quaternion.Inverse(sourceRestR));

            float sourceLen = new Vector3(sourceRest[s * 3], sourceRest[(s * 3) + 1], sourceRest[(s * 3) + 2]).Length();
            float targetLen = new Vector3(targetRest[t * 3], targetRest[(t * 3) + 1], targetRest[(t * 3) + 2]).Length();
            _lengthRatios[s] = sourceLen > AnimationConstants.SoaEpsilon ? targetLen / sourceLen : 1.0f;
        }

        if (mappedCount == 0)
        {
            AnimationThrowHelper.ThrowRetargeting(AnimationErrorCode.RetargetingNoMapping, "Zero bone mappings resolved.");
        }
    }
}
