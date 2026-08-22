import type { AnimationParameterStore } from './parameters';
import type {
    AnimationCompiledMotion,
    AnimationCompiledClipMotion,
    AnimationCompiledBlend1DMotion,
    AnimationCompiledBlend2DMotion,
    AnimationCompiledDirectMotion,
    AnimationCompiledAdditiveMotion,
} from './blend-types';
import { BLEND_EPSILON } from './blend-types';
import { assertNever } from './errors';
import { AnimationFrame } from './pose-frame';
import { blendFrame as blendFrameOp, blendWeightedFrames as blendWeightedFramesOp, applyAdditiveFrame as applyAdditiveFrameOp } from './pose-blend';
import type { AnimationMotionEvaluationContext } from './blend-scratch';

const resolveParameterScalar = (parameters: AnimationParameterStore, name: string): number => {
    const value = parameters.get(name);
    return typeof value === 'number' ? value : value ? 1 : 0;
};

const resolveDirectChildWeight = (
    parameters: AnimationParameterStore,
    parameter: string | undefined,
    weight: number
): number => {
    if (!parameter) {
        return Math.max(0, weight);
    }
    const value = parameters.get(parameter);
    return typeof value === 'number' ? Math.max(0, value * weight) : value ? Math.max(0, weight) : 0;
};

const resolveAdditiveWeight = (
    parameters: AnimationParameterStore,
    parameter: string | undefined,
    weight: number
): number => {
    if (!parameter) {
        return weight;
    }
    const value = parameters.get(parameter);
    return typeof value === 'number' ? value : value ? weight : 0;
};

export const resolveMotionTime = (
    normalizedTime: number,
    duration: number,
    cycleOffset: number,
    loop: boolean
): number => {
    if (duration <= 0) {
        return 0;
    }
    const offsetTime = normalizedTime + cycleOffset;
    if (!loop) {
        const normalized = Math.max(0, Math.min(1, offsetTime));
        return normalized * duration;
    }
    const wrapped = offsetTime % 1;
    const normalized = wrapped < 0 ? wrapped + 1 : wrapped;
    return normalized * duration;
};

const findBlend1DSegment = (
    children: readonly { readonly threshold: number }[],
    input: number
): number => {
    if (children.length === 1 || input <= children[0]!.threshold) {
        return -1;
    }
    for (let index = 0; index < children.length - 1; index += 1) {
        if (input > children[index + 1]!.threshold) {
            continue;
        }
        return index;
    }
    return -children.length;
};

const resolveBlend1DAlpha = (
    children: readonly { readonly threshold: number }[],
    leftIndex: number,
    input: number
): number => {
    const left = children[leftIndex]!;
    const right = children[leftIndex + 1]!;
    return (input - left.threshold) / Math.max(BLEND_EPSILON, right.threshold - left.threshold);
};

const resolveBlend2DWeights = (
    x: number,
    y: number,
    children: readonly { readonly x: number; readonly y: number }[],
    context: AnimationMotionEvaluationContext,
    depth: number
): number[] => {
    const weights = context.blendScratch.acquireBlend2DWeights(depth, children.length);
    let total = 0;
    for (let index = 0; index < children.length; index += 1) {
        const child = children[index]!;
        const dx = x - child.x;
        const dy = y - child.y;
        const distanceSquared = dx * dx + dy * dy;
        if (distanceSquared <= 1e-12) {
            for (let fill = 0; fill < children.length; fill += 1) {
                weights[fill] = 0;
            }
            weights[index] = 1;
            return weights;
        }
        const inverseDistance = 1 / Math.sqrt(distanceSquared);
        weights[index] = inverseDistance;
        total += inverseDistance;
    }
    if (total <= 0) {
        const uniform = 1 / Math.max(1, children.length);
        for (let index = 0; index < children.length; index += 1) {
            weights[index] = uniform;
        }
        return weights;
    }
    for (let index = 0; index < children.length; index += 1) {
        weights[index] /= total;
    }
    return weights;
};

function evaluateClipMotion(
    motion: AnimationCompiledClipMotion,
    normalizedTime: number,
    context: AnimationMotionEvaluationContext,
    out: AnimationFrame,
    loop: boolean
): AnimationFrame {
    out.reset(context.rig, context.restFrame.curves.values);
    const time = resolveMotionTime(normalizedTime * motion.timeScale, motion.clip.duration, motion.cycleOffset, loop);
    return motion.clip.sampleTime(time, out);
}

