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
    private readonly float[] _restWorldMatrices;

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

    /// <summary>Precomputed rest-pose world matrices (16 floats per bone).</summary>
    public ReadOnlySpan<float> RestWorldMatrices => _restWorldMatrices;

    /// <summary>Children of a bone.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<int> GetChildren(int boneIndex)
    {
        if ((uint)boneIndex >= (uint)BoneCount)
        {
            AnimationThrowHelper.ThrowValidation(AnimationErrorCode.ValidationInvalidArgument, $"Bone index {boneIndex} out of range.");
        }

        return _children[boneIndex];
    }

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

        _children = new int[BoneCount][];
        var roots = new List<int>();
        var childLists = new List<int>[BoneCount];
        for (int i = 0; i < BoneCount; i++)
        {
            childLists[i] = new List<int>();
            if (_parents[i] == -1)
            {
                roots.Add(i);
            }
        }

        for (int i = 0; i < BoneCount; i++)
        {
            int parent = _parents[i];
            if (parent != -1)
            {
                childLists[parent].Add(i);
            }
        }

        for (int i = 0; i < BoneCount; i++)
        {
            _children[i] = childLists[i].ToArray();
        }

        _rootIndices = roots.ToArray();
        _evaluationOrder = BuildEvaluationOrder(_parents, _children, _rootIndices, _boneNames);
        InverseBindMatrices = BuildInverseBindMatrices(bones);
        _restWorldMatrices = BuildRestWorldMatrices();
    }

    /// <summary>Bone index by ordinal name, -1 when absent.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindBoneIndex(string name) =>
        _nameToIndex.TryGetValue(name, out int index) ? index : -1;

    /// <summary>
    /// Copies the precomputed rest-pose world matrix palette (16 floats per bone).
    /// Composition runs once at construction; per-call cost is a single memcpy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CreateRestMatrixPalette(Span<float> outPalette)
    {
        if (outPalette.Length < BoneCount * 16)
        {
            AnimationThrowHelper.ThrowValidation(AnimationErrorCode.SamplingOutOfBounds, "Output matrix palette too small.");
        }

        _restWorldMatrices.AsSpan(0, BoneCount * 16).CopyTo(outPalette);
    }

    /// <summary>
    /// Composes rest-pose world matrices once (construction): same formulas as the
    /// per-frame forward kinematics, so results match live evaluation bit-for-bit.
    /// </summary>
    [SkipLocalsInit]
    private float[] BuildRestWorldMatrices()
    {
        var palette = new float[BoneCount * 16];
        int scratchBones = BoneCount;
        using ScratchWorldBuffers buffers = ScratchWorldBuffers.UseStack(scratchBones)
            ? ScratchWorldBuffers.FromStack(stackalloc Vector3[scratchBones], stackalloc Quaternion[scratchBones], stackalloc Vector3[scratchBones])
            : ScratchWorldBuffers.RentPooled(scratchBones);
        Span<Vector3> worldT = buffers.Translations;
        Span<Quaternion> worldR = buffers.Rotations;
        Span<Vector3> worldS = buffers.Scales;

        ReadOnlySpan<float> buf = RestPoseBuffer;
        int rBase = BoneCount * 3;
        int sBase = BoneCount * 7;

        for (int i = 0; i < _evaluationOrder.Length; i++)
        {
            int b = _evaluationOrder[i];
            int p = _parents[b];
            int tOff = b * 3;
            int rOff = rBase + (b * 4);
            int sOff = sBase + (b * 3);

            Vector3 locT = new(buf[tOff], buf[tOff + 1], buf[tOff + 2]);
            Quaternion locR = new(buf[rOff], buf[rOff + 1], buf[rOff + 2], buf[rOff + 3]);
            Vector3 locS = new(buf[sOff], buf[sOff + 1], buf[sOff + 2]);

            if (p == -1)
            {
                worldT[b] = locT;
                worldR[b] = locR;
                worldS[b] = locS;
            }
            else
            {
                worldS[b] = worldS[p] * locS;
                worldR[b] = Quaternion.Normalize(worldR[p] * locR);
                worldT[b] = worldT[p] + Vector3.Transform(locT * worldS[p], worldR[p]);
            }

            FastMath.ComposeTransformMatrix(worldT[b], worldR[b], worldS[b], palette.AsSpan(b * 16, 16));
        }

        return palette;
    }

    private static float[]? BuildInverseBindMatrices(ReadOnlySpan<BoneInfo> bones)
    {
        bool custom = false;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i].InverseBindMatrix.HasValue)
            {
                custom = true;
                break;
            }
        }

        if (!custom)
        {
            return null;
        }

        var matrices = new float[bones.Length * 16];
        for (int i = 0; i < bones.Length; i++)
        {
            Matrix4x4 m = bones[i].InverseBindMatrix ?? Matrix4x4.Identity;
            int off = i * 16;
            matrices[off] = m.M11; matrices[off + 1] = m.M21; matrices[off + 2] = m.M31; matrices[off + 3] = m.M41;
            matrices[off + 4] = m.M12; matrices[off + 5] = m.M22; matrices[off + 6] = m.M32; matrices[off + 7] = m.M42;
            matrices[off + 8] = m.M13; matrices[off + 9] = m.M23; matrices[off + 10] = m.M33; matrices[off + 11] = m.M43;
            matrices[off + 12] = m.M14; matrices[off + 13] = m.M24; matrices[off + 14] = m.M34; matrices[off + 15] = m.M44;
        }

        return matrices;
    }

    /// <summary>
    /// Parent-first order via iterative DFS with explicit stacks: no recursion depth
    /// risk, no closure allocations. Detects cycles and orphaned subtrees.
    /// </summary>
    private static int[] BuildEvaluationOrder(int[] parents, int[][] children, int[] roots, string[] boneNames)
    {
        int boneCount = parents.Length;
        var order = new int[boneCount];
        int written = 0;
        var state = new byte[boneCount];
        var stack = new int[boneCount];
        var childCursor = new int[boneCount];

        for (int r = 0; r < roots.Length; r++)
        {
            int root = roots[r];
            if (state[root] != 0)
            {
                continue;
            }

            int depth = 0;
            stack[depth] = root;
            state[root] = 1;

            while (depth >= 0)
            {
                int current = stack[depth];
                if (childCursor[current] < children[current].Length)
                {
                    int child = children[current][childCursor[current]++];
                    if (state[child] == 1)
                    {
                        AnimationThrowHelper.ThrowValidation(AnimationErrorCode.ValidationRigCycleDetected, $"Cycle detected at bone '{boneNames[child]}'.");
                    }

                    if (state[child] == 0)
                    {
                        state[child] = 1;
                        stack[++depth] = child;
                    }
                }
                else
                {
                    state[current] = 2;
                    order[written++] = current;
                    depth--;
                }
            }
        }

        for (int i = 0; i < boneCount; i++)
        {
            if (state[i] == 0)
            {
                AnimationThrowHelper.ThrowValidation(AnimationErrorCode.ValidationRigInvalidParent, $"Unconnected bone tree detected: '{boneNames[i]}'.");
            }
        }

        Array.Reverse(order);
        return order;
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
