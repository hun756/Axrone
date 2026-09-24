namespace Axrone.Animation;

/// <summary>Position-based IK solvers operating on world-space chains.</summary>
public static class IkSolvers
{
    /// <summary>
    /// FABRIK with rotation write-back: solves positions forward/backward, then rotates
    /// each joint from its current direction to the solved direction. Unreachable
    /// targets stretch straight toward the goal.
    /// </summary>
    [SkipLocalsInit]
    public static void SolveFabrik(
        Rig rig,
        AnimationFrame frame,
        ReadOnlySpan<int> chainBoneIndices,
        Vector3 targetPos,
        Span<Vector3> scratchPositions,
        int maxIterations = AnimationConstants.IkDefaultMaxIterations,
        float precision = AnimationConstants.IkDefaultPrecision)
    {
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(frame);
        if (chainBoneIndices.Length < 2 || scratchPositions.Length < chainBoneIndices.Length)
        {
            return;
        }

        Span<Vector3> worldT = stackalloc Vector3[rig.BoneCount];
        Span<Quaternion> worldR = stackalloc Quaternion[rig.BoneCount];
        Span<Vector3> worldS = stackalloc Vector3[rig.BoneCount];
        BlendingKernels.ForwardKinematics(rig, frame, worldT, worldR, worldS);

        int count = chainBoneIndices.Length;
        for (int i = 0; i < count; i++)
        {
            scratchPositions[i] = worldT[chainBoneIndices[i]];
        }

        float totalLength = 0.0f;
        for (int i = 0; i < count - 1; i++)
        {
            totalLength += Vector3.Distance(worldT[chainBoneIndices[i]], worldT[chainBoneIndices[i + 1]]);
        }

        Vector3 rootPos = scratchPositions[0];
        if (Vector3.Distance(rootPos, targetPos) >= totalLength)
        {
            Vector3 direction = targetPos - rootPos;
            if (direction.LengthSquared() > AnimationConstants.SoaEpsilon)
            {
                direction = Vector3.Normalize(direction);
            }
            else
            {
                direction = Vector3.UnitX;
            }

            float walked = 0.0f;
            for (int i = 1; i < count; i++)
            {
                walked += Vector3.Distance(worldT[chainBoneIndices[i - 1]], worldT[chainBoneIndices[i]]);
                scratchPositions[i] = rootPos + (direction * walked);
            }
        }
        else
        {
            int iterations = 0;
            for (int iter = 0; iter < maxIterations; iter++)
            {
                iterations++;
                if (Vector3.DistanceSquared(scratchPositions[count - 1], targetPos) <= precision * precision)
                {
                    break;
                }

                scratchPositions[count - 1] = targetPos;
                for (int i = count - 2; i >= 0; i--)
                {
                    scratchPositions[i] = ReachToward(scratchPositions[i + 1], scratchPositions[i], BoneLength(worldT, chainBoneIndices, i));
                }

                scratchPositions[0] = rootPos;
                for (int i = 0; i < count - 1; i++)
                {
                    scratchPositions[i + 1] = ReachToward(scratchPositions[i], scratchPositions[i + 1], BoneLength(worldT, chainBoneIndices, i));
                }
            }

            AnimationTelemetry.RecordIkIterations(iterations);
        }

        WriteBackRotations(rig, frame, worldT, worldR, chainBoneIndices, scratchPositions);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float BoneLength(ReadOnlySpan<Vector3> worldT, ReadOnlySpan<int> chain, int link) =>
        MathF.Max(Vector3.Distance(worldT[chain[link]], worldT[chain[link + 1]]), AnimationConstants.SoaEpsilon);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 ReachToward(Vector3 anchor, Vector3 point, float length)
    {
        Vector3 diff = point - anchor;
        float distance = diff.Length();
        if (distance <= AnimationConstants.SoaEpsilon)
        {
            return anchor + (Vector3.UnitX * length);
        }

        return anchor + ((diff / distance) * length);
    }

    private static void WriteBackRotations(
        Rig rig,
        AnimationFrame frame,
        ReadOnlySpan<Vector3> worldT,
        ReadOnlySpan<Quaternion> worldR,
        ReadOnlySpan<int> chain,
        ReadOnlySpan<Vector3> solved)
    {
        Span<Quaternion> localR = frame.GetRotations();

        for (int i = 0; i < chain.Length - 1; i++)
        {
            int bone = chain[i];
            Vector3 currentDir = worldT[chain[i + 1]] - worldT[bone];
            Vector3 targetDir = solved[i + 1] - solved[i];
            if (currentDir.LengthSquared() < AnimationConstants.SoaEpsilon
                || targetDir.LengthSquared() < AnimationConstants.SoaEpsilon)
            {
                continue;
            }

            Quaternion correction = FastMath.QuaternionFromTo(currentDir, targetDir);
            int parent = rig.Parents[bone];
            Quaternion parentWorld = parent != -1 ? worldR[parent] : Quaternion.Identity;
            Quaternion newLocal = FastMath.Normalize(
                FastMath.Multiply(FastMath.Multiply(FastMath.Invert(parentWorld), correction), FastMath.Multiply(parentWorld, localR[bone])));
            localR[bone] = newLocal;
        }
    }

    /// <summary>
    /// Cyclic Coordinate Descent with per-joint from-to corrections blended by weight.
    /// Recomputes world transforms as it descends so each joint sees fresh tips.
    /// </summary>
    [SkipLocalsInit]
    public static void SolveCcd(
        Rig rig,
        AnimationFrame frame,
        ReadOnlySpan<int> chainBoneIndices,
        Vector3 targetPos,
        float weight = 1.0f,
        int maxIterations = AnimationConstants.IkDefaultMaxIterations,
        float precision = AnimationConstants.IkDefaultPrecision)
    {
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(frame);
        if (chainBoneIndices.Length < 2 || weight <= 0.0f)
        {
            return;
        }

        float w = FastMath.Clamp01(weight);
        Span<Vector3> worldT = stackalloc Vector3[rig.BoneCount];
        Span<Quaternion> worldR = stackalloc Quaternion[rig.BoneCount];
        Span<Vector3> worldS = stackalloc Vector3[rig.BoneCount];

        Span<Quaternion> localR = frame.GetRotations();
        int tipBone = chainBoneIndices[chainBoneIndices.Length - 1];
        float precisionSq = MathF.Max(precision, AnimationConstants.IkPrecisionFloor);
        precisionSq *= precisionSq;

        int iterations = 0;
        for (int iter = 0; iter < maxIterations; iter++)
        {
            iterations++;
            BlendingKernels.ForwardKinematics(rig, frame, worldT, worldR, worldS);
            if (Vector3.DistanceSquared(worldT[tipBone], targetPos) <= precisionSq)
            {
                break;
            }

            for (int i = chainBoneIndices.Length - 2; i >= 0; i--)
            {
                int bone = chainBoneIndices[i];
                BlendingKernels.ForwardKinematics(rig, frame, worldT, worldR, worldS);

                Vector3 toTip = worldT[tipBone] - worldT[bone];
                Vector3 toTarget = targetPos - worldT[bone];
                if (toTip.LengthSquared() < AnimationConstants.SoaEpsilon
                    || toTarget.LengthSquared() < AnimationConstants.SoaEpsilon)
                {
                    continue;
                }

                Quaternion deltaWorld = FastMath.QuaternionFromTo(toTip, toTarget);
                int parent = rig.Parents[bone];
                Quaternion parentWorld = parent != -1 ? worldR[parent] : Quaternion.Identity;

                Quaternion localDelta = FastMath.Multiply(FastMath.Multiply(parentWorld, deltaWorld), FastMath.Invert(parentWorld));
                Quaternion targetLocal = FastMath.Normalize(FastMath.Multiply(localDelta, localR[bone]));
                localR[bone] = FastMath.Slerp(localR[bone], targetLocal, w);
            }
        }

        AnimationTelemetry.RecordIkIterations(iterations);
    }
}
