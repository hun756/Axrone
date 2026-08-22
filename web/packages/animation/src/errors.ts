export class AnimationError extends Error {
    constructor(
        message: string,
        readonly code: string,
        readonly cause?: unknown
    ) {
        super(message);
        this.name = 'AnimationError';
        Object.setPrototypeOf(this, new.target.prototype);
        (
            Error as typeof Error & { captureStackTrace?: (target: object, ctor: Function) => void }
        ).captureStackTrace?.(this, this.constructor);
    }
}

export class AnimationValidationError extends AnimationError {
    constructor(message: string, cause?: unknown) {
        super(message, 'ANIMATION_VALIDATION_ERROR', cause);
        this.name = 'AnimationValidationError';
    }
}

export class AnimationCompilationError extends AnimationError {
    constructor(
        message: string,
        readonly diagnosticCode: import('./blend-types').DiagnosticCode,
        readonly motionPath: string = '',
        cause?: unknown
    ) {
        super(message, diagnosticCode, cause);
        this.name = 'AnimationCompilationError';
    }
}

export class AnimationResolutionError extends AnimationError {
    constructor(message: string, readonly clipId: string, cause?: unknown) {
        super(message, 'animation.clip.unknown', cause);
        this.name = 'AnimationResolutionError';
    }
}

export class AnimationEvaluationError extends AnimationError {
    constructor(message: string, readonly motionKind: import('./blend-types').BlendMotionKind, cause?: unknown) {
        super(message, 'animation.kind.invalid', cause);
        this.name = 'AnimationEvaluationError';
    }
}

export class AnimationSamplingError extends AnimationError {
    constructor(message: string, cause?: unknown) {
        super(message, 'ANIMATION_SAMPLING_ERROR', cause);
        this.name = 'AnimationSamplingError';
    }
}

export class AnimationStateMachineError extends AnimationError {
    constructor(message: string, cause?: unknown) {
        super(message, 'ANIMATION_STATE_MACHINE_ERROR', cause);
        this.name = 'AnimationStateMachineError';
    }
}

export class AnimationRetargetingError extends AnimationError {
    constructor(message: string, cause?: unknown) {
        super(message, 'ANIMATION_RETARGETING_ERROR', cause);
        this.name = 'AnimationRetargetingError';
    }
}

export class AnimationIkError extends AnimationError {
    constructor(message: string, cause?: unknown) {
        super(message, 'ANIMATION_IK_ERROR', cause);
        this.name = 'AnimationIkError';
    }
}

export const assertNever = (value: never, message: string): never => {
    throw new AnimationError(`${message} '${String(value)}'`, 'ANIMATION_EXHAUSTIVENESS_ERROR');
};