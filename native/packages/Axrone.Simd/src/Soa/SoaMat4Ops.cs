namespace Axrone.Simd;

/// <summary>
/// Column-major 4x4 matrix kernels over float spans. All reads happen before writes, so a
/// target span may alias either input without introducing a temporary allocation.
/// </summary>
public static class SoaMat4Ops
{
    /// <summary>Composes a translation/rotation/scale matrix.</summary>
    /// <param name="target">Destination floats.</param>
    /// <param name="offset">Destination offset.</param>
    /// <param name="tx">Translation X.</param>
    /// <param name="ty">Translation Y.</param>
    /// <param name="tz">Translation Z.</param>
    /// <param name="qx">Rotation X.</param>
    /// <param name="qy">Rotation Y.</param>
    /// <param name="qz">Rotation Z.</param>
    /// <param name="qw">Rotation W.</param>
    /// <param name="sx">Scale X.</param>
    /// <param name="sy">Scale Y.</param>
    /// <param name="sz">Scale Z.</param>
    public static void Compose(
        Span<float> target,
        int offset,
        float tx,
        float ty,
        float tz,
        float qx,
        float qy,
        float qz,
        float qw,
        float sx,
        float sy,
        float sz)
    {
        float lengthSquared = (qx * qx) + (qy * qy) + (qz * qz) + (qw * qw);
        if (lengthSquared <= 1e-6f)
        {
            qx = 0f;
            qy = 0f;
            qz = 0f;
            qw = 1f;
        }
        else
        {
            float inverse = 1f / MathF.Sqrt(lengthSquared);
            qx *= inverse;
            qy *= inverse;
            qz *= inverse;
            qw *= inverse;
        }

        float x2 = qx + qx;
        float y2 = qy + qy;
        float z2 = qz + qz;
        float xx = qx * x2;
        float xy = qx * y2;
        float xz = qx * z2;
        float yy = qy * y2;
        float yz = qy * z2;
        float zz = qz * z2;
        float wx = qw * x2;
        float wy = qw * y2;
        float wz = qw * z2;

        target[offset] = (1f - (yy + zz)) * sx;
        target[offset + 1] = (xy - wz) * sy;
        target[offset + 2] = (xz + wy) * sz;
        target[offset + 3] = tx;
        target[offset + 4] = (xy + wz) * sx;
        target[offset + 5] = (1f - (xx + zz)) * sy;
        target[offset + 6] = (yz - wx) * sz;
        target[offset + 7] = ty;
        target[offset + 8] = (xz - wy) * sx;
        target[offset + 9] = (yz + wx) * sy;
        target[offset + 10] = (1f - (xx + yy)) * sz;
        target[offset + 11] = tz;
        target[offset + 12] = 0f;
        target[offset + 13] = 0f;
        target[offset + 14] = 0f;
        target[offset + 15] = 1f;
    }

    /// <summary>Multiplies two column-major matrices.</summary>
    public static void Multiply(
        Span<float> target,
        int targetOffset,
        ReadOnlySpan<float> left,
        int leftOffset,
        ReadOnlySpan<float> right,
        int rightOffset)
    {
        float a00 = left[leftOffset], a01 = left[leftOffset + 1], a02 = left[leftOffset + 2], a03 = left[leftOffset + 3];
        float a10 = left[leftOffset + 4], a11 = left[leftOffset + 5], a12 = left[leftOffset + 6], a13 = left[leftOffset + 7];
        float a20 = left[leftOffset + 8], a21 = left[leftOffset + 9], a22 = left[leftOffset + 10], a23 = left[leftOffset + 11];
        float a30 = left[leftOffset + 12], a31 = left[leftOffset + 13], a32 = left[leftOffset + 14], a33 = left[leftOffset + 15];

        float b00 = right[rightOffset], b01 = right[rightOffset + 1], b02 = right[rightOffset + 2], b03 = right[rightOffset + 3];
        float b10 = right[rightOffset + 4], b11 = right[rightOffset + 5], b12 = right[rightOffset + 6], b13 = right[rightOffset + 7];
        float b20 = right[rightOffset + 8], b21 = right[rightOffset + 9], b22 = right[rightOffset + 10], b23 = right[rightOffset + 11];
        float b30 = right[rightOffset + 12], b31 = right[rightOffset + 13], b32 = right[rightOffset + 14], b33 = right[rightOffset + 15];

        target[targetOffset] = (a00 * b00) + (a01 * b10) + (a02 * b20) + (a03 * b30);
        target[targetOffset + 1] = (a00 * b01) + (a01 * b11) + (a02 * b21) + (a03 * b31);
        target[targetOffset + 2] = (a00 * b02) + (a01 * b12) + (a02 * b22) + (a03 * b32);
        target[targetOffset + 3] = (a00 * b03) + (a01 * b13) + (a02 * b23) + (a03 * b33);
        target[targetOffset + 4] = (a10 * b00) + (a11 * b10) + (a12 * b20) + (a13 * b30);
        target[targetOffset + 5] = (a10 * b01) + (a11 * b11) + (a12 * b21) + (a13 * b31);
        target[targetOffset + 6] = (a10 * b02) + (a11 * b12) + (a12 * b22) + (a13 * b32);
        target[targetOffset + 7] = (a10 * b03) + (a11 * b13) + (a12 * b23) + (a13 * b33);
        target[targetOffset + 8] = (a20 * b00) + (a21 * b10) + (a22 * b20) + (a23 * b30);
        target[targetOffset + 9] = (a20 * b01) + (a21 * b11) + (a22 * b21) + (a23 * b31);
        target[targetOffset + 10] = (a20 * b02) + (a21 * b12) + (a22 * b22) + (a23 * b32);
        target[targetOffset + 11] = (a20 * b03) + (a21 * b13) + (a22 * b23) + (a23 * b33);
        target[targetOffset + 12] = (a30 * b00) + (a31 * b10) + (a32 * b20) + (a33 * b30);
        target[targetOffset + 13] = (a30 * b01) + (a31 * b11) + (a32 * b21) + (a33 * b31);
        target[targetOffset + 14] = (a30 * b02) + (a31 * b12) + (a32 * b22) + (a33 * b32);
        target[targetOffset + 15] = (a30 * b03) + (a31 * b13) + (a32 * b23) + (a33 * b33);
    }

