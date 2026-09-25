namespace Axrone.Animation;

/// <summary>
/// Per-bone layer mask over a bitset. A default (null-backed) mask includes everything,
/// so callers that do not mask pay nothing and cannot trip on uninitialized state.
/// </summary>
public readonly record struct AnimationMask
{
    private readonly uint[]? _bits;

    /// <summary>Bone count covered.</summary>
    public int BoneCount { get; }

    /// <summary>Creates a mask; optionally enables all bones.</summary>
    public AnimationMask(int boneCount, bool enableAll = true)
    {
        BoneCount = boneCount;
        int wordLength = (boneCount + 31) / 32;
        _bits = new uint[wordLength];
        if (enableAll)
        {
            Array.Fill(_bits, 0xFFFFFFFFu);
            int remainder = boneCount % 32;
            if (remainder > 0)
            {
                _bits[wordLength - 1] = (1u << remainder) - 1u;
            }
        }
    }

    /// <summary>Tests a bone; out-of-range reads as disabled, missing backing as enabled.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEnabled(int boneIndex)
    {
        if (_bits is null)
        {
            return true;
        }

        if ((uint)boneIndex >= (uint)BoneCount)
        {
            return false;
        }

        return (_bits[boneIndex >> 5] & (1u << (boneIndex & 31))) != 0;
    }

    /// <summary>Flips a bone; out-of-range writes are ignored.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int boneIndex, bool enabled)
    {
        if (_bits is null)
        {
            return;
        }

        if ((uint)boneIndex >= (uint)BoneCount)
        {
            return;
        }

        int word = boneIndex >> 5;
        uint mask = 1u << (boneIndex & 31);
        if (enabled)
        {
            _bits[word] |= mask;
        }
        else
        {
            _bits[word] &= ~mask;
        }
    }
}

