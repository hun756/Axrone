namespace Axrone.Animation;

/// <summary>Machine-readable animation failure codes, grouped by subsystem.</summary>
public enum AnimationErrorCode
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary>Rig has no bones.</summary>
    ValidationRigEmptyBones = 1,

    /// <summary>Duplicate bone name.</summary>
    ValidationRigDuplicateBoneName = 2,

    /// <summary>Bone parent index invalid.</summary>
    ValidationRigInvalidParent = 3,

    /// <summary>Bone hierarchy contains a cycle.</summary>
    ValidationRigCycleDetected = 4,

    /// <summary>Channel times/values length mismatch.</summary>
    ValidationClipMismatch = 5,

    /// <summary>Duplicate track binding.</summary>
    ValidationClipDuplicateTrack = 6,

    /// <summary>Degenerate channel data.</summary>
    ValidationClipDegenerateData = 7,

    /// <summary>Controller has no layers.</summary>
    ValidationControllerEmpty = 8,

    /// <summary>Motion references an unknown clip.</summary>
    CompilationUnknownClip = 9,

    /// <summary>Blend node has no children.</summary>
    CompilationEmptyChildren = 10,

    /// <summary>Motion graph contains a cycle.</summary>
    CompilationCyclicState = 11,

    /// <summary>Bone name does not resolve.</summary>
    ResolutionBoneNotFound = 12,

    /// <summary>Clip id does not resolve.</summary>
    ResolutionClipNotFound = 13,

    /// <summary>Unknown motion kind.</summary>
    EvaluationMotionKindInvalid = 14,

    /// <summary>Blend recursion exceeded maximum depth.</summary>
    EvaluationDepthOverflow = 15,

    /// <summary>Sample position out of bounds.</summary>
    SamplingOutOfBounds = 16,

    /// <summary>State machine parameter missing.</summary>
    StateMachineParameterNotFound = 17,

    /// <summary>Parameter type mismatch.</summary>
    StateMachineTypeMismatch = 18,

    /// <summary>Invalid transition.</summary>
    StateMachineInvalidTransition = 19,

    /// <summary>Rigs are incompatible for retargeting.</summary>
    RetargetingIncompatibleLayout = 20,

    /// <summary>No bone mapping resolved.</summary>
    RetargetingNoMapping = 21,

    /// <summary>IK target not found.</summary>
    IkTargetNotFound = 22,

    /// <summary>IK target unreachable.</summary>
    IkUnreachable = 23,

    /// <summary>Streaming chunk payload corrupt.</summary>
    StreamingChunkCorrupt = 24,

    /// <summary>Streaming chunk incompatible with clip.</summary>
    StreamingChunkIncompatible = 25,
}

/// <summary>Base animation failure carrying a machine-readable code.</summary>
public class AnimationException : Exception
{
    /// <summary>Failure code.</summary>
    public AnimationErrorCode Code { get; }

    /// <summary>Creates a failure.</summary>
    public AnimationException(AnimationErrorCode code, string message)
        : base($"[{code}] {message}") => Code = code;
}

/// <summary>Rig, clip, or controller definition rejected.</summary>
public sealed class ValidationException : AnimationException
{
    /// <summary>Creates a validation failure.</summary>
    public ValidationException(AnimationErrorCode code, string message)
        : base(code, message)
    {
    }
}

/// <summary>Motion graph or controller build rejected, with the offending path.</summary>
public sealed class CompilationException : AnimationException
{
    /// <summary>Motion path that failed, when known.</summary>
    public string? MotionPath { get; }

    /// <summary>Creates a compilation failure.</summary>
    public CompilationException(AnimationErrorCode code, string message, string? motionPath = null)
        : base(code, motionPath != null ? $"{message} (Path: {motionPath})" : message) => MotionPath = motionPath;
}

/// <summary>Named bone, clip, or parameter did not resolve.</summary>
public sealed class ResolutionException : AnimationException
{
    /// <summary>Creates a resolution failure.</summary>
    public ResolutionException(AnimationErrorCode code, string message)
        : base(code, message)
    {
    }
}
