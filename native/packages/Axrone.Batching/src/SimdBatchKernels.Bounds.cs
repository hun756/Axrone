namespace Axrone.Batching;

/// <summary>
/// Batch bound transforms: AABB by matrix, bounding spheres by matrix plus uniform radius scale.
/// </summary>
/// <remarks>
/// Both use the center/extent identity — <c>newCenter = M * center</c>,
/// <c>newExtent = |M| * extent</c> — so a rotation never has to visit all eight corners. All matrix
/// broadcasts are hoisted out of the vector loop.
/// </remarks>
public static unsafe partial class SimdBatchKernels
{
    /// <summary>
    /// Transforms axis-aligned boxes by <paramref name="matrix"/>.
    /// </summary>
    /// <param name="source">Boxes to transform.</param>
    /// <param name="destination">Receives the results; must be at least <paramref name="source"/>.Length.</param>
    /// <param name="matrix">Row-major transform.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than <paramref name="source"/>.</exception>
    public static void TransformAabb(ReadOnlySpan<Aabb> source, Span<Aabb> destination, in Matrix4x4 matrix)
    {
        ThrowHelper.ValidateDestinationSpan(source, destination);

        var count = (nuint)source.Length;
        if (count == 0)
        {
            return;
        }

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
            var tx = Vector256.Create(matrix.M41);
            var ty = Vector256.Create(matrix.M42);
            var tz = Vector256.Create(matrix.M43);

            var a11 = Vector256.Create(MathF.Abs(matrix.M11));
            var a12 = Vector256.Create(MathF.Abs(matrix.M12));
            var a13 = Vector256.Create(MathF.Abs(matrix.M13));
            var a21 = Vector256.Create(MathF.Abs(matrix.M21));
            var a22 = Vector256.Create(MathF.Abs(matrix.M22));
            var a23 = Vector256.Create(MathF.Abs(matrix.M23));
            var a31 = Vector256.Create(MathF.Abs(matrix.M31));
            var a32 = Vector256.Create(MathF.Abs(matrix.M32));
            var a33 = Vector256.Create(MathF.Abs(matrix.M33));

            var half = Vector256.Create(0.5f);

            ref Aabb sourceBase = ref MemoryMarshal.GetReference(source);
            ref Aabb targetBase = ref MemoryMarshal.GetReference(destination);

            nuint step = 8, limit = count - step + 1;
            for (; index < limit; index += step)
            {
                var i = (int)index;
                var minX = Vector256.Create(
                    Unsafe.Add(ref sourceBase, i).MinX, Unsafe.Add(ref sourceBase, i + 1).MinX,
                    Unsafe.Add(ref sourceBase, i + 2).MinX, Unsafe.Add(ref sourceBase, i + 3).MinX,
                    Unsafe.Add(ref sourceBase, i + 4).MinX, Unsafe.Add(ref sourceBase, i + 5).MinX,
                    Unsafe.Add(ref sourceBase, i + 6).MinX, Unsafe.Add(ref sourceBase, i + 7).MinX);
                var minY = Vector256.Create(
                    Unsafe.Add(ref sourceBase, i).MinY, Unsafe.Add(ref sourceBase, i + 1).MinY,
                    Unsafe.Add(ref sourceBase, i + 2).MinY, Unsafe.Add(ref sourceBase, i + 3).MinY,
                    Unsafe.Add(ref sourceBase, i + 4).MinY, Unsafe.Add(ref sourceBase, i + 5).MinY,
                    Unsafe.Add(ref sourceBase, i + 6).MinY, Unsafe.Add(ref sourceBase, i + 7).MinY);
                var minZ = Vector256.Create(
                    Unsafe.Add(ref sourceBase, i).MinZ, Unsafe.Add(ref sourceBase, i + 1).MinZ,
                    Unsafe.Add(ref sourceBase, i + 2).MinZ, Unsafe.Add(ref sourceBase, i + 3).MinZ,
                    Unsafe.Add(ref sourceBase, i + 4).MinZ, Unsafe.Add(ref sourceBase, i + 5).MinZ,
                    Unsafe.Add(ref sourceBase, i + 6).MinZ, Unsafe.Add(ref sourceBase, i + 7).MinZ);
                var maxX = Vector256.Create(
                    Unsafe.Add(ref sourceBase, i).MaxX, Unsafe.Add(ref sourceBase, i + 1).MaxX,
                    Unsafe.Add(ref sourceBase, i + 2).MaxX, Unsafe.Add(ref sourceBase, i + 3).MaxX,
                    Unsafe.Add(ref sourceBase, i + 4).MaxX, Unsafe.Add(ref sourceBase, i + 5).MaxX,
                    Unsafe.Add(ref sourceBase, i + 6).MaxX, Unsafe.Add(ref sourceBase, i + 7).MaxX);
                var maxY = Vector256.Create(
                    Unsafe.Add(ref sourceBase, i).MaxY, Unsafe.Add(ref sourceBase, i + 1).MaxY,
                    Unsafe.Add(ref sourceBase, i + 2).MaxY, Unsafe.Add(ref sourceBase, i + 3).MaxY,
                    Unsafe.Add(ref sourceBase, i + 4).MaxY, Unsafe.Add(ref sourceBase, i + 5).MaxY,
                    Unsafe.Add(ref sourceBase, i + 6).MaxY, Unsafe.Add(ref sourceBase, i + 7).MaxY);
                var maxZ = Vector256.Create(
                    Unsafe.Add(ref sourceBase, i).MaxZ, Unsafe.Add(ref sourceBase, i + 1).MaxZ,
                    Unsafe.Add(ref sourceBase, i + 2).MaxZ, Unsafe.Add(ref sourceBase, i + 3).MaxZ,
                    Unsafe.Add(ref sourceBase, i + 4).MaxZ, Unsafe.Add(ref sourceBase, i + 5).MaxZ,
                    Unsafe.Add(ref sourceBase, i + 6).MaxZ, Unsafe.Add(ref sourceBase, i + 7).MaxZ);

                var cx = Vector256.Multiply(Vector256.Add(minX, maxX), half);
                var cy = Vector256.Multiply(Vector256.Add(minY, maxY), half);
                var cz = Vector256.Multiply(Vector256.Add(minZ, maxZ), half);
                var ex = Vector256.Multiply(Vector256.Subtract(maxX, minX), half);
                var ey = Vector256.Multiply(Vector256.Subtract(maxY, minY), half);
                var ez = Vector256.Multiply(Vector256.Subtract(maxZ, minZ), half);

                var ncx = Vector256.Add(
                    Vector256.Add(Vector256.Multiply(cx, m11), Vector256.Multiply(cy, m21)),
                    Vector256.Add(Vector256.Multiply(cz, m31), tx));
                var ncy = Vector256.Add(
                    Vector256.Add(Vector256.Multiply(cx, m12), Vector256.Multiply(cy, m22)),
                    Vector256.Add(Vector256.Multiply(cz, m32), ty));
                var ncz = Vector256.Add(
                    Vector256.Add(Vector256.Multiply(cx, m13), Vector256.Multiply(cy, m23)),
                    Vector256.Add(Vector256.Multiply(cz, m33), tz));

                var nex = Vector256.Add(
                    Vector256.Add(Vector256.Multiply(ex, a11), Vector256.Multiply(ey, a21)),
                    Vector256.Multiply(ez, a31));
                var ney = Vector256.Add(
                    Vector256.Add(Vector256.Multiply(ex, a12), Vector256.Multiply(ey, a22)),
                    Vector256.Multiply(ez, a32));
                var nez = Vector256.Add(
                    Vector256.Add(Vector256.Multiply(ex, a13), Vector256.Multiply(ey, a23)),
                    Vector256.Multiply(ez, a33));

                for (var lane = 0; lane < 8; lane++)
                {
                    Unsafe.Add(ref targetBase, i + lane) = new Aabb(
                        ncx.GetElement(lane) - nex.GetElement(lane),
                        ncy.GetElement(lane) - ney.GetElement(lane),
                        ncz.GetElement(lane) - nez.GetElement(lane),
                        ncx.GetElement(lane) + nex.GetElement(lane),
                        ncy.GetElement(lane) + ney.GetElement(lane),
                        ncz.GetElement(lane) + nez.GetElement(lane));
                }
            }
        }

