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
                worldS[b] = worldS[p] * locS[b];
                worldR[b] = Quaternion.Normalize(worldR[p] * locR[b]);
                worldT[b] = worldT[p] + Vector3.Transform(locT[b] * worldS[p], worldR[p]);
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
}
