/**
 * Shared 2D transform utilities for the render stack.
 *
 * These functions operate on column-major 4x4 matrices (WebGL convention) and
 * perform 2D affine transformations, ignoring the z-component of the input
 * point but preserving the z-translation from the matrix.
 */

/**
 * Transform a 2D point by a 4x4 matrix, treating the point as (x, y, 0, 1).
 *
 * @param matrix - Column-major 4x4 matrix (ArrayLike<number>, length >= 16)
 * @param localX - Local X coordinate
 * @param localY - Local Y coordinate
 * @param out - Output Float32Array (length >= 3) to receive the transformed point
 * @returns The output array with (x, y, z) components
 */
export const transformPoint2D = (
    matrix: ArrayLike<number>,
    localX: number,
    localY: number,
    out: Float32Array
): Float32Array => {
    out[0] = (matrix[0] ?? 0) * localX + (matrix[1] ?? 0) * localY + (matrix[3] ?? 0);
    out[1] = (matrix[4] ?? 0) * localX + (matrix[5] ?? 0) * localY + (matrix[7] ?? 0);
    out[2] = (matrix[8] ?? 0) * localX + (matrix[9] ?? 0) * localY + (matrix[11] ?? 0);
    return out;
};
