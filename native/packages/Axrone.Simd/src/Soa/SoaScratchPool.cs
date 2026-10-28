namespace Axrone.Simd;

/// <summary>
/// Per-thread scratch arena for temporary vector/quaternion/matrix results. Slots are
/// addressed by index and masked, so callers never allocate and never expose a lifetime
/// that outlives the thread.
/// </summary>
public sealed class SoaScratchPool
{
    /// <summary>Number of Vec3 scratch slots.</summary>
    public const int Vec3Count = 8;

    /// <summary>Number of quaternion scratch slots.</summary>
    public const int QuatCount = 4;

    /// <summary>Number of matrix scratch slots.</summary>
    public const int Mat4Count = 2;

    private const int Vec3Offset = 0;
    private const int QuatOffset = Vec3Count * 3;
    private const int Mat4Offset = QuatOffset + (QuatCount * 4);
    private const int TotalFloats = Mat4Offset + (Mat4Count * 16);

    [ThreadStatic]
    private static SoaScratchPool? s_shared;

    private readonly float[] _data;

    /// <summary>Creates a scratch pool.</summary>
    public SoaScratchPool()
    {
        _data = GC.AllocateUninitializedArray<float>(TotalFloats, pinned: true);
    }

    /// <summary>Gets the pool for the current thread.</summary>
    public static SoaScratchPool Shared => s_shared ??= new SoaScratchPool();

    /// <summary>Gets a three-float scratch slot.</summary>
    /// <param name="slot">Slot index; masked into the pool.</param>
    /// <returns>A writable vector span.</returns>
    public Span<float> Vec3(int slot) => _data.AsSpan(Vec3Offset + ((slot & (Vec3Count - 1)) * 3), 3);

    /// <summary>Gets a four-float quaternion scratch slot.</summary>
    /// <param name="slot">Slot index; masked into the pool.</param>
    /// <returns>A writable quaternion span.</returns>
    public Span<float> Quat(int slot) => _data.AsSpan(QuatOffset + ((slot & (QuatCount - 1)) * 4), 4);

    /// <summary>Gets a sixteen-float matrix scratch slot.</summary>
    /// <param name="slot">Slot index; masked into the pool.</param>
    /// <returns>A writable matrix span.</returns>
    public Span<float> Mat4(int slot) => _data.AsSpan(Mat4Offset + ((slot & (Mat4Count - 1)) * 16), 16);

    /// <summary>Clears every scratch slot.</summary>
    public void Clear() => _data.AsSpan().Clear();
}
