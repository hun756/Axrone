namespace Axrone.Terrain;

/// <summary>
/// Per-texel splat blending shared by the texture-space painter and the chunk engine.
/// Channel weights always sum to <see cref="WeightTotal"/>, so repeated stamps stay
/// lossless and the shader can renormalize cheaply.
/// </summary>
public static class TerrainSplatMath
{
    /// <summary>Total weight stored across the four channels.</summary>
    public const int WeightTotal = 255;

    /// <summary>
    /// Applies one blend step to a four-channel texel, moving the target layer toward full
    /// weight and redistributing the remaining weight across the other channels.
    /// </summary>
    /// <param name="texel">Four-channel RGBA texel.</param>
    /// <param name="layerIndex">Target channel in <c>[0, 3]</c>.</param>
    /// <param name="amount">Blend amount in <c>[0, 1]</c>.</param>
    /// <returns><see langword="true"/> when the texel changed.</returns>
    /// <exception cref="TerrainException">The texel or layer index is invalid.</exception>
    public static bool Paint(Span<byte> texel, int layerIndex, float amount)
    {
        if (texel.Length < 4)
        {
            throw new TerrainException(
                FormattableString.Invariant($"Splat texel requires 4 bytes, received {texel.Length}."),
                TerrainErrorCode.ValidationFailed);
        }

        if (layerIndex < 0 || layerIndex >= TerrainLimits.MaxLayers)
        {
            throw new TerrainException(
                FormattableString.Invariant(
                    $"Invalid splat layer index: {layerIndex}. Expected an integer in [0, {TerrainLimits.MaxLayers - 1}]."),
                TerrainErrorCode.ValidationFailed);
        }

        int current = texel[layerIndex];
        int target = Math.Min(
            WeightTotal,
            TerrainMath.RoundHalfUp(current + ((WeightTotal - current) * amount)));
        if (target == current)
        {
            return false;
        }

        texel[layerIndex] = (byte)target;
        int remaining = WeightTotal - target;
        int othersTotal = WeightTotal - current;

        var distributed = 0;
        var lastOtherChannel = -1;
        for (var channel = 0; channel < 4; channel++)
        {
            if (channel == layerIndex)
            {
                continue;
            }

            lastOtherChannel = channel;
            int value = texel[channel];
            int scaled = othersTotal > 0
                ? TerrainMath.RoundHalfUp((value / (float)othersTotal) * remaining)
                : channel == (layerIndex + 1) % 4
                    ? remaining
                    : 0;
            texel[channel] = (byte)scaled;
            distributed += scaled;
        }

        int remainder = remaining - distributed;
        if (remainder != 0 && lastOtherChannel >= 0)
        {
            texel[lastOtherChannel] = (byte)Math.Max(0, texel[lastOtherChannel] + remainder);
        }

        return true;
    }
}
