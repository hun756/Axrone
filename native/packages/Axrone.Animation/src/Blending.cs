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
}