    /// <summary>Inverts a column-major matrix.</summary>
    /// <param name="target">Destination floats.</param>
    /// <param name="targetOffset">Destination offset.</param>
    /// <param name="source">Source floats.</param>
    /// <param name="sourceOffset">Source offset.</param>
    /// <returns><see langword="false"/> when the matrix is singular.</returns>
    public static bool TryInvert(
        Span<float> target,
        int targetOffset,
        ReadOnlySpan<float> source,
        int sourceOffset)
    {
        float a00 = source[sourceOffset], a01 = source[sourceOffset + 1], a02 = source[sourceOffset + 2], a03 = source[sourceOffset + 3];
        float a10 = source[sourceOffset + 4], a11 = source[sourceOffset + 5], a12 = source[sourceOffset + 6], a13 = source[sourceOffset + 7];
        float a20 = source[sourceOffset + 8], a21 = source[sourceOffset + 9], a22 = source[sourceOffset + 10], a23 = source[sourceOffset + 11];
        float a30 = source[sourceOffset + 12], a31 = source[sourceOffset + 13], a32 = source[sourceOffset + 14], a33 = source[sourceOffset + 15];

        float b00 = (a00 * a11) - (a01 * a10);
        float b01 = (a00 * a12) - (a02 * a10);
        float b02 = (a00 * a13) - (a03 * a10);
        float b03 = (a01 * a12) - (a02 * a11);
        float b04 = (a01 * a13) - (a03 * a11);
        float b05 = (a02 * a13) - (a03 * a12);
        float b06 = (a20 * a31) - (a21 * a30);
        float b07 = (a20 * a32) - (a22 * a30);
        float b08 = (a20 * a33) - (a23 * a30);
        float b09 = (a21 * a32) - (a22 * a31);
        float b10 = (a21 * a33) - (a23 * a31);
        float b11 = (a22 * a33) - (a23 * a32);

        float determinant = (b00 * b11) - (b01 * b10) + (b02 * b09) + (b03 * b08) - (b04 * b07) + (b05 * b06);
        if (!float.IsFinite(determinant) || MathF.Abs(determinant) <= 1e-6f)
        {
            return false;
        }

        float inverse = 1f / determinant;
        target[targetOffset] = ((a11 * b11) - (a12 * b10) + (a13 * b09)) * inverse;
        target[targetOffset + 1] = ((a02 * b10) - (a01 * b11) - (a03 * b09)) * inverse;
        target[targetOffset + 2] = ((a31 * b05) - (a32 * b04) + (a33 * b03)) * inverse;
        target[targetOffset + 3] = ((a22 * b04) - (a21 * b05) - (a23 * b03)) * inverse;
        target[targetOffset + 4] = ((a12 * b08) - (a10 * b11) - (a13 * b07)) * inverse;
        target[targetOffset + 5] = ((a00 * b11) - (a02 * b08) + (a03 * b07)) * inverse;
        target[targetOffset + 6] = ((a32 * b02) - (a30 * b05) - (a33 * b01)) * inverse;
        target[targetOffset + 7] = ((a20 * b05) - (a22 * b02) + (a23 * b01)) * inverse;
        target[targetOffset + 8] = ((a10 * b10) - (a11 * b08) + (a13 * b06)) * inverse;
        target[targetOffset + 9] = ((a01 * b08) - (a00 * b10) - (a03 * b06)) * inverse;
        target[targetOffset + 10] = ((a30 * b04) - (a31 * b02) + (a33 * b00)) * inverse;
        target[targetOffset + 11] = ((a21 * b02) - (a20 * b04) - (a23 * b00)) * inverse;
        target[targetOffset + 12] = ((a11 * b07) - (a10 * b09) - (a12 * b06)) * inverse;
        target[targetOffset + 13] = ((a00 * b09) - (a01 * b07) + (a02 * b06)) * inverse;
        target[targetOffset + 14] = ((a31 * b01) - (a30 * b03) - (a32 * b00)) * inverse;
        target[targetOffset + 15] = ((a20 * b03) - (a21 * b01) + (a22 * b00)) * inverse;
        return true;
    }
}
