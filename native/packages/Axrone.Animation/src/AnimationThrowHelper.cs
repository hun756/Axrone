namespace Axrone.Animation;

/// <summary>
/// Cold-path exception factory: keeps throw sites out of hot methods.
/// StackTraceHidden keeps helper frames out of stack traces so logs point
/// at the real fault site, not the factory.
/// </summary>
public static class AnimationThrowHelper
{
    /// <summary>Throws a validation failure.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowValidation(AnimationErrorCode code, string message) =>
        throw new ValidationException(code, message);

    /// <summary>Throws a compilation failure.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowCompilation(AnimationErrorCode code, string message, string? path = null) =>
        throw new CompilationException(code, message, path);

    /// <summary>Throws a resolution failure.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowResolution(AnimationErrorCode code, string message) =>
        throw new ResolutionException(code, message);

    /// <summary>Throws an evaluation failure.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowEvaluation(AnimationErrorCode code, string message) =>
        throw new EvaluationException(code, message);

    /// <summary>Throws a state machine failure.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowStateMachine(AnimationErrorCode code, string message) =>
        throw new StateMachineException(code, message);

    /// <summary>Throws a retargeting failure.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowRetargeting(AnimationErrorCode code, string message) =>
        throw new RetargetingException(code, message);

    /// <summary>Throws an IK failure.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowIk(AnimationErrorCode code, string message) =>
        throw new IkException(code, message);

    /// <summary>Throws a sampling failure.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowSampling(AnimationErrorCode code, string message) =>
        throw new SamplingException(code, message);

    /// <summary>Throws when a curve id resolves to no channel slot.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowCurveNotFound(CurveId curve) =>
        throw new ResolutionException(AnimationErrorCode.ResolutionCurveNotFound, $"Curve '{curve}' missing.");

    /// <summary>Throws a streaming failure.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowStreaming(AnimationErrorCode code, string message) =>
        throw new StreamingException(code, message);

    /// <summary>Throws for unreachable branches.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowUnreachable() =>
        throw new UnreachableException();

    /// <summary>Throws when a rig has no bones.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowRigEmptyBones() =>
        throw new ValidationException(AnimationErrorCode.ValidationRigEmptyBones, "Rig requires at least one bone.");

    /// <summary>Throws when a bone name is empty.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowRigEmptyBoneName(int index) =>
        throw new ValidationException(AnimationErrorCode.ValidationRigDuplicateBoneName, $"Bone {index} has empty name.");

    /// <summary>Throws on a duplicate bone name.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowRigDuplicateBoneName(string name) =>
        throw new ValidationException(AnimationErrorCode.ValidationRigDuplicateBoneName, $"Duplicate bone name '{name}'.");

    /// <summary>Throws on an invalid parent index.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowRigInvalidParent(int index, string name, int parentIndex) =>
        throw new ValidationException(AnimationErrorCode.ValidationRigInvalidParent, $"Bone {index} ('{name}') invalid parent {parentIndex}.");

    /// <summary>Throws when hierarchy traversal finds a cycle.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowRigCycleDetected(string boneName) =>
        throw new ValidationException(AnimationErrorCode.ValidationRigCycleDetected, $"Cycle detected at bone '{boneName}'.");

    /// <summary>Throws when a bone tree connects to no root.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowRigUnconnectedBone(string boneName) =>
        throw new ValidationException(AnimationErrorCode.ValidationRigInvalidParent, $"Unconnected bone tree detected: '{boneName}'.");

    /// <summary>Throws when a matrix palette buffer is undersized.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowPaletteTooSmall(int actual, int required) =>
        throw new ValidationException(AnimationErrorCode.SamplingOutOfBounds, $"Output matrix palette too small: required {required}, got {actual}.");
}