/// <summary>Named float channels (morph weights, custom curves) over a dense buffer.</summary>
public readonly record struct CurveStore
{
    private readonly float[] _values;
    private readonly Dictionary<CurveId, int> _offsets;

    /// <summary>Channel count.</summary>
    public int Count => _values.Length;

    /// <summary>Creates a store over a curve layout.</summary>
    public CurveStore(Dictionary<CurveId, int> layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _offsets = layout;
        _values = new float[layout.Count];
    }

    /// <summary>Raw channel span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> AsSpan() => _values.AsSpan();

    /// <summary>Resolves an id to a runtime slot once (bind time).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CurveHandle ResolveHandle(CurveId id)
    {
        if (!_offsets.TryGetValue(id, out int offset))
        {
            AnimationThrowHelper.ThrowCurveNotFound(id);
        }

        return new CurveHandle(offset);
    }

    /// <summary>Tries to resolve an id to a runtime slot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryResolveHandle(CurveId id, out CurveHandle handle)
    {
        if (_offsets.TryGetValue(id, out int offset))
        {
            handle = new CurveHandle(offset);
            return true;
        }

        handle = CurveHandle.Invalid;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RequireSlot(in CurveHandle handle)
    {
        if ((uint)handle.Slot >= (uint)_values.Length)
        {
            AnimationThrowHelper.ThrowValidation(AnimationErrorCode.ValidationInvalidArgument, $"Curve slot {handle.Slot} out of range.");
        }

        return handle.Slot;
    }

    /// <summary>Reads a channel; unknown ids fail loudly.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Read(CurveId id)
    {
        if (!_offsets.TryGetValue(id, out int offset))
        {
            AnimationThrowHelper.ThrowCurveNotFound(id);
        }

        return _values[offset];
    }

    /// <summary>Reads a channel by handle (no lookup).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Read(in CurveHandle handle) => _values[RequireSlot(in handle)];

    /// <summary>Writes a channel; unknown ids fail loudly.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(CurveId id, float value)
    {
        if (!_offsets.TryGetValue(id, out int offset))
        {
            AnimationThrowHelper.ThrowCurveNotFound(id);
        }

        _values[offset] = value;
    }

    /// <summary>Writes a channel by handle (no lookup).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(in CurveHandle handle, float value) => _values[RequireSlot(in handle)] = value;

    /// <summary>Clears all channels.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => Array.Clear(_values, 0, _values.Length);

    /// <summary>Copies channels, truncated to the shorter store.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyFrom(in CurveStore other)
    {
        ReadOnlySpan<float> src = other._values;
        Span<float> dst = _values;
        int len = Math.Min(src.Length, dst.Length);
        src.Slice(0, len).CopyTo(dst);
    }
}

/// <summary>
/// Local-space pose frame: SoA translation/rotation/scale buffer plus curves.
/// Managed backing pooled by arenas — zero steady-state allocation after warmup.
/// </summary>
public sealed class AnimationFrame
{
    private readonly float[] _poseBuffer;

    /// <summary>Bone count.</summary>
    public int BoneCount { get; }

    /// <summary>Curve channels.</summary>
    public CurveStore Curves { get; }

    /// <summary>Creates a frame.</summary>
    public AnimationFrame(int boneCount, Dictionary<CurveId, int> curveLayout)
    {
        ArgumentNullException.ThrowIfNull(curveLayout);
        if (boneCount <= 0)
        {
            AnimationThrowHelper.ThrowValidation(AnimationErrorCode.ValidationRigEmptyBones, "Frame requires at least one bone.");
        }

        BoneCount = boneCount;
        _poseBuffer = new float[boneCount * 10];
        Curves = new CurveStore(curveLayout);
    }

    /// <summary>Translations as Vector3 lanes over the buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Vector3> GetTranslations() =>
        MemoryMarshal.Cast<float, Vector3>(_poseBuffer.AsSpan(0, BoneCount * 3));

    /// <summary>Rotations as Quaternion lanes over the buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Quaternion> GetRotations() =>
        MemoryMarshal.Cast<float, Quaternion>(_poseBuffer.AsSpan(BoneCount * 3, BoneCount * 4));

    /// <summary>Scales as Vector3 lanes over the buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Vector3> GetScales() =>
        MemoryMarshal.Cast<float, Vector3>(_poseBuffer.AsSpan(BoneCount * 7, BoneCount * 3));

    /// <summary>Translations, read-only.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<Vector3> ReadTranslations() =>
        MemoryMarshal.Cast<float, Vector3>(_poseBuffer.AsSpan(0, BoneCount * 3));

    /// <summary>Rotations, read-only.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<Quaternion> ReadRotations() =>
        MemoryMarshal.Cast<float, Quaternion>(_poseBuffer.AsSpan(BoneCount * 3, BoneCount * 4));

    /// <summary>Scales, read-only.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<Vector3> ReadScales() =>
        MemoryMarshal.Cast<float, Vector3>(_poseBuffer.AsSpan(BoneCount * 7, BoneCount * 3));

    /// <summary>Deep-copies pose and curves.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyFrom(AnimationFrame source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source._poseBuffer.AsSpan().CopyTo(_poseBuffer.AsSpan());
        Curves.CopyFrom(source.Curves);
    }

    /// <summary>Restores the rig rest pose and clears curves.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetToRest(Rig rig)
    {
        ArgumentNullException.ThrowIfNull(rig);
        rig.RestPoseBuffer.AsSpan().CopyTo(_poseBuffer.AsSpan());
        Curves.Clear();
    }
}

/// <summary>
/// Stack-disciplined scratch frame pool: preallocated frames handed out LIFO and
/// reclaimed in reverse, so nested blend evaluation never touches the GC.
/// </summary>
public sealed class FrameArena
{
    private readonly AnimationFrame[] _pool;
    private int _stackPointer;

    /// <summary>Preallocates scratch frames.</summary>
    public FrameArena(int boneCount, Dictionary<CurveId, int> curveLayout, int capacity = 32)
    {
        ArgumentNullException.ThrowIfNull(curveLayout);
        if (capacity <= 0)
        {
            AnimationThrowHelper.ThrowValidation(AnimationErrorCode.EvaluationDepthOverflow, "Arena capacity must be positive.");
        }

        _pool = new AnimationFrame[capacity];
        for (int i = 0; i < capacity; i++)
        {
            _pool[i] = new AnimationFrame(boneCount, curveLayout);
        }

        _stackPointer = 0;
    }

    /// <summary>Rents the next scratch frame.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AnimationFrame Alloc()
    {
        if (_stackPointer >= _pool.Length)
        {
            AnimationThrowHelper.ThrowEvaluation(AnimationErrorCode.EvaluationDepthOverflow, "FrameArena scratch frame exhausted.");
        }

        return _pool[_stackPointer++];
    }

    /// <summary>Returns the most recent frame.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Free()
    {
        if (_stackPointer > 0)
        {
            _stackPointer--;
        }
    }

    /// <summary>Releases all frames at once (frame boundary).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => _stackPointer = 0;
}
