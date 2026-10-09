namespace Axrone.Animation;

// CA1815: the sampling policies are stateless strategy tags, resolved at compile
// time to a static call. They carry no data, so an Equals/== contract would
// describe nothing: there is no instance for a caller to compare, and adding one
// would invite code to branch on a difference that cannot exist.
#pragma warning disable CA1815

/// <summary>
/// Interpolation contract for one <see cref="AnimationChannel"/>. Every member is
/// <c>static abstract</c>: a policy is a stateless strategy, so the channel
/// resolves the mode to a concrete <c>readonly struct</c> and calls it directly
/// with no boxing, no allocation, and no virtual dispatch on the sampling hot
/// path. Implementations are the three authoring modes
/// (<see cref="InterpolationMode"/>) and nothing else, which is what keeps the
/// switch in <see cref="AnimationChannel.Sample(float, Span{float}, ref int)"/>
/// exhaustive over data instead of over types.
/// <para>
/// The three members are the three places a mode can act, and each is part of the
/// contract rather than an implementation detail of one mode:
/// <list type="bullet">
/// <item><see cref="SampleFirst"/> — before the first key (and the single-key
/// channel). The channel clamps here before it knows which mode is live, so every
/// policy must agree that the boundary key is taken verbatim.</item>
/// <item><see cref="SampleLast"/> — at or after the last key, same contract.</item>
/// <item><see cref="SampleSegment"/> — strictly inside the key range, where the
/// modes genuinely differ.</item>
/// </list>
/// Splitting the edges out is what lets a future mode (extrapolation, ease, a
/// different boundary rule) implement them without touching the channel.
/// </para>
/// <para>
/// All members take the packed <c>float</c> key values rather than a typed
/// value: channels are SoA-packed and may be rotation Quats, triples with
/// glTF cubic tangents, or single-lane curves, so there is one value type to keep
/// the span pipeline and the benchmark layout intact.
/// </para>
/// </summary>
public interface ISamplePolicy
{
    /// <summary>
    /// Writes the first key's value components into <paramref name="output"/>.
    /// </summary>
    /// <param name="values">Packed key values for the whole channel.</param>
    /// <param name="stride">Floats per key.</param>
    /// <param name="output">Destination span of at least the component count floats (equal to <paramref name="stride"/> except for splines, where tangents are input-only and the value is a third of the stride).</param>
    static abstract void SampleFirst(ReadOnlySpan<float> values, int stride, Span<float> output);

    /// <summary>
    /// Writes the last key's value components into <paramref name="output"/>.
    /// </summary>
    /// <param name="values">Packed key values for the whole channel.</param>
    /// <param name="stride">Floats per key; the ctor guarantees <c>values.Length</c> is an exact multiple of it, so the key count is <c>values.Length / stride</c>.</param>
    /// <param name="output">Destination span of at least the component count floats (equal to <paramref name="stride"/> except for splines, where tangents are input-only and the value is a third of the stride).</param>
    static abstract void SampleLast(ReadOnlySpan<float> values, int stride, Span<float> output);

    /// <summary>
    /// Writes the blend between two adjacent keys into <paramref name="output"/>.
    /// </summary>
    /// <param name="values">Packed key values for the whole channel.</param>
    /// <param name="stride">Floats per key.</param>
    /// <param name="target">What the channel drives; selects the rotation-only paths.</param>
    /// <param name="segment">Index of the key the time falls after; the segment spans keys <paramref name="segment"/> and <c>segment + 1</c>.</param>
    /// <param name="factor">Normalized position inside the segment, in [0, 1].</param>
    /// <param name="dt">Key time span of the segment, pre-divided for tangent scaling.</param>
    /// <param name="output">Destination span of at least the component count floats (equal to <paramref name="stride"/> except for splines, where tangents are input-only and the value is a third of the stride).</param>
    static abstract void SampleSegment(ReadOnlySpan<float> values, int stride, ChannelTarget target, int segment, float factor, float dt, Span<float> output);
}

/// <summary>
/// Holds the previous key: <see cref="InterpolationMode.Step"/> sampling. The
/// segment blend is a copy of key <c>segment</c>, so the factor and the segment
/// length are unused by construction.
/// </summary>
public readonly struct StepSamplePolicy : ISamplePolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SampleFirst(ReadOnlySpan<float> values, int stride, Span<float> output) =>
        values.Slice(0, stride).CopyTo(output);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SampleLast(ReadOnlySpan<float> values, int stride, Span<float> output) =>
        values.Slice(((values.Length / stride) - 1) * stride, stride).CopyTo(output);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SampleSegment(ReadOnlySpan<float> values, int stride, ChannelTarget target, int segment, float factor, float dt, Span<float> output) =>
        values.Slice(segment * stride, stride).CopyTo(output);
}

