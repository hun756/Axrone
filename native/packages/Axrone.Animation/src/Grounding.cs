namespace Axrone.Animation;

/// <summary>Planar grounding: weighted root-height offset from active foot contacts.</summary>
public static class GroundingSystem
{
    /// <summary>Computes the root Y offset that plants the weighted contacts on the ground.</summary>
    public static float ComputeRootYOffset(ReadOnlySpan<FootContact> contacts, ReadOnlySpan<float> contactHeights, float targetGroundHeight, float time)
    {
        float totalWeight = 0.0f;
        float totalOffset = 0.0f;

        for (int i = 0; i < contacts.Length; i++)
        {
            float weight = contacts[i].EvaluateWeight(time);
            if (weight > 0.0f)
            {
                float boneHeight = i < contactHeights.Length ? contactHeights[i] : 0.0f;
                totalOffset += (targetGroundHeight - boneHeight) * weight;
                totalWeight += weight;
            }
        }

        return totalWeight > AnimationConstants.SoaEpsilon ? totalOffset / totalWeight : 0.0f;
    }
}
