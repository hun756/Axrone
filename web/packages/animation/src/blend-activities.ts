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
import { resolveMotionTime } from './blend-evaluate';
import type { AnimationControllerClipActivity } from './types';

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
    weights: number[]
): void => {
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
            return;
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
        return;
    }
    for (let index = 0; index < children.length; index += 1) {
        weights[index] /= total;
    }
};

function collectClipActivity(
    motion: AnimationCompiledClipMotion,
    normalizedTime: number,
    loop: boolean,
    resolvedLayerWeight: number,
    resolvedMotionWeight: number,
    layerId: string,
    stateId: string,
    out: AnimationControllerClipActivity[]
): void {
    const time = resolveMotionTime(normalizedTime * motion.timeScale, motion.clip.duration, motion.cycleOffset, loop);
    out.push({ clipId: motion.clip.id, layerId, stateId, layerWeight: resolvedLayerWeight, motionWeight: resolvedMotionWeight, loop, time, normalizedTime: motion.clip.duration > 0 ? time / motion.clip.duration : 0 } satisfies AnimationControllerClipActivity);
}

function collectBlend1DActivity(
    motion: AnimationCompiledBlend1DMotion,
    normalizedTime: number,
    loop: boolean,
    parameters: AnimationParameterStore,
    resolvedLayerWeight: number,
    resolvedMotionWeight: number,
    layerId: string,
    stateId: string,
    out: AnimationControllerClipActivity[],
    depth: number
): void {
    const input = resolveParameterScalar(parameters, motion.parameter);
    const segment = findBlend1DSegment(motion.children, input);
    if (segment < 0) {
        collectMotionClipActivities(motion.children[-segment - 1]!.motion, normalizedTime, loop, parameters, layerId, stateId, resolvedLayerWeight, resolvedMotionWeight, out, depth + 1);
        return;
    }
    const alpha = resolveBlend1DAlpha(motion.children, segment, input);
    collectMotionClipActivities(motion.children[segment]!.motion, normalizedTime, loop, parameters, layerId, stateId, resolvedLayerWeight, resolvedMotionWeight * (1 - alpha), out, depth + 1);
    collectMotionClipActivities(motion.children[segment + 1]!.motion, normalizedTime, loop, parameters, layerId, stateId, resolvedLayerWeight, resolvedMotionWeight * alpha, out, depth + 1);
}

function collectBlend2DActivity(
    motion: AnimationCompiledBlend2DMotion,
    normalizedTime: number,
    loop: boolean,
    parameters: AnimationParameterStore,
    resolvedLayerWeight: number,
    resolvedMotionWeight: number,
    layerId: string,
    stateId: string,
    out: AnimationControllerClipActivity[],
    depth: number,
    weights: number[]
): void {
    resolveBlend2DWeights(resolveParameterScalar(parameters, motion.parameterX), resolveParameterScalar(parameters, motion.parameterY), motion.children, weights);
    for (let index = 0; index < motion.children.length; index += 1) {
        const childWeight = weights[index] ?? 0;
        if (childWeight <= 0) continue;
        collectMotionClipActivities(motion.children[index]!.motion, normalizedTime, loop, parameters, layerId, stateId, resolvedLayerWeight, resolvedMotionWeight * childWeight, out, depth + 1);
    }
}

function collectDirectActivity(
    motion: AnimationCompiledDirectMotion,
    normalizedTime: number,
    loop: boolean,
    parameters: AnimationParameterStore,
    resolvedLayerWeight: number,
    resolvedMotionWeight: number,
    layerId: string,
    stateId: string,
    out: AnimationControllerClipActivity[],
    depth: number
): void {
    for (let index = 0; index < motion.children.length; index += 1) {
        const child = motion.children[index]!;
        const childWeight = resolveDirectChildWeight(parameters, child.parameter, child.weight);
        if (childWeight <= 0) continue;
        collectMotionClipActivities(child.motion, normalizedTime, loop, parameters, layerId, stateId, resolvedLayerWeight, resolvedMotionWeight * childWeight, out, depth + 1);
    }
}

function collectAdditiveActivity(
    motion: AnimationCompiledAdditiveMotion,
    normalizedTime: number,
    loop: boolean,
    parameters: AnimationParameterStore,
    resolvedLayerWeight: number,
    resolvedMotionWeight: number,
    layerId: string,
    stateId: string,
    out: AnimationControllerClipActivity[],
    depth: number
): void {
    collectMotionClipActivities(motion.base, normalizedTime, loop, parameters, layerId, stateId, resolvedLayerWeight, resolvedMotionWeight, out, depth + 1);
    const additiveWeight = resolveAdditiveWeight(parameters, motion.parameter, motion.weight);
    if (additiveWeight > 0) {
        collectMotionClipActivities(motion.additive, normalizedTime, loop, parameters, layerId, stateId, resolvedLayerWeight, resolvedMotionWeight * additiveWeight, out, depth + 1);
    }
}

const _activityWeights: number[] = [];

export const collectMotionClipActivities = (
    motion: AnimationCompiledMotion,
    normalizedTime: number,
    loop: boolean,
    parameters: AnimationParameterStore,
    layerId: string,
    stateId: string,
    layerWeight: number,
    motionWeight: number,
    out: AnimationControllerClipActivity[] = [],
    depth: number = 0
): readonly AnimationControllerClipActivity[] => {
    const resolvedLayerWeight = Math.max(0, Math.min(1, layerWeight));
    const resolvedMotionWeight = Math.max(0, motionWeight);
    if (resolvedLayerWeight <= 0 || resolvedMotionWeight <= 0) {
        return out;
    }
    switch (motion.kind) {
        case 'clip': collectClipActivity(motion, normalizedTime, loop, resolvedLayerWeight, resolvedMotionWeight, layerId, stateId, out); break;
        case 'blend1d': collectBlend1DActivity(motion, normalizedTime, loop, parameters, resolvedLayerWeight, resolvedMotionWeight, layerId, stateId, out, depth); break;
        case 'blend2d': collectBlend2DActivity(motion, normalizedTime, loop, parameters, resolvedLayerWeight, resolvedMotionWeight, layerId, stateId, out, depth, _activityWeights); break;
        case 'direct': collectDirectActivity(motion, normalizedTime, loop, parameters, resolvedLayerWeight, resolvedMotionWeight, layerId, stateId, out, depth); break;
        case 'additive': collectAdditiveActivity(motion, normalizedTime, loop, parameters, resolvedLayerWeight, resolvedMotionWeight, layerId, stateId, out, depth); break;
        default: assertNever(motion, 'Unsupported motion kind');
    }
    return out;
};