        for (; index < count; index++)
        {
            var i = (int)index;
            var center = Vector3.Transform(source[i].Center, matrix);
            var sourceExtent = source[i].Extent;
            var extent = new Vector3(
                (MathF.Abs(matrix.M11) * sourceExtent.X) + (MathF.Abs(matrix.M21) * sourceExtent.Y) + (MathF.Abs(matrix.M31) * sourceExtent.Z),
                (MathF.Abs(matrix.M12) * sourceExtent.X) + (MathF.Abs(matrix.M22) * sourceExtent.Y) + (MathF.Abs(matrix.M32) * sourceExtent.Z),
                (MathF.Abs(matrix.M13) * sourceExtent.X) + (MathF.Abs(matrix.M23) * sourceExtent.Y) + (MathF.Abs(matrix.M33) * sourceExtent.Z));
            destination[i] = Aabb.FromCenterExtent(center, extent);
        }
    }

    /// <summary>
    /// Transforms bounding spheres: centers by <paramref name="matrix"/>, radii scaled by
    /// <paramref name="maxScale"/>.
    /// </summary>
    /// <param name="source">Spheres as <c>(center.X, center.Y, center.Z, radius)</c>.</param>
    /// <param name="destination">Receives the results; must be at least <paramref name="source"/>.Length.</param>
    /// <param name="matrix">Row-major transform.</param>
    /// <param name="maxScale">Largest scale factor of <paramref name="matrix"/>; must be finite and non-negative.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is too short.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxScale"/> is negative or non-finite.</exception>
    /// <remarks>
    /// The radius scale is caller-supplied so this kernel never takes a matrix square root in the hot
    /// loop; the caller computes it once per matrix, not once per sphere.
    /// </remarks>
    public static void TransformBoundingSpheres(
        ReadOnlySpan<Vector4> source, Span<Vector4> destination, in Matrix4x4 matrix, float maxScale)
    {
        ThrowHelper.ValidateDestinationSpan(source, destination);
        if (maxScale < 0f || !float.IsFinite(maxScale))
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(maxScale));
        }

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
            var r0 = Vector256.Create(matrix.M11, matrix.M12, matrix.M13, 0f, matrix.M11, matrix.M12, matrix.M13, 0f);
            var r1 = Vector256.Create(matrix.M21, matrix.M22, matrix.M23, 0f, matrix.M21, matrix.M22, matrix.M23, 0f);
            var r2 = Vector256.Create(matrix.M31, matrix.M32, matrix.M33, 0f, matrix.M31, matrix.M32, matrix.M33, 0f);
            var r3 = Vector256.Create(matrix.M41, matrix.M42, matrix.M43, 0f, matrix.M41, matrix.M42, matrix.M43, 0f);
            var scale = Vector256.Create(0f, 0f, 0f, maxScale, 0f, 0f, 0f, maxScale);

            nuint step = 2, limit = count - step + 1;
            for (; index < limit; index += step)
            {
                var offset = index * 4;

                var x = Vector256.Create(
                    Unsafe.Add(ref sourceBase, offset), Unsafe.Add(ref sourceBase, offset),
                    Unsafe.Add(ref sourceBase, offset), 0f,
                    Unsafe.Add(ref sourceBase, offset + 4), Unsafe.Add(ref sourceBase, offset + 4),
                    Unsafe.Add(ref sourceBase, offset + 4), 0f);
                var y = Vector256.Create(
                    Unsafe.Add(ref sourceBase, offset + 1), Unsafe.Add(ref sourceBase, offset + 1),
                    Unsafe.Add(ref sourceBase, offset + 1), 0f,
                    Unsafe.Add(ref sourceBase, offset + 5), Unsafe.Add(ref sourceBase, offset + 5),
                    Unsafe.Add(ref sourceBase, offset + 5), 0f);
                var z = Vector256.Create(
                    Unsafe.Add(ref sourceBase, offset + 2), Unsafe.Add(ref sourceBase, offset + 2),
                    Unsafe.Add(ref sourceBase, offset + 2), 0f,
                    Unsafe.Add(ref sourceBase, offset + 6), Unsafe.Add(ref sourceBase, offset + 6),
                    Unsafe.Add(ref sourceBase, offset + 6), 0f);
                var radius = Vector256.Create(
                    0f, 0f, 0f, Unsafe.Add(ref sourceBase, offset + 3),
                    0f, 0f, 0f, Unsafe.Add(ref sourceBase, offset + 7));

                var transformed = Vector256.Add(
                    Vector256.Add(Vector256.Multiply(x, r0), Vector256.Multiply(y, r1)),
                    Vector256.Add(Vector256.Multiply(z, r2), r3));

                Vector256.Add(transformed, Vector256.Multiply(radius, scale))
                    .StoreUnsafe(ref targetBase, offset);
            }
        }

        for (; index < count; index++)
        {
            var i = (int)index;
            var center = Vector3.Transform(new Vector3(source[i].X, source[i].Y, source[i].Z), matrix);
            destination[i] = new Vector4(center.X, center.Y, center.Z, source[i].W * maxScale);
        }
    }
}
