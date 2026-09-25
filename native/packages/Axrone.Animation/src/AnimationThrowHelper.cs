namespace Axrone.Animation;

/// <summary>Cold-path exception factory: keeps throw sites out of hot methods.</summary>
public static class AnimationThrowHelper
{
    /// <summary>Throws a validation failure.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowValidation(AnimationErrorCode code, string message) =>
        throw new ValidationException(code, message);

    /// <summary>Throws a compilation failure.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowCompilation(AnimationErrorCode code, string message, string? path = null) =>
        throw new CompilationException(code, message, path);

    /// <summary>Throws a resolution failure.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowResolution(AnimationErrorCode code, string message) =>
        throw new ResolutionException(code, message);

    /// <summary>Throws when a curve id resolves to no channel slot.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowCurveNotFound(CurveId curve) =>
        throw new ResolutionException(AnimationErrorCode.ResolutionCurveNotFound, $"Curve '{curve}' missing.");

    /// <summary>Throws an evaluation failure.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowEvaluation(AnimationErrorCode code, string message) =>
        throw new EvaluationException(code, message);

    /// <summary>Throws a state machine failure.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowStateMachine(AnimationErrorCode code, string message) =>
        throw new StateMachineException(code, message);

    /// <summary>Throws a retargeting failure.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowRetargeting(AnimationErrorCode code, string message) =>
        throw new RetargetingException(code, message);

    /// <summary>Throws an IK failure.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowIk(AnimationErrorCode code, string message) =>
        throw new IkException(code, message);

    /// <summary>Throws a sampling failure.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowSampling(AnimationErrorCode code, string message) =>
        throw new SamplingException(code, message);

    /// <summary>Throws a streaming failure.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowStreaming(AnimationErrorCode code, string message) =>
        throw new StreamingException(code, message);

    /// <summary>Throws for unreachable branches.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowUnreachable() =>
        throw new UnreachableException();
}
