namespace Axrone.Batching;

/// <summary>
/// Batch vector kernels: quaternion product, dot, cross, normalization.
/// </summary>
/// <remarks>
/// Same shape as the geometry kernels — span-in/span-out over engine types, 8-wide gather where
/// the AoS layout forces it, scalar tail that is the benchmark baseline. Normalization is guarded:
/// a zero-length input writes <see cref="Vector4.Zero"/> instead of NaN.
/// </remarks>
public static unsafe partial class SimdBatchKernels
{
    /// <summary>Length-squared below which a vector counts as zero and normalizes to zero.</summary>
    private const float NormalizeEpsilon = 1e-12f;

    /// <summary>
    /// Multiplies quaternion pairs: <c>destination[i] = left[i] * right[i]</c>.
    /// </summary>
    /// <param name="left">First factors.</param>
    /// <param name="right">Second factors.</param>
    /// <param name="destination">Receives the products; must be at least <paramref name="left"/>.Length.</param>
    /// <exception cref="ArgumentException">A span is shorter than <paramref name="left"/>.</exception>
    public static void QuaternionMultiply(ReadOnlySpan<Quaternion> left, ReadOnlySpan<Quaternion> right, Span<Quaternion> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);

        var count = (nuint)left.Length;
        if (count == 0)
        {
            return;
        }

