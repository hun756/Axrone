namespace Axrone.Animation;

/// <summary>Shared numeric tolerances and bounds for the animation runtime.</summary>
public static class AnimationConstants
{
    /// <summary>General float epsilon for span and time comparisons.</summary>
    public const float SoaEpsilon = 1e-6f;

    /// <summary>Slerp dot threshold above which linear blending takes over.</summary>
    public const float SlerpLinearThreshold = 0.9995f;

    /// <summary>Blend weight floor; totals below this fall back to rest.</summary>
    public const float BlendEpsilon = 1e-6f;

    /// <summary>Squared-distance epsilon for blend-space exact hits.</summary>
    public const float BlendDistanceEpsilonSq = 1e-12f;

    /// <summary>Maximum blend-tree recursion depth.</summary>
    public const int MaxBlendDepth = 16;

    /// <summary>Blend1D child count at or below which linear scan beats binary search.</summary>
    public const int Blend1DLinearScanLimit = 8;

    /// <summary>IK precision lower bound.</summary>
    public const float IkPrecisionFloor = 1e-5f;

    /// <summary>Default IK precision.</summary>
    public const float IkDefaultPrecision = 1e-3f;

    /// <summary>Default IK iteration cap.</summary>
    public const int IkDefaultMaxIterations = 12;

    /// <summary>Motion-matching facing penalty for degenerate directions.</summary>
    public const float MissingFacingPenalty = 5.0f;

    /// <summary>Foot-contact weight floor inside the contact window.</summary>
    public const float FootWeightFloor = 0.25f;

    /// <summary>Keyframe reduction tolerance for positions.</summary>
    public const float KeyframePosTol = 1e-4f;

    /// <summary>Keyframe reduction tolerance for rotations (radians).</summary>
    public const float KeyframeRotTol = 0.004363323f;

    /// <summary>Keyframe reduction tolerance for scales.</summary>
    public const float KeyframeScaleTol = 1e-4f;

    /// <summary>Keyframe reduction tolerance for curves.</summary>
    public const float KeyframeCurveTol = 1e-4f;

    /// <summary>
    /// Bone count at or below which world-transform scratch lives on the stack
    /// (~10KB for the T/R/S triple). Larger rigs rent from the pool instead of
    /// risking a stack overflow.
    /// </summary>
    public const int MaxStackScratchBones = 256;

    /// <summary>
    /// Blend child count at or below which evaluation scratch lives on the stack.
    /// Larger fan-outs rent from the pool.
    /// </summary>
    public const int MaxStackScratchFrames = 64;
}
