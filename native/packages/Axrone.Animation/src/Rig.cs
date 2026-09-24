namespace Axrone.Animation;

/// <summary>Single bone authoring input: name, parent, rest transform, optional bind matrix.</summary>
public sealed class BoneInfo
{
    /// <summary>Bone name, ordinal-compared.</summary>
    public required string Name { get; init; }

    /// <summary>Parent bone index, -1 for roots.</summary>
    public required int ParentIndex { get; init; }

    /// <summary>Rest translation.</summary>
    public Vector3 RestTranslation { get; init; } = Vector3.Zero;

    /// <summary>Rest rotation.</summary>
    public Quaternion RestRotation { get; init; } = Quaternion.Identity;

    /// <summary>Rest scale.</summary>
    public Vector3 RestScale { get; init; } = Vector3.One;

    /// <summary>Optional custom inverse bind matrix.</summary>
    public Matrix4x4? InverseBindMatrix { get; init; }
}

/// <summary>
/// Validated skeleton: flat SoA rest pose, hierarchy topology, and parent-first
/// evaluation order. Immutable after construction; safe to share across threads.
/// Topology surfaces as spans, never arrays.
/// </summary>
public sealed class Rig
{
    private readonly string[] _boneNames;
    private readonly int[] _parents;
    private readonly int[] _evaluationOrder;
    private readonly int[] _rootIndices;
    private readonly int[][] _children;
    private readonly Dictionary<string, int> _nameToIndex;

    internal readonly float[] RestPoseBuffer;
    internal readonly float[]? InverseBindMatrices;

    /// <summary>Rig identity.</summary>
    public RigId Id { get; }

    /// <summary>Bone count.</summary>
    public int BoneCount { get; }

    /// <summary>Bone names by index.</summary>
    public ReadOnlySpan<string> BoneNames => _boneNames;

    /// <summary>Parent index per bone, -1 for roots.</summary>
    public ReadOnlySpan<int> Parents => _parents;

    /// <summary>Parent-first evaluation order.</summary>
    public ReadOnlySpan<int> EvaluationOrder => _evaluationOrder;

    /// <summary>Root bone indices.</summary>
    public ReadOnlySpan<int> RootIndices => _rootIndices;

    /// <summary>Packed rest pose: translations, rotations, scales.</summary>
    public ReadOnlySpan<float> RestPose => RestPoseBuffer;

    /// <summary>Packed inverse bind matrices, or empty when absent.</summary>
    public ReadOnlySpan<float> InverseBind => InverseBindMatrices;

    /// <summary>Children of a bone.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<int> GetChildren(int boneIndex) => _children[boneIndex];

    /// <summary>Validates and freezes a skeleton.</summary>
    public Rig(RigId id, ReadOnlySpan<BoneInfo> bones)
    {
        Id = id;
        BoneCount = bones.Length;
        if (BoneCount <= 0)
        {
            AnimationThrowHelper.ThrowValidation(AnimationErrorCode.ValidationRigEmptyBones, "Rig requires at least one bone.");
        }

        _boneNames = new string[BoneCount];
        _parents = new int[BoneCount];
        RestPoseBuffer = new float[BoneCount * 10];
        _nameToIndex = new Dictionary<string, int>(BoneCount, StringComparer.Ordinal);

        for (int i = 0; i < BoneCount; i++)
        {
            BoneInfo bone = bones[i];
            if (string.IsNullOrEmpty(bone.Name))
            {
                AnimationThrowHelper.ThrowValidation(AnimationErrorCode.ValidationRigDuplicateBoneName, $"Bone {i} has empty name.");
            }

            if (!_nameToIndex.TryAdd(bone.Name, i))
            {
                AnimationThrowHelper.ThrowValidation(AnimationErrorCode.ValidationRigDuplicateBoneName, $"Duplicate bone name '{bone.Name}'.");
            }

            _boneNames[i] = bone.Name;
            _parents[i] = bone.ParentIndex;

            if (bone.ParentIndex >= BoneCount || bone.ParentIndex == i || bone.ParentIndex < -1)
            {
                AnimationThrowHelper.ThrowValidation(AnimationErrorCode.ValidationRigInvalidParent, $"Bone {i} ('{bone.Name}') invalid parent {bone.ParentIndex}.");
            }

            WriteRestPose(i, bone);
        }

        _rootIndices = Array.Empty<int>();
        _children = Array.Empty<int[]>();
        _evaluationOrder = Array.Empty<int>();
        InverseBindMatrices = null;
    }

    private void WriteRestPose(int index, BoneInfo bone)
    {
        int tOffset = index * 3;
        RestPoseBuffer[tOffset] = bone.RestTranslation.X;
        RestPoseBuffer[tOffset + 1] = bone.RestTranslation.Y;
        RestPoseBuffer[tOffset + 2] = bone.RestTranslation.Z;

        int rOffset = (BoneCount * 3) + (index * 4);
        Quaternion rotation = bone.RestRotation;
        if (rotation.X == 0.0f && rotation.Y == 0.0f && rotation.Z == 0.0f && rotation.W == 0.0f)
        {
            rotation = Quaternion.Identity;
        }

        RestPoseBuffer[rOffset] = rotation.X;
        RestPoseBuffer[rOffset + 1] = rotation.Y;
        RestPoseBuffer[rOffset + 2] = rotation.Z;
        RestPoseBuffer[rOffset + 3] = rotation.W;

        int sOffset = (BoneCount * 7) + (index * 3);
        RestPoseBuffer[sOffset] = bone.RestScale.X == 0.0f ? 1.0f : bone.RestScale.X;
        RestPoseBuffer[sOffset + 1] = bone.RestScale.Y == 0.0f ? 1.0f : bone.RestScale.Y;
        RestPoseBuffer[sOffset + 2] = bone.RestScale.Z == 0.0f ? 1.0f : bone.RestScale.Z;
    }
}
