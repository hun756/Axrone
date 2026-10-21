namespace Axrone.Numeric;

/// <summary>Validated color rendering configuration.</summary>
public struct ColorRenderingConfig
{
    /// <summary>Exposure multiplier, must be positive.</summary>
    public float Exposure
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => field;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => field = value > 0.0f ? value : ThrowHelper.ThrowArgumentOutOfRangeException<float>(nameof(value));
    } = 1.0f;

    /// <summary>Display gamma, must be positive.</summary>
    public float Gamma
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => field;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => field = value > 0.0f ? value : ThrowHelper.ThrowArgumentOutOfRangeException<float>(nameof(value));
    } = 2.4f;

    /// <summary>Creates default configuration.</summary>
    public ColorRenderingConfig()
    {
    }
}
