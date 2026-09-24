namespace Axrone.Animation;

/// <summary>Motion-matching query: desired trajectory and facing with tag filters.</summary>
public readonly record struct MotionMatchQuery(
    Vector3 TrajectoryPos,
    Vector3 FacingDirection,
    HashSet<string>? RequiredTags = null,
    HashSet<string>? ExcludedTags = null,
    float ContinuityBias = 0.0f);

/// <summary>One searchable pose sample.</summary>
public readonly record struct MotionMatchCandidate(
    int ClipIndex,
    float Time,
    Vector3 TrajectoryPos,
    Vector3 FacingDirection,
    HashSet<string>? Tags,
    float CostBias = 0.0f);

/// <summary>
/// Linear motion-matching search: tag pre-filter, squared trajectory distance,
/// facing alignment cost, bias adjustments. Returns the best index or -1.
/// </summary>
public static class MotionMatching
{
    /// <summary>Searches candidates; reports the best score alongside the index.</summary>
    public static int Search(in MotionMatchQuery query, ReadOnlySpan<MotionMatchCandidate> candidates, out float bestScore)
    {
        int bestIndex = -1;
        bestScore = float.MaxValue;

        for (int i = 0; i < candidates.Length; i++)
        {
            MotionMatchCandidate candidate = candidates[i];

            if (query.RequiredTags != null && query.RequiredTags.Count > 0)
            {
                if (candidate.Tags == null || !query.RequiredTags.IsSubsetOf(candidate.Tags))
                {
                    continue;
                }
            }

            if (query.ExcludedTags != null && query.ExcludedTags.Count > 0 && candidate.Tags != null)
            {
                if (candidate.Tags.Overlaps(query.ExcludedTags))
                {
                    continue;
                }
            }

            float trajectoryDistSq = Vector3.DistanceSquared(query.TrajectoryPos, candidate.TrajectoryPos);

            float facingScore;
            float queryLen = query.FacingDirection.Length();
            float candidateLen = candidate.FacingDirection.Length();
            if (queryLen <= AnimationConstants.SoaEpsilon || candidateLen <= AnimationConstants.SoaEpsilon)
            {
                facingScore = AnimationConstants.MissingFacingPenalty;
            }
            else
            {
                float dot = Vector3.Dot(query.FacingDirection / queryLen, candidate.FacingDirection / candidateLen);
                facingScore = 1.0f - dot;
            }

            float score = candidate.CostBias + trajectoryDistSq + facingScore - query.ContinuityBias;
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }
}
