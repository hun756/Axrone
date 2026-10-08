namespace Axrone.Animation;

/// <summary>
/// Stack-or-pooled world-transform scratch triple. Small rigs pay nothing (stack);
/// large rigs rent (pool) instead of risking a stack overflow. The budget lives
/// in <see cref="AnimationConstants.MaxStackScratchBones"/> — one place, not
/// folklore at every call site.
/// Usage:
/// <code>using ScratchWorldBuffers buffers = ScratchWorldBuffers.UseStack(n)
///     ? ScratchWorldBuffers.FromStack(stackalloc Vec3[n], stackalloc Quat[n], stackalloc Vec3[n])
///     : ScratchWorldBuffers.RentPooled(n);</code>
/// </summary>
public ref struct ScratchWorldBuffers
{
    /// <summary>World translations.</summary>
    public Span<Vec3> Translations { get; }

    /// <summary>World rotations.</summary>
    public Span<Quat> Rotations { get; }

    /// <summary>World scales.</summary>
    public Span<Vec3> Scales { get; }

    private Vec3[]? _rentedT;
    private Quat[]? _rentedR;
    private Vec3[]? _rentedS;

    private ScratchWorldBuffers(Span<Vec3> t, Span<Quat> r, Span<Vec3> s)
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
    public static ScratchWorldBuffers FromStack(Span<Vec3> t, Span<Quat> r, Span<Vec3> s) =>
        new(t, r, s);

    /// <summary>Rents the triple from the shared pool (cold path).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ScratchWorldBuffers RentPooled(int boneCount)
    {
        Vec3[] t = ArrayPool<Vec3>.Shared.Rent(boneCount);
        Quat[] r = ArrayPool<Quat>.Shared.Rent(boneCount);
        Vec3[] s = ArrayPool<Vec3>.Shared.Rent(boneCount);
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
        Vec3[]? t = _rentedT;
        Quat[]? r = _rentedR;
        Vec3[]? s = _rentedS;
        _rentedT = null;
        _rentedR = null;
        _rentedS = null;
        if (t is not null)
        {
            ArrayPool<Vec3>.Shared.Return(t);
        }

        if (r is not null)
        {
            ArrayPool<Quat>.Shared.Return(r);
        }

        if (s is not null)
        {
            ArrayPool<Vec3>.Shared.Return(s);
        }
    }
}