        ref float leftBase = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Quaternion, float>(left));
        ref float rightBase = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Quaternion, float>(right));
        ref float targetBase = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Quaternion, float>(destination));
        nuint index = 0;

        if (Vector256.IsHardwareAccelerated && count >= 8)
        {
            nuint step = 8, limit = count - step + 1;
            for (; index < limit; index += step)
            {
                var offset = index * 4;

                var ax = Vector256.Create(
                    Unsafe.Add(ref leftBase, offset), Unsafe.Add(ref leftBase, offset + 4),
                    Unsafe.Add(ref leftBase, offset + 8), Unsafe.Add(ref leftBase, offset + 12),
                    Unsafe.Add(ref leftBase, offset + 16), Unsafe.Add(ref leftBase, offset + 20),
                    Unsafe.Add(ref leftBase, offset + 24), Unsafe.Add(ref leftBase, offset + 28));
                var ay = Vector256.Create(
                    Unsafe.Add(ref leftBase, offset + 1), Unsafe.Add(ref leftBase, offset + 5),
                    Unsafe.Add(ref leftBase, offset + 9), Unsafe.Add(ref leftBase, offset + 13),
                    Unsafe.Add(ref leftBase, offset + 17), Unsafe.Add(ref leftBase, offset + 21),
                    Unsafe.Add(ref leftBase, offset + 25), Unsafe.Add(ref leftBase, offset + 29));
                var az = Vector256.Create(
                    Unsafe.Add(ref leftBase, offset + 2), Unsafe.Add(ref leftBase, offset + 6),
                    Unsafe.Add(ref leftBase, offset + 10), Unsafe.Add(ref leftBase, offset + 14),
                    Unsafe.Add(ref leftBase, offset + 18), Unsafe.Add(ref leftBase, offset + 22),
                    Unsafe.Add(ref leftBase, offset + 26), Unsafe.Add(ref leftBase, offset + 30));
                var aw = Vector256.Create(
                    Unsafe.Add(ref leftBase, offset + 3), Unsafe.Add(ref leftBase, offset + 7),
                    Unsafe.Add(ref leftBase, offset + 11), Unsafe.Add(ref leftBase, offset + 15),
                    Unsafe.Add(ref leftBase, offset + 19), Unsafe.Add(ref leftBase, offset + 23),
                    Unsafe.Add(ref leftBase, offset + 27), Unsafe.Add(ref leftBase, offset + 31));

                var bx = Vector256.Create(
                    Unsafe.Add(ref rightBase, offset), Unsafe.Add(ref rightBase, offset + 4),
                    Unsafe.Add(ref rightBase, offset + 8), Unsafe.Add(ref rightBase, offset + 12),
                    Unsafe.Add(ref rightBase, offset + 16), Unsafe.Add(ref rightBase, offset + 20),
                    Unsafe.Add(ref rightBase, offset + 24), Unsafe.Add(ref rightBase, offset + 28));
                var by = Vector256.Create(
                    Unsafe.Add(ref rightBase, offset + 1), Unsafe.Add(ref rightBase, offset + 5),
                    Unsafe.Add(ref rightBase, offset + 9), Unsafe.Add(ref rightBase, offset + 13),
                    Unsafe.Add(ref rightBase, offset + 17), Unsafe.Add(ref rightBase, offset + 21),
                    Unsafe.Add(ref rightBase, offset + 25), Unsafe.Add(ref rightBase, offset + 29));
                var bz = Vector256.Create(
                    Unsafe.Add(ref rightBase, offset + 2), Unsafe.Add(ref rightBase, offset + 6),
                    Unsafe.Add(ref rightBase, offset + 10), Unsafe.Add(ref rightBase, offset + 14),
                    Unsafe.Add(ref rightBase, offset + 18), Unsafe.Add(ref rightBase, offset + 22),
                    Unsafe.Add(ref rightBase, offset + 26), Unsafe.Add(ref rightBase, offset + 30));
                var bw = Vector256.Create(
                    Unsafe.Add(ref rightBase, offset + 3), Unsafe.Add(ref rightBase, offset + 7),
                    Unsafe.Add(ref rightBase, offset + 11), Unsafe.Add(ref rightBase, offset + 15),
                    Unsafe.Add(ref rightBase, offset + 19), Unsafe.Add(ref rightBase, offset + 23),
                    Unsafe.Add(ref rightBase, offset + 27), Unsafe.Add(ref rightBase, offset + 31));

                var rx = Vector256.Add(
                    Vector256.Add(Vector256.Multiply(aw, bx), Vector256.Multiply(ax, bw)),
                    Vector256.Subtract(Vector256.Multiply(ay, bz), Vector256.Multiply(az, by)));
                var ry = Vector256.Add(
                    Vector256.Subtract(Vector256.Multiply(aw, by), Vector256.Multiply(ax, bz)),
                    Vector256.Add(Vector256.Multiply(ay, bw), Vector256.Multiply(az, bx)));
                var rz = Vector256.Add(
                    Vector256.Add(Vector256.Multiply(aw, bz), Vector256.Multiply(ax, by)),
                    Vector256.Subtract(Vector256.Multiply(az, bw), Vector256.Multiply(ay, bx)));
                var rw = Vector256.Subtract(
                    Vector256.Multiply(aw, bw),
                    Vector256.Add(
                        Vector256.Add(Vector256.Multiply(ax, bx), Vector256.Multiply(ay, by)),
                        Vector256.Multiply(az, bz)));

                for (var lane = nuint.Zero; lane < step; lane++)
                {
                    var target = offset + (lane * 4);
                    Unsafe.Add(ref targetBase, target) = rx.GetElement((int)lane);
                    Unsafe.Add(ref targetBase, target + 1) = ry.GetElement((int)lane);
                    Unsafe.Add(ref targetBase, target + 2) = rz.GetElement((int)lane);
                    Unsafe.Add(ref targetBase, target + 3) = rw.GetElement((int)lane);
                }
            }
        }

        for (; index < count; index++)
        {
            destination[(int)index] = Quaternion.Multiply(left[(int)index], right[(int)index]);
        }
    }

    /// <summary>
    /// Computes pairwise dot products.
    /// </summary>
    /// <param name="left">First vectors.</param>
    /// <param name="right">Second vectors.</param>
    /// <param name="destination">Receives the dots; must be at least <paramref name="left"/>.Length.</param>
    /// <exception cref="ArgumentException">A span is shorter than <paramref name="left"/>.</exception>
    public static void BatchDot3(ReadOnlySpan<Vector3> left, ReadOnlySpan<Vector3> right, Span<float> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);

        var count = (nuint)left.Length;
        if (count == 0)
        {
            return;
        }

        ref float leftBase = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(left));
        ref float rightBase = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(right));
        ref float targetBase = ref MemoryMarshal.GetReference(destination);
        nuint index = 0;

        if (Vector256.IsHardwareAccelerated && count >= 8)
        {
            nuint step = 8, limit = count - step + 1;
            for (; index < limit; index += step)
            {
                var offset = index * 3;

                var ax = Vector256.Create(
                    Unsafe.Add(ref leftBase, offset), Unsafe.Add(ref leftBase, offset + 3),
                    Unsafe.Add(ref leftBase, offset + 6), Unsafe.Add(ref leftBase, offset + 9),
                    Unsafe.Add(ref leftBase, offset + 12), Unsafe.Add(ref leftBase, offset + 15),
                    Unsafe.Add(ref leftBase, offset + 18), Unsafe.Add(ref leftBase, offset + 21));
                var ay = Vector256.Create(
                    Unsafe.Add(ref leftBase, offset + 1), Unsafe.Add(ref leftBase, offset + 4),
                    Unsafe.Add(ref leftBase, offset + 7), Unsafe.Add(ref leftBase, offset + 10),
                    Unsafe.Add(ref leftBase, offset + 13), Unsafe.Add(ref leftBase, offset + 16),
                    Unsafe.Add(ref leftBase, offset + 19), Unsafe.Add(ref leftBase, offset + 22));
                var az = Vector256.Create(
                    Unsafe.Add(ref leftBase, offset + 2), Unsafe.Add(ref leftBase, offset + 5),
                    Unsafe.Add(ref leftBase, offset + 8), Unsafe.Add(ref leftBase, offset + 11),
                    Unsafe.Add(ref leftBase, offset + 14), Unsafe.Add(ref leftBase, offset + 17),
                    Unsafe.Add(ref leftBase, offset + 20), Unsafe.Add(ref leftBase, offset + 23));

                var bx = Vector256.Create(
                    Unsafe.Add(ref rightBase, offset), Unsafe.Add(ref rightBase, offset + 3),
                    Unsafe.Add(ref rightBase, offset + 6), Unsafe.Add(ref rightBase, offset + 9),
                    Unsafe.Add(ref rightBase, offset + 12), Unsafe.Add(ref rightBase, offset + 15),
                    Unsafe.Add(ref rightBase, offset + 18), Unsafe.Add(ref rightBase, offset + 21));
                var by = Vector256.Create(
                    Unsafe.Add(ref rightBase, offset + 1), Unsafe.Add(ref rightBase, offset + 4),
                    Unsafe.Add(ref rightBase, offset + 7), Unsafe.Add(ref rightBase, offset + 10),
                    Unsafe.Add(ref rightBase, offset + 13), Unsafe.Add(ref rightBase, offset + 16),
                    Unsafe.Add(ref rightBase, offset + 19), Unsafe.Add(ref rightBase, offset + 22));
                var bz = Vector256.Create(
                    Unsafe.Add(ref rightBase, offset + 2), Unsafe.Add(ref rightBase, offset + 5),
                    Unsafe.Add(ref rightBase, offset + 8), Unsafe.Add(ref rightBase, offset + 11),
                    Unsafe.Add(ref rightBase, offset + 14), Unsafe.Add(ref rightBase, offset + 17),
                    Unsafe.Add(ref rightBase, offset + 20), Unsafe.Add(ref rightBase, offset + 23));

                Vector256.Add(
                    Vector256.Add(Vector256.Multiply(ax, bx), Vector256.Multiply(ay, by)),
                    Vector256.Multiply(az, bz)).StoreUnsafe(ref targetBase, index);
            }
        }

        for (; index < count; index++)
        {
            Unsafe.Add(ref targetBase, index) = Vector3.Dot(left[(int)index], right[(int)index]);
        }
    }

    /// <summary>
    /// Computes pairwise cross products.
    /// </summary>
    /// <param name="left">First vectors.</param>
    /// <param name="right">Second vectors.</param>
    /// <param name="destination">Receives the crosses; must be at least <paramref name="left"/>.Length.</param>
    /// <exception cref="ArgumentException">A span is shorter than <paramref name="left"/>.</exception>
    public static void BatchCross3(ReadOnlySpan<Vector3> left, ReadOnlySpan<Vector3> right, Span<Vector3> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);

        var count = (nuint)left.Length;
        if (count == 0)
        {
            return;
        }

        ref float leftBase = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(left));
        ref float rightBase = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(right));
        ref float targetBase = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector3, float>(destination));
        nuint index = 0;

        if (Vector256.IsHardwareAccelerated && count >= 8)
        {
            nuint step = 8, limit = count - step + 1;
            for (; index < limit; index += step)
            {
                var offset = index * 3;

                var ax = Vector256.Create(
                    Unsafe.Add(ref leftBase, offset), Unsafe.Add(ref leftBase, offset + 3),
                    Unsafe.Add(ref leftBase, offset + 6), Unsafe.Add(ref leftBase, offset + 9),
                    Unsafe.Add(ref leftBase, offset + 12), Unsafe.Add(ref leftBase, offset + 15),
                    Unsafe.Add(ref leftBase, offset + 18), Unsafe.Add(ref leftBase, offset + 21));
                var ay = Vector256.Create(
                    Unsafe.Add(ref leftBase, offset + 1), Unsafe.Add(ref leftBase, offset + 4),
                    Unsafe.Add(ref leftBase, offset + 7), Unsafe.Add(ref leftBase, offset + 10),
                    Unsafe.Add(ref leftBase, offset + 13), Unsafe.Add(ref leftBase, offset + 16),
                    Unsafe.Add(ref leftBase, offset + 19), Unsafe.Add(ref leftBase, offset + 22));
                var az = Vector256.Create(
                    Unsafe.Add(ref leftBase, offset + 2), Unsafe.Add(ref leftBase, offset + 5),
                    Unsafe.Add(ref leftBase, offset + 8), Unsafe.Add(ref leftBase, offset + 11),
                    Unsafe.Add(ref leftBase, offset + 14), Unsafe.Add(ref leftBase, offset + 17),
                    Unsafe.Add(ref leftBase, offset + 20), Unsafe.Add(ref leftBase, offset + 23));

                var bx = Vector256.Create(
                    Unsafe.Add(ref rightBase, offset), Unsafe.Add(ref rightBase, offset + 3),
                    Unsafe.Add(ref rightBase, offset + 6), Unsafe.Add(ref rightBase, offset + 9),
                    Unsafe.Add(ref rightBase, offset + 12), Unsafe.Add(ref rightBase, offset + 15),
                    Unsafe.Add(ref rightBase, offset + 18), Unsafe.Add(ref rightBase, offset + 21));
                var by = Vector256.Create(
                    Unsafe.Add(ref rightBase, offset + 1), Unsafe.Add(ref rightBase, offset + 4),
                    Unsafe.Add(ref rightBase, offset + 7), Unsafe.Add(ref rightBase, offset + 10),
                    Unsafe.Add(ref rightBase, offset + 13), Unsafe.Add(ref rightBase, offset + 16),
                    Unsafe.Add(ref rightBase, offset + 19), Unsafe.Add(ref rightBase, offset + 22));
                var bz = Vector256.Create(
                    Unsafe.Add(ref rightBase, offset + 2), Unsafe.Add(ref rightBase, offset + 5),
                    Unsafe.Add(ref rightBase, offset + 8), Unsafe.Add(ref rightBase, offset + 11),
                    Unsafe.Add(ref rightBase, offset + 14), Unsafe.Add(ref rightBase, offset + 17),
                    Unsafe.Add(ref rightBase, offset + 20), Unsafe.Add(ref rightBase, offset + 23));

                var rx = Vector256.Subtract(Vector256.Multiply(ay, bz), Vector256.Multiply(az, by));
                var ry = Vector256.Subtract(Vector256.Multiply(az, bx), Vector256.Multiply(ax, bz));
                var rz = Vector256.Subtract(Vector256.Multiply(ax, by), Vector256.Multiply(ay, bx));

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
            destination[(int)index] = Vector3.Cross(left[(int)index], right[(int)index]);
        }
    }

    /// <summary>
    /// Normalizes vectors; zero-length inputs write <see cref="Vector4.Zero"/>.
    /// </summary>
    /// <param name="source">Vectors to normalize.</param>
    /// <param name="destination">Receives the results; must be at least <paramref name="source"/>.Length.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    public static void Normalize4(ReadOnlySpan<Vector4> source, Span<Vector4> destination)
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

        if (Vector256.IsHardwareAccelerated && count >= 8)
        {
            var epsilon = Vector256.Create(NormalizeEpsilon);
            var one = Vector256.Create(1f);
            nuint step = 8, limit = count - step + 1;
            for (; index < limit; index += step)
            {
                var offset = index * 4;

                var x = Vector256.Create(
                    Unsafe.Add(ref sourceBase, offset), Unsafe.Add(ref sourceBase, offset + 4),
                    Unsafe.Add(ref sourceBase, offset + 8), Unsafe.Add(ref sourceBase, offset + 12),
                    Unsafe.Add(ref sourceBase, offset + 16), Unsafe.Add(ref sourceBase, offset + 20),
                    Unsafe.Add(ref sourceBase, offset + 24), Unsafe.Add(ref sourceBase, offset + 28));
                var y = Vector256.Create(
                    Unsafe.Add(ref sourceBase, offset + 1), Unsafe.Add(ref sourceBase, offset + 5),
                    Unsafe.Add(ref sourceBase, offset + 9), Unsafe.Add(ref sourceBase, offset + 13),
                    Unsafe.Add(ref sourceBase, offset + 17), Unsafe.Add(ref sourceBase, offset + 21),
                    Unsafe.Add(ref sourceBase, offset + 25), Unsafe.Add(ref sourceBase, offset + 29));
                var z = Vector256.Create(
                    Unsafe.Add(ref sourceBase, offset + 2), Unsafe.Add(ref sourceBase, offset + 6),
                    Unsafe.Add(ref sourceBase, offset + 10), Unsafe.Add(ref sourceBase, offset + 14),
                    Unsafe.Add(ref sourceBase, offset + 18), Unsafe.Add(ref sourceBase, offset + 22),
                    Unsafe.Add(ref sourceBase, offset + 26), Unsafe.Add(ref sourceBase, offset + 30));
                var w = Vector256.Create(
                    Unsafe.Add(ref sourceBase, offset + 3), Unsafe.Add(ref sourceBase, offset + 7),
                    Unsafe.Add(ref sourceBase, offset + 11), Unsafe.Add(ref sourceBase, offset + 15),
                    Unsafe.Add(ref sourceBase, offset + 19), Unsafe.Add(ref sourceBase, offset + 23),
                    Unsafe.Add(ref sourceBase, offset + 27), Unsafe.Add(ref sourceBase, offset + 31));

                var dot = Vector256.Add(
                    Vector256.Add(Vector256.Multiply(x, x), Vector256.Multiply(y, y)),
                    Vector256.Add(Vector256.Multiply(z, z), Vector256.Multiply(w, w)));
                var safe = Vector256.GreaterThan(dot, epsilon);
                var invLen = Vector256.Divide(one, Vector256.Sqrt(Vector256.Max(dot, epsilon)));
                invLen = Vector256.ConditionalSelect(safe, invLen, Vector256<float>.Zero);

                x = Vector256.Multiply(x, invLen);
                y = Vector256.Multiply(y, invLen);
                z = Vector256.Multiply(z, invLen);
                w = Vector256.Multiply(w, invLen);

                for (var lane = nuint.Zero; lane < step; lane++)
                {
                    var target = offset + (lane * 4);
                    Unsafe.Add(ref targetBase, target) = x.GetElement((int)lane);
                    Unsafe.Add(ref targetBase, target + 1) = y.GetElement((int)lane);
                    Unsafe.Add(ref targetBase, target + 2) = z.GetElement((int)lane);
                    Unsafe.Add(ref targetBase, target + 3) = w.GetElement((int)lane);
                }
            }
        }

        for (; index < count; index++)
        {
            var v = source[(int)index];
            var lengthSquared = v.LengthSquared();
            destination[(int)index] = lengthSquared > NormalizeEpsilon
                ? Vector4.Normalize(v)
                : Vector4.Zero;
        }
    }

    /// <summary>
    /// Normalizes 3D vectors; zero-length inputs write <see cref="Vector3.Zero"/>.
    /// </summary>
    /// <param name="source">Vectors to normalize.</param>
    /// <param name="destination">Receives the results; must be at least <paramref name="source"/>.Length.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    public static void Normalize3(ReadOnlySpan<Vector3> source, Span<Vector3> destination)
    {
        ThrowHelper.ValidateDestinationSpan(source, destination);

        for (var i = 0; i < source.Length; i++)
        {
            var v = source[i];
            var lengthSquared = v.LengthSquared();
            destination[i] = lengthSquared > NormalizeEpsilon
                ? Vector3.Normalize(v)
                : Vector3.Zero;
        }
    }
}