function evaluateBlend1DMotion(
    motion: AnimationCompiledBlend1DMotion,
    normalizedTime: number,
    context: AnimationMotionEvaluationContext,
    out: AnimationFrame,
    loop: boolean,
    depth: number
): AnimationFrame {
    const input = resolveParameterScalar(context.parameters, motion.parameter);
    const segment = findBlend1DSegment(motion.children, input);
    if (segment < 0) {
        return evaluateMotion(motion.children[-segment - 1]!.motion, normalizedTime, context, out, loop, depth + 1);
    }
    const alpha = resolveBlend1DAlpha(motion.children, segment, input);
    const leftFrame = context.scratch.acquire();
    const rightFrame = context.scratch.acquire();
    evaluateMotion(motion.children[segment]!.motion, normalizedTime, context, leftFrame, loop, depth + 1);
    evaluateMotion(motion.children[segment + 1]!.motion, normalizedTime, context, rightFrame, loop, depth + 1);
    return blendFrameOp(out, leftFrame, rightFrame, alpha);
}

function evaluateBlend2DMotion(
    motion: AnimationCompiledBlend2DMotion,
    normalizedTime: number,
    context: AnimationMotionEvaluationContext,
    out: AnimationFrame,
    loop: boolean,
    depth: number
): AnimationFrame {
    const weights = resolveBlend2DWeights(
        resolveParameterScalar(context.parameters, motion.parameterX),
        resolveParameterScalar(context.parameters, motion.parameterY),
        motion.children,
        context,
        depth
    );
    const frames = context.scratch.acquireFrameArray(motion.children.length);
    for (let index = 0; index < motion.children.length; index += 1) {
        const frame = context.scratch.acquire();
        evaluateMotion(motion.children[index]!.motion, normalizedTime, context, frame, loop, depth + 1);
        frames[index] = frame;
    }
    return blendWeightedFramesOp(out, frames, weights, context.restFrame, context.blendScratch.referenceRotation);
}

function evaluateDirectMotion(
    motion: AnimationCompiledDirectMotion,
    normalizedTime: number,
    context: AnimationMotionEvaluationContext,
    out: AnimationFrame,
    loop: boolean,
    depth: number
): AnimationFrame {
    const frames = context.scratch.acquireFrameArray(motion.children.length);
    const weights = context.scratch.acquireWeightArray(motion.children.length);
    let activeCount = 0;
    for (let index = 0; index < motion.children.length; index += 1) {
        const child = motion.children[index]!;
        const weight = resolveDirectChildWeight(context.parameters, child.parameter, child.weight);
        if (weight <= 0) {
            continue;
        }
        const frame = context.scratch.acquire();
        evaluateMotion(child.motion, normalizedTime, context, frame, loop, depth + 1);
        frames[activeCount] = frame;
        weights[activeCount] = weight;
        activeCount += 1;
    }
    if (activeCount === 0) {
        return out.copyFrom(context.restFrame);
    }
    if (activeCount === 1) {
        return out.copyFrom(frames[0]!);
    }
    frames.length = activeCount;
    weights.length = activeCount;
    return blendWeightedFramesOp(
        out,
        frames,
        weights,
        context.restFrame,
        context.blendScratch.referenceRotation
    );
}

function evaluateAdditiveMotion(
    motion: AnimationCompiledAdditiveMotion,
    normalizedTime: number,
    context: AnimationMotionEvaluationContext,
    out: AnimationFrame,
    loop: boolean,
    depth: number
): AnimationFrame {
    const baseFrame = context.scratch.acquire();
    const additiveFrame = context.scratch.acquire();
    evaluateMotion(motion.base, normalizedTime, context, baseFrame, loop, depth + 1);
    evaluateMotion(motion.additive, normalizedTime, context, additiveFrame, loop, depth + 1);
    const resolvedWeight = resolveAdditiveWeight(context.parameters, motion.parameter, motion.weight);
    return applyAdditiveFrameOp(out, baseFrame, additiveFrame, context.restFrame, resolvedWeight, context.blendScratch.additiveScratch);
}

export const evaluateMotion = (
    motion: AnimationCompiledMotion,
    normalizedTime: number,
    context: AnimationMotionEvaluationContext,
    out: AnimationFrame,
    loop: boolean = true,
    depth: number = 0
): AnimationFrame => {
    switch (motion.kind) {
        case 'clip': return evaluateClipMotion(motion, normalizedTime, context, out, loop);
        case 'blend1d': return evaluateBlend1DMotion(motion, normalizedTime, context, out, loop, depth);
        case 'blend2d': return evaluateBlend2DMotion(motion, normalizedTime, context, out, loop, depth);
        case 'direct': return evaluateDirectMotion(motion, normalizedTime, context, out, loop, depth);
        case 'additive': return evaluateAdditiveMotion(motion, normalizedTime, context, out, loop, depth);
        default: return assertNever(motion, 'Unsupported motion kind');
    }
};