/// <summary>
/// Straight blend between keys: <see cref="InterpolationMode.Linear"/> sampling.
/// Rotation channels with a four-lane stride blend spherically, everything else
/// lerps component-wise; the stride guard keeps a four-lane non-rotation channel
/// (which can only be a cubic Quat triple, never sampled here) on the
/// component path.
/// </summary>
public readonly struct LinearSamplePolicy : ISamplePolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SampleFirst(ReadOnlySpan<float> values, int stride, Span<float> output) =>
        values.Slice(0, stride).CopyTo(output);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SampleLast(ReadOnlySpan<float> values, int stride, Span<float> output) =>
        values.Slice(((values.Length / stride) - 1) * stride, stride).CopyTo(output);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SampleSegment(ReadOnlySpan<float> values, int stride, ChannelTarget target, int segment, float factor, float dt, Span<float> output)
    {
        int off0 = segment * stride;
        int off1 = (segment + 1) * stride;

        if (target == ChannelTarget.Rotation && stride == 4)
        {
            Quat q0 = new(values[off0], values[off0 + 1], values[off0 + 2], values[off0 + 3]);
            Quat q1 = new(values[off1], values[off1 + 1], values[off1 + 2], values[off1 + 3]);
            Quat result = FastMath.Slerp(q0, q1, factor);
            output[0] = result.X;
            output[1] = result.Y;
            output[2] = result.Z;
            output[3] = result.W;
            return;
        }

        for (int c = 0; c < stride; c++)
        {
            output[c] = (values[off0 + c] * (1.0f - factor)) + (values[off1 + c] * factor);
        }
    }
}

/// <summary>
/// Hermite blend over glTF-layout triples: <see cref="InterpolationMode.CubicSpline"/>
/// sampling. Every key stores an in-tangent, a value, and an out-tangent, so the
/// component count is a third of the stride and tangents scale by the segment
/// length. Four-component results renormalize, since the blended Quat is
/// no longer unit length.
/// </summary>
/// <remarks>
/// Tangents are interpolation <i>input</i>, never output: the edge rules copy the
/// key's value third (like <see cref="SampleSegment"/> writes values only), so a
/// component-width destination (at most 4 floats) always suffices and callers
/// never observe packed triples.
/// </remarks>
public readonly struct CubicSamplePolicy : ISamplePolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SampleFirst(ReadOnlySpan<float> values, int stride, Span<float> output)
    {
        int compCount = stride / 3;
        values.Slice(compCount, compCount).CopyTo(output);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SampleLast(ReadOnlySpan<float> values, int stride, Span<float> output)
    {
        int compCount = stride / 3;
        int lastKeyOffset = ((values.Length / stride) - 1) * stride;
        values.Slice(lastKeyOffset + compCount, compCount).CopyTo(output);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SampleSegment(ReadOnlySpan<float> values, int stride, ChannelTarget target, int segment, float factor, float dt, Span<float> output)
    {
        int compCount = stride / 3;
        float t2 = factor * factor;
        float t3 = t2 * factor;
        float h00 = (2.0f * t3) - (3.0f * t2) + 1.0f;
        float h10 = t3 - (2.0f * t2) + factor;
        float h01 = (-2.0f * t3) + (3.0f * t2);
        float h11 = t3 - t2;

        int off0 = segment * stride;
        int off1 = (segment + 1) * stride;

        for (int c = 0; c < compCount; c++)
        {
            float p0 = values[off0 + compCount + c];
            float m0 = values[off0 + (2 * compCount) + c] * dt;
            float p1 = values[off1 + compCount + c];
            float m1 = values[off1 + c] * dt;
            output[c] = (h00 * p0) + (h10 * m0) + (h01 * p1) + (h11 * m1);
        }

        if (target == ChannelTarget.Rotation && compCount == 4)
        {
            Quat q = FastMath.Normalize(new Quat(output[0], output[1], output[2], output[3]));
            output[0] = q.X;
            output[1] = q.Y;
            output[2] = q.Z;
            output[3] = q.W;
        }
    }
}

#pragma warning restore CA1815
