namespace Axrone.Animation;

/// <summary>Pose-space kernels: forward kinematics plus override/weighted/additive blends.</summary>
public static class BlendingKernels
{
    /// <summary>Local-to-world hierarchy compose in evaluation order.</summary>
    [SkipLocalsInit]
    public static void ForwardKinematics(Rig rig, AnimationFrame localFrame, Span<Vector3> worldT, Span<Quaternion> worldR, Span<Vector3> worldS)
    {
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(localFrame);

        ReadOnlySpan<Vector3> locT = localFrame.ReadTranslations();
        ReadOnlySpan<Quaternion> locR = localFrame.ReadRotations();
        ReadOnlySpan<Vector3> locS = localFrame.ReadScales();
        ReadOnlySpan<int> order = rig.EvaluationOrder;
        ReadOnlySpan<int> parents = rig.Parents;

        for (int i = 0; i < order.Length; i++)
        {
            int b = order[i];
            int p = parents[b];
            if (p == -1)
            {
                worldT[b] = locT[b];
                worldR[b] = locR[b];
                worldS[b] = locS[b];
            }
            else
            {
                FastMath.ConcatenateLocal(worldT[p], worldR[p], worldS[p], locT[b], locR[b], locS[b], out worldT[b], out worldR[b], out worldS[b]);
            }
        }
    }

    /// <summary>
    /// Override blend with optional bone mask. Unmasked runs the vectorized fast path
    /// with copy-through at the alpha extremes.
    /// </summary>
    [SkipLocalsInit]
    public static void BlendFrame(AnimationFrame target, AnimationFrame baseFrame, AnimationFrame overlayFrame, float alpha, AnimationMask? mask = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(baseFrame);
        ArgumentNullException.ThrowIfNull(overlayFrame);

        if (baseFrame.BoneCount != overlayFrame.BoneCount || target.BoneCount != baseFrame.BoneCount)
        {
            AnimationThrowHelper.ThrowEvaluation(AnimationErrorCode.ValidationClipMismatch, "Mismatched bone count in BlendFrame.");
        }

        if (!mask.HasValue)
        {
            if (alpha <= 0.0f)
            {
                target.CopyFrom(baseFrame);
                return;
            }

            if (alpha >= 1.0f)
            {
                target.CopyFrom(overlayFrame);
                return;
            }
        }

        float t = FastMath.Clamp01(alpha);
        int boneCount = baseFrame.BoneCount;

        ReadOnlySpan<Vector3> baseT = baseFrame.ReadTranslations();
        ReadOnlySpan<Quaternion> baseR = baseFrame.ReadRotations();
        ReadOnlySpan<Vector3> baseS = baseFrame.ReadScales();

        ReadOnlySpan<Vector3> overT = overlayFrame.ReadTranslations();
        ReadOnlySpan<Quaternion> overR = overlayFrame.ReadRotations();
        ReadOnlySpan<Vector3> overS = overlayFrame.ReadScales();

        Span<Vector3> dstT = target.GetTranslations();
        Span<Quaternion> dstR = target.GetRotations();
        Span<Vector3> dstS = target.GetScales();

        for (int i = 0; i < boneCount; i++)
        {
            if (mask.HasValue && !mask.Value.IsEnabled(i))
            {
                dstT[i] = baseT[i];
                dstR[i] = baseR[i];
                dstS[i] = baseS[i];
                continue;
            }

            dstT[i] = Vector3.Lerp(baseT[i], overT[i], t);
            dstR[i] = FastMath.Slerp(baseR[i], overR[i], t);
            dstS[i] = Vector3.Lerp(baseS[i], overS[i], t);
        }

        BlendCurves(target, baseFrame, overlayFrame, t);
    }

    private static void BlendCurves(AnimationFrame target, AnimationFrame baseFrame, AnimationFrame overlayFrame, float t)
    {
        Span<float> dst = target.Curves.AsSpan();
        ReadOnlySpan<float> baseC = baseFrame.Curves.AsSpan();
        ReadOnlySpan<float> overC = overlayFrame.Curves.AsSpan();
        int count = Math.Min(dst.Length, Math.Min(baseC.Length, overC.Length));

        for (int i = 0; i < count; i++)
        {
            dst[i] = (baseC[i] * (1.0f - t)) + (overC[i] * t);
        }
    }

    /// <summary>
    /// Normalized weighted average over N frames with hemisphere-consistent
    /// quaternion accumulation. Zero total weight falls back to rest.
    /// </summary>
    [SkipLocalsInit]
    public static void BlendWeightedFrames(AnimationFrame target, ReadOnlySpan<AnimationFrame> frames, ReadOnlySpan<float> weights, Rig rig)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(rig);

