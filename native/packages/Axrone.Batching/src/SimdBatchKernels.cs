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
    public static void TransformPositions3D(ReadOnlySpan<Vector3> source, Span<Vector3> destination, in Matrix4x4 matrix)
    {
        ThrowHelper.ValidateDestinationSpan(source, destination);

        var count = (nuint)source.Length;
        if (count == 0)
        {
            return;
        }

        ref float sourceBase = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(source));
        ref float targetBase = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(destination));
        nuint index = 0;

        if (Vector256.IsHardwareAccelerated && count >= 8)
        {
            var m11 = Vector256.Create(matrix.M11);
            var m12 = Vector256.Create(matrix.M12);
            var m13 = Vector256.Create(matrix.M13);
            var m21 = Vector256.Create(matrix.M21);
            var m22 = Vector256.Create(matrix.M22);
            var m23 = Vector256.Create(matrix.M23);
            var m31 = Vector256.Create(matrix.M31);
            var m32 = Vector256.Create(matrix.M32);
            var m33 = Vector256.Create(matrix.M33);
            var m41 = Vector256.Create(matrix.M41);
            var m42 = Vector256.Create(matrix.M42);
            var m43 = Vector256.Create(matrix.M43);

            nuint step = 8, limit = count - step + 1;
            for (; index < limit; index += step)
            {
                var offset = index * 3;

                var vx = Vector256.Create(
                    Unsafe.Add(ref sourceBase, offset), Unsafe.Add(ref sourceBase, offset + 3),
                    Unsafe.Add(ref sourceBase, offset + 6), Unsafe.Add(ref sourceBase, offset + 9),
                    Unsafe.Add(ref sourceBase, offset + 12), Unsafe.Add(ref sourceBase, offset + 15),
                    Unsafe.Add(ref sourceBase, offset + 18), Unsafe.Add(ref sourceBase, offset + 21));
                var vy = Vector256.Create(
                    Unsafe.Add(ref sourceBase, offset + 1), Unsafe.Add(ref sourceBase, offset + 4),
                    Unsafe.Add(ref sourceBase, offset + 7), Unsafe.Add(ref sourceBase, offset + 10),
                    Unsafe.Add(ref sourceBase, offset + 13), Unsafe.Add(ref sourceBase, offset + 16),
                    Unsafe.Add(ref sourceBase, offset + 19), Unsafe.Add(ref sourceBase, offset + 22));
                var vz = Vector256.Create(
                    Unsafe.Add(ref sourceBase, offset + 2), Unsafe.Add(ref sourceBase, offset + 5),
                    Unsafe.Add(ref sourceBase, offset + 8), Unsafe.Add(ref sourceBase, offset + 11),
                    Unsafe.Add(ref sourceBase, offset + 14), Unsafe.Add(ref sourceBase, offset + 17),
                    Unsafe.Add(ref sourceBase, offset + 20), Unsafe.Add(ref sourceBase, offset + 23));

                var rx = Vector256.Add(
                    Vector256.Add(Vector256.Multiply(vx, m11), Vector256.Multiply(vy, m21)),
                    Vector256.Add(Vector256.Multiply(vz, m31), m41));
                var ry = Vector256.Add(
                    Vector256.Add(Vector256.Multiply(vx, m12), Vector256.Multiply(vy, m22)),
                    Vector256.Add(Vector256.Multiply(vz, m32), m42));
                var rz = Vector256.Add(
                    Vector256.Add(Vector256.Multiply(vx, m13), Vector256.Multiply(vy, m23)),
                    Vector256.Add(Vector256.Multiply(vz, m33), m43));

                for (var lane = nuint.Zero; lane < step; lane++)
                {
                    var target = offset + (lane * 3);
                    Unsafe.Add(ref targetBase, target) = rx.GetElement((int)lane);
                    Unsafe.Add(ref targetBase, target + 1) = ry.GetElement((int)lane);
                    Unsafe.Add(ref targetBase, target + 2) = rz.GetElement((int)lane);
                }
            }
        }

        for (; index < count; index++)
        {
            destination[(int)index] = Vector3.Transform(source[(int)index], matrix);
        }
    }

    /// <summary>
    /// Transforms homogeneous vectors by <paramref name="matrix"/>, including the perspective row.
    /// </summary>
    /// <param name="source">Vectors to transform.</param>
    /// <param name="destination">Receives the results; must be at least <paramref name="source"/>.Length.</param>
    /// <param name="matrix">Row-major transform.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    public static void TransformAffine(ReadOnlySpan<Vector4> source, Span<Vector4> destination, in Matrix4x4 matrix)
    {
        ThrowHelper.ValidateDestinationSpan(source, destination);

        var count = (nuint)source.Length;
        if (count == 0)
        {
            return;
        }

        ref float sourceBase = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector4, float>(source));
        ref float targetBase = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector4, float>(destination));
        nuint index = 0;

        if (Vector256.IsHardwareAccelerated && count >= 2)
        {
            var r0 = Vector256.Create(matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                                      matrix.M11, matrix.M12, matrix.M13, matrix.M14);
            var r1 = Vector256.Create(matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                                      matrix.M21, matrix.M22, matrix.M23, matrix.M24);
            var r2 = Vector256.Create(matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                                      matrix.M31, matrix.M32, matrix.M33, matrix.M34);
            var r3 = Vector256.Create(matrix.M41, matrix.M42, matrix.M43, matrix.M44,
                                      matrix.M41, matrix.M42, matrix.M43, matrix.M44);

            nuint step = 2, limit = count - step + 1;
            for (; index < limit; index += step)
            {
                var offset = index * 4;

                var vx = Vector256.Create(
                    Unsafe.Add(ref sourceBase, offset), Unsafe.Add(ref sourceBase, offset),
                    Unsafe.Add(ref sourceBase, offset), Unsafe.Add(ref sourceBase, offset),
                    Unsafe.Add(ref sourceBase, offset + 4), Unsafe.Add(ref sourceBase, offset + 4),
                    Unsafe.Add(ref sourceBase, offset + 4), Unsafe.Add(ref sourceBase, offset + 4));
                var vy = Vector256.Create(
                    Unsafe.Add(ref sourceBase, offset + 1), Unsafe.Add(ref sourceBase, offset + 1),
                    Unsafe.Add(ref sourceBase, offset + 1), Unsafe.Add(ref sourceBase, offset + 1),
                    Unsafe.Add(ref sourceBase, offset + 5), Unsafe.Add(ref sourceBase, offset + 5),
                    Unsafe.Add(ref sourceBase, offset + 5), Unsafe.Add(ref sourceBase, offset + 5));
                var vz = Vector256.Create(
                    Unsafe.Add(ref sourceBase, offset + 2), Unsafe.Add(ref sourceBase, offset + 2),
                    Unsafe.Add(ref sourceBase, offset + 2), Unsafe.Add(ref sourceBase, offset + 2),
                    Unsafe.Add(ref sourceBase, offset + 6), Unsafe.Add(ref sourceBase, offset + 6),
                    Unsafe.Add(ref sourceBase, offset + 6), Unsafe.Add(ref sourceBase, offset + 6));
                var vw = Vector256.Create(
                    Unsafe.Add(ref sourceBase, offset + 3), Unsafe.Add(ref sourceBase, offset + 3),
                    Unsafe.Add(ref sourceBase, offset + 3), Unsafe.Add(ref sourceBase, offset + 3),
                    Unsafe.Add(ref sourceBase, offset + 7), Unsafe.Add(ref sourceBase, offset + 7),
                    Unsafe.Add(ref sourceBase, offset + 7), Unsafe.Add(ref sourceBase, offset + 7));

                Vector256.Add(
                    Vector256.Add(Vector256.Multiply(vx, r0), Vector256.Multiply(vy, r1)),
                    Vector256.Add(Vector256.Multiply(vz, r2), Vector256.Multiply(vw, r3)))
                    .StoreUnsafe(ref targetBase, offset);
            }
        }

        for (; index < count; index++)
        {
            var v = source[(int)index];
            destination[(int)index] = new Vector4(
                (v.X * matrix.M11) + (v.Y * matrix.M21) + (v.Z * matrix.M31) + (v.W * matrix.M41),
                (v.X * matrix.M12) + (v.Y * matrix.M22) + (v.Z * matrix.M32) + (v.W * matrix.M42),
                (v.X * matrix.M13) + (v.Y * matrix.M23) + (v.Z * matrix.M33) + (v.W * matrix.M43),
                (v.X * matrix.M14) + (v.Y * matrix.M24) + (v.Z * matrix.M34) + (v.W * matrix.M44));
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
    /// as a flat vector pass over <c>count * 3</c> floats.
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

        if (Vector256.IsHardwareAccelerated && scalarCount >= (nuint)Vector256<float>.Count)
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
        else if (Vector128.IsHardwareAccelerated && scalarCount >= (nuint)Vector128<float>.Count)
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
