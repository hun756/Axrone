namespace Axrone.Animation;

/// <summary>
/// Stack-or-pooled world-transform scratch triple. Small rigs pay nothing (stack);
/// large rigs rent (pool) instead of risking a stack overflow. The budget lives
/// in <see cref="AnimationConstants.MaxStackScratchBones"/> — one place, not
/// folklore at every call site.
/// Usage:
/// <code>using ScratchWorldBuffers buffers = ScratchWorldBuffers.UseStack(n)
///     ? ScratchWorldBuffers.FromStack(stackalloc Vector3[n], stackalloc Quaternion[n], stackalloc Vector3[n])
///     : ScratchWorldBuffers.RentPooled(n);</code>
/// </summary>
public ref struct ScratchWorldBuffers
{
    /// <summary>World translations.</summary>
    public Span<Vector3> Translations { get; }

    /// <summary>World rotations.</summary>
    public Span<Quaternion> Rotations { get; }

    /// <summary>World scales.</summary>
    public Span<Vector3> Scales { get; }

    private Vector3[]? _rentedT;
    private Quaternion[]? _rentedR;
    private Vector3[]? _rentedS;

    private ScratchWorldBuffers(Span<Vector3> t, Span<Quaternion> r, Span<Vector3> s)
    {
        Translations = t;
        Rotations = r;
        Scales = s;
        _rentedT = null;
        _rentedR = null;
        _rentedS = null;
    }

    /// <summary>Whether a rig fits the stack budget.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool UseStack(int boneCount) => boneCount <= AnimationConstants.MaxStackScratchBones;

    /// <summary>Wraps caller-stackallocated spans (no pooling).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ScratchWorldBuffers FromStack(Span<Vector3> t, Span<Quaternion> r, Span<Vector3> s) =>
        new(t, r, s);

    /// <summary>Rents the triple from the shared pool (cold path).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ScratchWorldBuffers RentPooled(int boneCount)
    {
        Vector3[] t = ArrayPool<Vector3>.Shared.Rent(boneCount);
        Quaternion[] r = ArrayPool<Quaternion>.Shared.Rent(boneCount);
        Vector3[] s = ArrayPool<Vector3>.Shared.Rent(boneCount);
        var buffers = new ScratchWorldBuffers(
            t.AsSpan(0, boneCount),
            r.AsSpan(0, boneCount),
            s.AsSpan(0, boneCount));
        buffers._rentedT = t;
        buffers._rentedR = r;
        buffers._rentedS = s;
        return buffers;
    }

    /// <summary>Returns rentals; no-op for stack backing.</summary>
    public void Dispose()
    {
        Vector3[]? t = _rentedT;
        Quaternion[]? r = _rentedR;
        Vector3[]? s = _rentedS;
        _rentedT = null;
        _rentedR = null;
        _rentedS = null;
        if (t is not null)
        {
            ArrayPool<Vector3>.Shared.Return(t);
        }

        if (r is not null)
        {
            ArrayPool<Quaternion>.Shared.Return(r);
        }

        if (s is not null)
        {
            ArrayPool<Vector3>.Shared.Return(s);
        }
    }
}
