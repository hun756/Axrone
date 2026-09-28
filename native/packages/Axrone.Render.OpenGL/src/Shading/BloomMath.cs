namespace Axrone.Render.OpenGL.Shading;

/// <summary>
/// Pure CPU derivation of the Karis soft-knee bloom prefilter curve and of the
/// mip-pyramid level count used by <c>BloomPassExecutor</c>.
/// </summary>
/// <remarks>
/// <para>
/// The GPU evaluates the same curve in <c>BloomShaders.PrefilterFragment</c>; this type is
/// the authority for the scalar uniforms that feed it. Kept free of GL types so the curve
/// can be unit-tested without a context.
/// </para>
/// <para>
/// Conventions:
/// <list type="bullet">
///   <item><description>Luminance is Rec.709 (<c>0.2126 R + 0.7152 G + 0.0722 B</c>).</description></item>
///   <item><description>Knee width is <c>max(0, threshold) * clamp(softKnee, 0, 1)</c>.</description></item>
///   <item><description>A non-positive knee collapses to a hard threshold guarded by <see cref="Epsilon"/>.</description></item>
/// </list>
/// </para>
/// </remarks>
public static class BloomMath
{
    /// <summary>Default soft-knee factor: half the threshold is ramped, halving the visible edge.</summary>
    public const float DefaultSoftKnee = 0.5f;

    /// <summary>Smallest pyramid level count. A pyramid always has at least one halving step.</summary>
    public const int MinMipCount = 2;

    /// <summary>Largest pyramid level count. Bounds the smallest level to 1/256 of the source.</summary>
    public const int MaxMipCount = 8;

    /// <summary>Comparison guard for degenerate knees and hard-threshold boundaries.</summary>
    public const float Epsilon = 1e-5f;

    /// <summary>Rec.709 red weight.</summary>
    public const float LumaR = 0.2126f;

    /// <summary>Rec.709 green weight.</summary>
    public const float LumaG = 0.7152f;

    /// <summary>Rec.709 blue weight.</summary>
    public const float LumaB = 0.0722f;

    /// <summary>
    /// Computes the Rec.709 luminance of a linear-RGB color.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    /// <returns>The weighted luminance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Luminance(float r, float g, float b)
    {
        return (LumaR * r) + (LumaG * g) + (LumaB * b);
    }

    /// <summary>
    /// Computes the Karis average weight <c>1 / (1 + luma)</c>, which biases
    /// downsample groups toward their darker taps so a single bright pixel cannot
    /// dominate a mip level.
    /// </summary>
    /// <param name="luminance">The tap luminance.</param>
    /// <returns>The group weight.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float KarisWeight(float luminance)
    {
        return 1f / (1f + MathF.Max(luminance, 0f));
    }

    /// <summary>
    /// Computes the soft-knee width for a threshold.
    /// </summary>
    /// <param name="threshold">The luminance threshold. Negative values clamp to zero.</param>
    /// <param name="softKnee">The soft-knee factor. Clamped to <c>[0, 1]</c>.</param>
    /// <returns>The knee width, in luminance units.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ComputeKneeWidth(float threshold, float softKnee)
    {
        float t = MathF.Max(0f, threshold);
        float k = Math.Clamp(softKnee, 0f, 1f);
        return t * k;
    }

    /// <summary>
    /// Evaluates the Karis soft-knee prefilter contribution: a quadratic ramp from
    /// <c>0</c> at <c>threshold - knee/4</c> to <c>1</c> at <c>threshold + knee/4</c>.
    /// </summary>
    /// <param name="luminance">The tap luminance.</param>
    /// <param name="threshold">The luminance threshold.</param>
    /// <param name="softKnee">The soft-knee factor. Clamped to <c>[0, 1]</c>.</param>
    /// <returns>
    /// The contribution in <c>[0, 1]</c>: <c>0.25</c> exactly at the threshold,
    /// <c>0</c> when a non-positive knee degenerates to a hard threshold.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SoftKneeContribution(float luminance, float threshold, float softKnee)
    {
        // One clamped threshold drives both the knee width and the comparison, so a
        // negative threshold degenerates to a hard cut at zero.
        float t = MathF.Max(0f, threshold);
        float knee = ComputeKneeWidth(t, softKnee);

        if (knee <= Epsilon)
        {
            // Degenerate knee: hard threshold. The epsilon guard keeps a tap sitting
            // exactly on the boundary from flickering between 0 and 1.
            return luminance > t + Epsilon ? 1f : 0f;
        }

        float kneeQuadratic = knee * 0.25f;

        if (luminance + kneeQuadratic < t)
        {
            return 0f;
        }

        if (luminance - kneeQuadratic > t)
        {
            return 1f;
        }

        float s = (luminance - t + kneeQuadratic) / (2f * kneeQuadratic);
        return s * s;
    }

    /// <summary>
    /// Computes how many pyramid levels a bloom chain of a given source size needs.
    /// Level 0 is half the source resolution; every further level halves both axes with
    /// a floor of 1 pixel, so a 1x1 source still yields two 1x1 levels.
    /// </summary>
    /// <param name="width">The source width in pixels.</param>
    /// <param name="height">The source height in pixels.</param>
    /// <param name="requested">The requested level count. Clamped to <c>[<see cref="MinMipCount"/>, <see cref="MaxMipCount"/>]</c>.</param>
    /// <returns>The level count, always in <c>[<see cref="MinMipCount"/>, <see cref="MaxMipCount"/>]</c>.</returns>
    public static int ComputeMipCount(int width, int height, int requested)
    {
        int target = Math.Clamp(requested, MinMipCount, MaxMipCount);

        // Level 0 is the half-resolution prefilter target.
        int w = Math.Max(1, width / 2);
        int h = Math.Max(1, height / 2);
        int levels = 1;

        while (levels < target && (w > 1 || h > 1))
        {
            w = Math.Max(1, w / 2);
            h = Math.Max(1, h / 2);
            levels++;
        }

        return Math.Max(levels, MinMipCount);
    }
}
