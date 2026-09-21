namespace Axrone.Batching;

/// <summary>
/// Batch geometry kernels over <see cref="System.Numerics"/> types.
/// </summary>
/// <remarks>
/// <c>Axrone.Simd</c> covers flat numeric spans; this layer adds the engine-typed shapes on
/// top — positions, directions, bounds. Everything here is span-in/span-out and writes results into
/// a caller-owned destination, so a kernel can target a slot handed over from a pool without an
/// intermediate copy.
/// </remarks>
public static unsafe partial class SimdBatchKernels
{
    /// <summary>
    /// Transforms positions by <paramref name="matrix"/>, ignoring the perspective row.
    /// </summary>
    /// <param name="source">Positions to transform.</param>
    /// <param name="destination">Receives the results; must be at least <paramref name="source"/>.Length.</param>
    /// <param name="matrix">Row-major transform.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    /// <remarks>
    /// Deliberately scalar: the 8-wide AoS-gather version measured slower than this loop at every
    /// batch size (see <c>GeometryKernelBenchmarks</c>).
    /// <see cref="Vector3.Transform(Vector3, Matrix4x4)"/> itself is JIT-intrinsic accelerated,
    /// so the loop already issues vector instructions.
    /// </remarks>
    public static void TransformPositions3D(ReadOnlySpan<Vector3> source, Span<Vector3> destination, in Matrix4x4 matrix)
    {
        ThrowHelper.ValidateDestinationSpan(source, destination);

        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = Vector3.Transform(source[i], matrix);
        }
    }

    /// <summary>
    /// Transforms homogeneous vectors by <paramref name="matrix"/>, including the perspective row.
    /// </summary>
    /// <param name="source">Vectors to transform.</param>
    /// <param name="destination">Receives the results; must be at least <paramref name="source"/>.Length.</param>
    /// <param name="matrix">Row-major transform.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    /// <remarks>
    /// Deliberately scalar: the 2-wide vector version measured at best tied with this loop
    /// (see <c>GeometryKernelBenchmarks</c>), and the simpler code wins ties.
    /// </remarks>
    public static void TransformAffine(ReadOnlySpan<Vector4> source, Span<Vector4> destination, in Matrix4x4 matrix)
    {
        ThrowHelper.ValidateDestinationSpan(source, destination);

        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = Vector4.Transform(source[i], matrix);
        }
    }

    /// <summary>
    /// Advances positions by velocity: <c>position += velocity * deltaTime</c>.
    /// </summary>
    /// <param name="positions">Positions to advance in place.</param>
    /// <param name="velocities">Per-position velocity.</param>
    /// <param name="deltaTime">Elapsed time to integrate over.</param>
    /// <exception cref="ArgumentException"><paramref name="velocities"/> is shorter than <paramref name="positions"/>.</exception>
    /// <remarks>
    /// Both spans are contiguous in component order, so this needs no AoS-to-SoA transpose and runs
    /// as a flat vector pass over <c>count * 3</c> floats, cascading 256 → 128 → scalar.
    /// </remarks>
    public static void IntegrateVelocity(Span<Vector3> positions, ReadOnlySpan<Vector3> velocities, float deltaTime)
    {
        if (velocities.Length < positions.Length)
        {
            ThrowHelper.ThrowMismatchedSpans();
        }

        var scalarCount = (nuint)positions.Length * 3;
        if (scalarCount == 0)
        {
            return;
        }

        ref float positionBase = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(positions));
        ref float velocityBase = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(velocities));
        nuint index = 0;

        // No 512-bit tier by measurement: contiguous 512-bit passes clocked ~2x slower than 256-bit
        // on the AVX-512 test machine (frequency downclock archetype); see GeometryKernelBenchmarks.
        if (Vector256.IsHardwareAccelerated && scalarCount - index >= (nuint)Vector256<float>.Count)
        {
            var step = (nuint)Vector256<float>.Count;
            var dt = Vector256.Create(deltaTime);
            nuint limit = scalarCount - step + 1;
            for (; index < limit; index += step)
            {
                var position = Vector256.LoadUnsafe(ref positionBase, index);
                var velocity = Vector256.LoadUnsafe(ref velocityBase, index);
                Vector256.Add(position, Vector256.Multiply(velocity, dt)).StoreUnsafe(ref positionBase, index);
            }
        }

        if (Vector128.IsHardwareAccelerated && scalarCount - index >= (nuint)Vector128<float>.Count)
        {
            var step = (nuint)Vector128<float>.Count;
            var dt = Vector128.Create(deltaTime);
            nuint limit = scalarCount - step + 1;
            for (; index < limit; index += step)
            {
                var position = Vector128.LoadUnsafe(ref positionBase, index);
                var velocity = Vector128.LoadUnsafe(ref velocityBase, index);
                Vector128.Add(position, Vector128.Multiply(velocity, dt)).StoreUnsafe(ref positionBase, index);
            }
        }

        for (; index < scalarCount; index++)
        {
            Unsafe.Add(ref positionBase, index) += Unsafe.Add(ref velocityBase, index) * deltaTime;
        }
    }
}
