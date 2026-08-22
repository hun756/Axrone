import type { AnimationParameterStore } from './parameters';
import type {
    AnimationCompiledMotion,
    AnimationCompiledClipMotion,
    AnimationCompiledBlend1DMotion,
    AnimationCompiledBlend2DMotion,
    AnimationCompiledDirectMotion,
    AnimationCompiledAdditiveMotion,
} from './blend-types';
import type { AnimationControllerClipActivity } from './types';
import { dispatchMotion } from './blend-visitor';
import type { BlendMotionVisitor } from './blend-types';
import {
    resolveParameterScalar,
    resolveDirectChildWeight,
    resolveAdditiveWeight,
    resolveMotionTime,
    findBlend1DSegment,
    resolveBlend1DAlpha,
    computeBlend2DWeights,
} from './blend-helpers';

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
    computeBlend2DWeights(resolveParameterScalar(parameters, motion.parameterX), resolveParameterScalar(parameters, motion.parameterY), motion.children, weights);
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

interface ActivityContext {
    readonly normalizedTime: number;
    readonly loop: boolean;
    readonly parameters: AnimationParameterStore;
    readonly resolvedLayerWeight: number;
    readonly resolvedMotionWeight: number;
    readonly layerId: string;
    readonly stateId: string;
    readonly out: AnimationControllerClipActivity[];
    readonly depth: number;
    readonly weights: number[];
}

const activityVisitor: BlendMotionVisitor<ActivityContext, void> = {
    visitClip: (motion: AnimationCompiledClipMotion, ctx: ActivityContext) => collectClipActivity(motion, ctx.normalizedTime, ctx.loop, ctx.resolvedLayerWeight, ctx.resolvedMotionWeight, ctx.layerId, ctx.stateId, ctx.out),
    visitBlend1d: (motion: AnimationCompiledBlend1DMotion, ctx: ActivityContext) => collectBlend1DActivity(motion, ctx.normalizedTime, ctx.loop, ctx.parameters, ctx.resolvedLayerWeight, ctx.resolvedMotionWeight, ctx.layerId, ctx.stateId, ctx.out, ctx.depth),
    visitBlend2d: (motion: AnimationCompiledBlend2DMotion, ctx: ActivityContext) => collectBlend2DActivity(motion, ctx.normalizedTime, ctx.loop, ctx.parameters, ctx.resolvedLayerWeight, ctx.resolvedMotionWeight, ctx.layerId, ctx.stateId, ctx.out, ctx.depth, ctx.weights),
    visitDirect: (motion: AnimationCompiledDirectMotion, ctx: ActivityContext) => collectDirectActivity(motion, ctx.normalizedTime, ctx.loop, ctx.parameters, ctx.resolvedLayerWeight, ctx.resolvedMotionWeight, ctx.layerId, ctx.stateId, ctx.out, ctx.depth),
    visitAdditive: (motion: AnimationCompiledAdditiveMotion, ctx: ActivityContext) => collectAdditiveActivity(motion, ctx.normalizedTime, ctx.loop, ctx.parameters, ctx.resolvedLayerWeight, ctx.resolvedMotionWeight, ctx.layerId, ctx.stateId, ctx.out, ctx.depth),
};

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
    const resolvedLayerWeight = Math.max(0, Math.min(1, Number.isFinite(layerWeight) ? layerWeight : 0));
    const resolvedMotionWeight = Math.max(0, Number.isFinite(motionWeight) ? motionWeight : 0);
    if (resolvedLayerWeight <= 0 || resolvedMotionWeight <= 0) {
        return out;
    }
    dispatchMotion(motion, activityVisitor, {
        normalizedTime,
        loop,
        parameters,
        resolvedLayerWeight,
        resolvedMotionWeight,
        layerId,
        stateId,
        out,
        depth,
        weights: _activityWeights,
    });
    return out;
};
