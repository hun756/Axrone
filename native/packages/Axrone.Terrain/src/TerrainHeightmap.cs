namespace Axrone.Terrain;

/// <summary>Height distribution summary produced by <see cref="TerrainHeightmap.ComputeStatistics"/>.</summary>
/// <param name="Min">Smallest normalized height.</param>
/// <param name="Max">Largest normalized height.</param>
/// <param name="Mean">Arithmetic mean of all normalized heights.</param>
public readonly record struct TerrainStatistics(float Min, float Max, float Mean);

/// <summary>
/// Immutable-size, normalized heightmap backed by a <see cref="float"/> array.
/// Heights are stored row-major (<c>z * resolution + x</c>) in the <c>[0, 1]</c>
/// range; world-space scaling is applied downstream by the mesh builder and the
/// heightfield source factory.
/// </summary>
public sealed class TerrainHeightmap
{
    private readonly float[] _heights;

    private TerrainHeightmap(int resolution, float[] heights)
    {
        Resolution = resolution;
        _heights = heights;
    }

    /// <summary>Gets the vertex count per side of the height grid.</summary>
    public int Resolution { get; }

    /// <summary>Gets a read-only view over the normalized height values.</summary>
    public ReadOnlySpan<float> Heights => _heights;

    /// <summary>Gets the number of height samples.</summary>
    public int Length => _heights.Length;

    /// <summary>Creates a uniform heightmap.</summary>
    /// <param name="resolution">Vertex count per side; one of 33, 65, 129 or 257.</param>
    /// <param name="height">Normalized height in <c>[0, 1]</c>.</param>
    /// <returns>A flat heightmap.</returns>
    /// <exception cref="TerrainException">The resolution or height is invalid.</exception>
    public static TerrainHeightmap CreateFlat(int resolution, float height = 0f)
    {
        TerrainLimits.AssertResolution(resolution);
        if (!float.IsFinite(height) || height < 0f || height > 1f)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Invalid flat height: {height}. Expected a value in [0, 1]."),
                TerrainErrorCode.ValidationFailed);
        }

        var heights = new float[resolution * resolution];
        if (height != 0f)
        {
            Array.Fill(heights, height);
        }

        return new TerrainHeightmap(resolution, heights);
    }

    /// <summary>Copies and clamps a raw height buffer into a heightmap.</summary>
    /// <param name="resolution">Vertex count per side; one of 33, 65, 129 or 257.</param>
    /// <param name="heights">Normalized or unnormalized heights; values are clamped into <c>[0, 1]</c>.</param>
    /// <returns>An independent heightmap owning a copy of <paramref name="heights"/>.</returns>
    /// <exception cref="TerrainException">The buffer length or a value is invalid.</exception>
    public static TerrainHeightmap FromRawHeights(int resolution, ReadOnlySpan<float> heights)
    {
        TerrainLimits.AssertResolution(resolution);

        int expectedLength = resolution * resolution;
        if (heights.Length != expectedLength)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Height buffer length {heights.Length} does not match resolution {resolution} (expected {expectedLength})."),
                TerrainErrorCode.HeightmapSizeMismatch);
        }

        var copied = new float[expectedLength];
        for (var index = 0; index < expectedLength; index++)
        {
            float value = heights[index];
            if (!float.IsFinite(value))
            {
                throw new TerrainException(
                    FormattableString.Invariant($"Height buffer contains a non-finite value at index {index}."),
                    TerrainErrorCode.ValidationFailed);
            }

            copied[index] = TerrainMath.Clamp01(value);
        }

        return new TerrainHeightmap(resolution, copied);
    }

    /// <summary>Returns the height at integer grid coordinates.</summary>
    /// <param name="x">Grid column in <c>[0, resolution)</c>.</param>
    /// <param name="z">Grid row in <c>[0, resolution)</c>.</param>
    /// <returns>The normalized height sample.</returns>
    /// <exception cref="TerrainException">The coordinate is outside the grid.</exception>
    public float GetHeight(int x, int z)
    {
        if (x < 0 || z < 0 || x >= Resolution || z >= Resolution)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Height sample ({x}, {z}) is outside the {Resolution}x{Resolution} grid."),
                TerrainErrorCode.ValidationFailed);
        }

        return _heights[(z * Resolution) + x];
    }

    /// <summary>Bilinear sample using normalized <c>[0, 1]</c> grid coordinates.</summary>
    /// <param name="u">Horizontal coordinate; clamped into <c>[0, 1]</c>.</param>
    /// <param name="v">Vertical coordinate; clamped into <c>[0, 1]</c>.</param>
    /// <returns>The interpolated normalized height.</returns>
    public float SampleHeight(float u, float v) => TerrainMath.Bilinear(_heights, Resolution, u, v);

    /// <summary>Computes the minimum, maximum and mean normalized height.</summary>
    /// <returns>A height distribution summary.</returns>
    public TerrainStatistics ComputeStatistics()
    {
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        double total = 0d;

        foreach (float value in _heights)
        {
            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }

            total += value;
        }

        return new TerrainStatistics(min, max, (float)(total / _heights.Length));
    }

    /// <summary>Returns a copy rescaled so heights span the full <c>[0, 1]</c> range.</summary>
    /// <returns>A new normalized heightmap; a constant heightmap normalizes to zeros.</returns>
    public TerrainHeightmap Normalize()
    {
        TerrainStatistics statistics = ComputeStatistics();
        float range = statistics.Max - statistics.Min;
        var normalized = new float[_heights.Length];

        if (range > 0f)
        {
            for (var index = 0; index < normalized.Length; index++)
            {
                normalized[index] = (_heights[index] - statistics.Min) / range;
            }
        }

        return new TerrainHeightmap(Resolution, normalized);
    }

    /// <summary>Returns a deep copy of this heightmap.</summary>
    /// <returns>An independent heightmap.</returns>
    public TerrainHeightmap Clone() => new(Resolution, (float[])_heights.Clone());

    /// <summary>Copies the normalized heights into a new array.</summary>
    /// <returns>An owned array copy safe to mutate.</returns>
    public float[] ToArray() => (float[])_heights.Clone();
}
