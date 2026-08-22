import type { AnimationParameterStore } from './parameters';
import type { AnimationRig } from './rig';
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
import { quatAccumulateWeighted, quatFinalizeWeighted, quatIdentity, quatMultiply, quatNormalize, quatSlerp } from './math';
import { resolveMotionTime } from './blend-evaluate';
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

function extractClipRootDelta(
    motion: AnimationCompiledClipMotion,
    prevTime: number,
    currTime: number,
    loop: boolean,
    rootBoneIndex: number,
    rig: AnimationRig,
    outTranslation: Float32Array,
    outRotation: Float32Array
): void {
    motion.clip.extractBoneDelta(
        rootBoneIndex,
        resolveMotionTime(prevTime * motion.timeScale, motion.clip.duration, motion.cycleOffset, loop),
        resolveMotionTime(currTime * motion.timeScale, motion.clip.duration, motion.cycleOffset, loop),
        loop, rig, outTranslation, outRotation
    );
}

function extractBlend1DRootDelta(
    motion: AnimationCompiledBlend1DMotion,
    prevTime: number,
    currTime: number,
    loop: boolean,
    rootBoneIndex: number,
    rig: AnimationRig,
    parameters: AnimationParameterStore,
    outTranslation: Float32Array,
    outRotation: Float32Array,
    context: AnimationMotionEvaluationContext,
    depth: number
): void {
    const input = resolveParameterScalar(parameters, motion.parameter);
    const segment = findBlend1DSegment(motion.children, input);
    if (segment < 0) {
        extractMotionRootDelta(motion.children[-segment - 1]!.motion, prevTime, currTime, loop, rootBoneIndex, rig, parameters, outTranslation, outRotation, context, depth);
        return;
    }
    const alpha = resolveBlend1DAlpha(motion.children, segment, input);
    const leftScratch = context.blendScratch.acquireRootDeltaScratch(depth * 2);
    const rightScratch = context.blendScratch.acquireRootDeltaScratch(depth * 2 + 1);
    extractMotionRootDelta(motion.children[segment]!.motion, prevTime, currTime, loop, rootBoneIndex, rig, parameters, leftScratch.translation, leftScratch.rotation, context, depth + 1);
    extractMotionRootDelta(motion.children[segment + 1]!.motion, prevTime, currTime, loop, rootBoneIndex, rig, parameters, rightScratch.translation, rightScratch.rotation, context, depth + 1);
    outTranslation[0] = leftScratch.translation[0]! + (rightScratch.translation[0]! - leftScratch.translation[0]!) * alpha;
    outTranslation[1] = leftScratch.translation[1]! + (rightScratch.translation[1]! - leftScratch.translation[1]!) * alpha;
    outTranslation[2] = leftScratch.translation[2]! + (rightScratch.translation[2]! - leftScratch.translation[2]!) * alpha;
    quatSlerp(outRotation, 0, leftScratch.rotation, 0, rightScratch.rotation, 0, alpha);
}

function extractBlend2DRootDelta(
    motion: AnimationCompiledBlend2DMotion,
    prevTime: number,
    currTime: number,
    loop: boolean,
    rootBoneIndex: number,
    rig: AnimationRig,
    parameters: AnimationParameterStore,
    outTranslation: Float32Array,
    outRotation: Float32Array,
    context: AnimationMotionEvaluationContext,
    depth: number
): void {
    const weights = resolveBlend2DWeights(
        resolveParameterScalar(parameters, motion.parameterX),
        resolveParameterScalar(parameters, motion.parameterY),
        motion.children,
        context,
        depth
    );
    const childScratch = context.blendScratch.acquireRootDeltaScratch(depth * 2);
    const referenceScratch = context.blendScratch.acquireRootDeltaScratch(depth * 2 + 1);
    outTranslation.fill(0);
    let totalRotationWeight = 0;
    for (let index = 0; index < motion.children.length; index += 1) {
        extractMotionRootDelta(motion.children[index]!.motion, prevTime, currTime, loop, rootBoneIndex, rig, parameters, childScratch.translation, childScratch.rotation, context, depth + 1);
        const weight = Math.max(0, weights[index] ?? 0);
        outTranslation[0] += childScratch.translation[0]! * weights[index]!;
        outTranslation[1] += childScratch.translation[1]! * weights[index]!;
        outTranslation[2] += childScratch.translation[2]! * weights[index]!;
        if (weight > 0) {
            quatAccumulateWeighted(outRotation, 0, referenceScratch.rotation, 0, childScratch.rotation, 0, weight, totalRotationWeight <= 0);
            totalRotationWeight += weight;
        }
    }
    quatFinalizeWeighted(outRotation, 0, outRotation, 0, totalRotationWeight);
}