        float totalWeight = 0.0f;
        int validCount = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] > 0.0f)
            {
                totalWeight += weights[i];
                validCount++;
            }
        }

        if (totalWeight <= AnimationConstants.BlendEpsilon || validCount == 0)
        {
            target.ResetToRest(rig);
            return;
        }

        int boneCount = target.BoneCount;
        Span<Vector3> dstT = target.GetTranslations();
        Span<Quaternion> dstR = target.GetRotations();
        Span<Vector3> dstS = target.GetScales();

        dstT.Clear();
        dstS.Clear();

        for (int b = 0; b < boneCount; b++)
        {
            Vector3 accT = Vector3.Zero;
            Vector3 accS = Vector3.Zero;
            Vector4 accQ = Vector4.Zero;
            Quaternion reference = Quaternion.Identity;
            bool first = true;

            for (int i = 0; i < frames.Length; i++)
            {
                float w = weights[i];
                if (w <= 0.0f)
                {
                    continue;
                }

                AnimationFrame frame = frames[i];
                accT += frame.ReadTranslations()[b] * w;
                accS += frame.ReadScales()[b] * w;

                Quaternion q = frame.ReadRotations()[b];
                if (first)
                {
                    reference = q;
                    first = false;
                }

                float dot = (q.X * reference.X) + (q.Y * reference.Y) + (q.Z * reference.Z) + (q.W * reference.W);
                Vector4 lane = new(q.X, q.Y, q.Z, q.W);
                accQ += dot < 0.0f ? -lane * w : lane * w;
            }

            float invWeight = 1.0f / totalWeight;
            dstT[b] = accT * invWeight;
            dstS[b] = accS * invWeight;
            dstR[b] = Quaternion.Normalize(new Quaternion(accQ.X, accQ.Y, accQ.Z, accQ.W));
        }

        Span<float> dstC = target.Curves.AsSpan();
        dstC.Clear();
        float invTotal = 1.0f / totalWeight;

        for (int i = 0; i < frames.Length; i++)
        {
            float w = weights[i];
            if (w <= 0.0f)
            {
                continue;
            }

            ReadOnlySpan<float> channels = frames[i].Curves.AsSpan();
            int count = Math.Min(dstC.Length, channels.Length);
            for (int c = 0; c < count; c++)
            {
                dstC[c] += channels[c] * w;
            }
        }

        for (int c = 0; c < dstC.Length; c++)
        {
            dstC[c] *= invTotal;
        }
    }

    /// <summary>
    /// Additive blend: rest-relative deltas scaled by alpha onto the base pose.
    /// Rotation deltas ride the rest-inverse sandwich with slerp-scaled magnitude.
    /// </summary>
    [SkipLocalsInit]
    public static void ApplyAdditiveFrame(AnimationFrame target, AnimationFrame baseFrame, AnimationFrame additiveFrame, Rig rig, float alpha)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(baseFrame);
        ArgumentNullException.ThrowIfNull(additiveFrame);
        ArgumentNullException.ThrowIfNull(rig);

        float a = FastMath.Clamp01(alpha);
        int boneCount = target.BoneCount;

        ReadOnlySpan<Vector3> baseT = baseFrame.ReadTranslations();
        ReadOnlySpan<Quaternion> baseR = baseFrame.ReadRotations();
        ReadOnlySpan<Vector3> baseS = baseFrame.ReadScales();

        ReadOnlySpan<Vector3> addT = additiveFrame.ReadTranslations();
        ReadOnlySpan<Quaternion> addR = additiveFrame.ReadRotations();
        ReadOnlySpan<Vector3> addS = additiveFrame.ReadScales();

        ReadOnlySpan<float> rest = rig.RestPoseBuffer;
        int rotBase = boneCount * 3;
        int scaleBase = boneCount * 7;

        Span<Vector3> dstT = target.GetTranslations();
        Span<Quaternion> dstR = target.GetRotations();
        Span<Vector3> dstS = target.GetScales();

        for (int i = 0; i < boneCount; i++)
        {
            int tOff = i * 3;
            int rOff = rotBase + (i * 4);
            int sOff = scaleBase + (i * 3);

            Vector3 restT = new(rest[tOff], rest[tOff + 1], rest[tOff + 2]);
            Quaternion restR = new(rest[rOff], rest[rOff + 1], rest[rOff + 2], rest[rOff + 3]);
            Vector3 restS = new(rest[sOff], rest[sOff + 1], rest[sOff + 2]);

            dstT[i] = baseT[i] + ((addT[i] - restT) * a);
            dstS[i] = baseS[i] + ((addS[i] - restS) * a);

            Quaternion invRest = Quaternion.Inverse(restR);
            Quaternion delta = Quaternion.Concatenate(invRest, addR[i]);
            Quaternion scaledDelta = FastMath.Slerp(Quaternion.Identity, delta, a);
            dstR[i] = Quaternion.Normalize(baseR[i] * scaledDelta);
        }

        Span<float> dstC = target.Curves.AsSpan();
        ReadOnlySpan<float> baseC = baseFrame.Curves.AsSpan();
        ReadOnlySpan<float> addC = additiveFrame.Curves.AsSpan();
        int curveCount = Math.Min(dstC.Length, Math.Min(baseC.Length, addC.Length));
        for (int i = 0; i < curveCount; i++)
        {
            dstC[i] = baseC[i] + (addC[i] * a);
        }
    }
}
