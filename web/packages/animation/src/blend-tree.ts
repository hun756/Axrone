export { AnimationScratchPool, BlendScratchContext, type AnimationMotionEvaluationContext } from './blend-scratch';
export { compileMotion } from './blend-compile';
export { evaluateMotion, resolveMotionTime } from './blend-evaluate';
export { resolveMotionDuration } from './blend-duration';
export { collectMotionEvents } from './blend-events';
export { collectMotionClipActivities } from './blend-activities';
export { extractMotionRootDelta } from './blend-root-delta';
export { dispatchMotion, createVisitor, foldMotion } from './blend-visitor';
export type {
    AnimationCompiledMotion,
    AnimationCompiledClipMotion,
    AnimationCompiledBlend1DMotion,
    AnimationCompiledBlend2DMotion,
    AnimationCompiledDirectMotion,
    AnimationCompiledAdditiveMotion,
    AnimationCompiledBlend1DChild,
    AnimationCompiledBlend2DChild,
    AnimationCompiledDirectChild,
    BlendMotionKind,
    BlendMotionOfKind,
    BlendMotionVisitor,
    BlendWeight,
    NormalizedTime,
    DiagnosticCode,
} from './blend-types';
export {
    asBlendWeight,
    asNormalizedTime,
    unblendWeight,
    unnormalizedTime,
    MAX_BLEND_DEPTH,
    BLEND_EPSILON,
    BLEND_DISTANCE_EPSILON_SQ,
    SLERP_LINEAR_THRESHOLD,
    BLEND1D_LINEAR_SCAN_LIMIT,
} from './blend-types';