function extractDirectRootDelta(
    motion: AnimationCompiledDirectMotion,
    prevTime: number,
    currTime: number,
    loop: boolean,
    rootBoneIndex: number,
    rig: AnimationRig,
    parameters: AnimationParameterStore,
    outTranslation: Float32Array,
    outRotation: Float32Array,
    context: AnimationMotionEvaluationContext,
    depth: number
): void {
    const childScratch = context.blendScratch.acquireRootDeltaScratch(depth * 2);
    const referenceScratch = context.blendScratch.acquireRootDeltaScratch(depth * 2 + 1);
    outTranslation.fill(0);
    let totalWeight = 0;
    for (let index = 0; index < motion.children.length; index += 1) {
        const child = motion.children[index]!;
        const weight = resolveDirectChildWeight(parameters, child.parameter, child.weight);
        if (weight <= 0) continue;
        extractMotionRootDelta(child.motion, prevTime, currTime, loop, rootBoneIndex, rig, parameters, childScratch.translation, childScratch.rotation, context, depth + 1);
        outTranslation[0] += childScratch.translation[0]! * weight;
        outTranslation[1] += childScratch.translation[1]! * weight;
        outTranslation[2] += childScratch.translation[2]! * weight;
        quatAccumulateWeighted(outRotation, 0, referenceScratch.rotation, 0, childScratch.rotation, 0, weight, totalWeight <= 0);
        totalWeight += weight;
    }
    if (totalWeight > 0) {
        outTranslation[0] /= totalWeight;
        outTranslation[1] /= totalWeight;
        outTranslation[2] /= totalWeight;
    }
    quatFinalizeWeighted(outRotation, 0, outRotation, 0, totalWeight);
}

function extractAdditiveRootDelta(
    motion: AnimationCompiledAdditiveMotion,
    prevTime: number,
    currTime: number,
    loop: boolean,
    rootBoneIndex: number,
    rig: AnimationRig,
    parameters: AnimationParameterStore,
    outTranslation: Float32Array,
    outRotation: Float32Array,
    context: AnimationMotionEvaluationContext,
    depth: number
): void {
    const baseScratch = context.blendScratch.acquireRootDeltaScratch(depth * 2);
    const additiveScratch = context.blendScratch.acquireRootDeltaScratch(depth * 2 + 1);
    extractMotionRootDelta(motion.base, prevTime, currTime, loop, rootBoneIndex, rig, parameters, baseScratch.translation, baseScratch.rotation, context, depth + 1);
    extractMotionRootDelta(motion.additive, prevTime, currTime, loop, rootBoneIndex, rig, parameters, additiveScratch.translation, additiveScratch.rotation, context, depth + 1);
    const resolvedWeight = resolveAdditiveWeight(parameters, motion.parameter, motion.weight);
    outTranslation[0] = baseScratch.translation[0]! + additiveScratch.translation[0]! * resolvedWeight;
    outTranslation[1] = baseScratch.translation[1]! + additiveScratch.translation[1]! * resolvedWeight;
    outTranslation[2] = baseScratch.translation[2]! + additiveScratch.translation[2]! * resolvedWeight;
    quatIdentity(outRotation, 0);
    quatSlerp(outRotation, 0, outRotation, 0, additiveScratch.rotation, 0, resolvedWeight);
    quatMultiply(outRotation, 0, baseScratch.rotation, 0, outRotation, 0);
    quatNormalize(outRotation, 0, outRotation, 0);
}

export const extractMotionRootDelta = (
    motion: AnimationCompiledMotion,
    previousNormalizedTime: number,
    currentNormalizedTime: number,
    loop: boolean,
    rootBoneIndex: number,
    rig: AnimationRig,
    parameters: AnimationParameterStore,
    outTranslation: Float32Array,
    outRotation: Float32Array,
    context: AnimationMotionEvaluationContext,
    depth: number = 0
): void => {
    switch (motion.kind) {
        case 'clip': extractClipRootDelta(motion, previousNormalizedTime, currentNormalizedTime, loop, rootBoneIndex, rig, outTranslation, outRotation); break;
        case 'blend1d': extractBlend1DRootDelta(motion, previousNormalizedTime, currentNormalizedTime, loop, rootBoneIndex, rig, parameters, outTranslation, outRotation, context, depth); break;
        case 'blend2d': extractBlend2DRootDelta(motion, previousNormalizedTime, currentNormalizedTime, loop, rootBoneIndex, rig, parameters, outTranslation, outRotation, context, depth); break;
        case 'direct': extractDirectRootDelta(motion, previousNormalizedTime, currentNormalizedTime, loop, rootBoneIndex, rig, parameters, outTranslation, outRotation, context, depth); break;
        case 'additive': extractAdditiveRootDelta(motion, previousNormalizedTime, currentNormalizedTime, loop, rootBoneIndex, rig, parameters, outTranslation, outRotation, context, depth); break;
        default: assertNever(motion, 'Unsupported motion kind');
    }
};
