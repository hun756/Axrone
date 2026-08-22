import type { AnimationParameterStore } from './parameters';
import type {
    AnimationCompiledMotion,
    AnimationCompiledClipMotion,
    AnimationCompiledBlend1DMotion,
    AnimationCompiledBlend2DMotion,
    AnimationCompiledDirectMotion,
    AnimationCompiledAdditiveMotion,
} from './blend-types';
import type { AnimationControllerEvent } from './types';
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

function collectClipEvents(
    motion: AnimationCompiledClipMotion,
    prevTime: number,
    currTime: number,
    loop: boolean,
    resolvedLayerWeight: number,
    resolvedMotionWeight: number,
    layerId: string,
    stateId: string,
    out: AnimationControllerEvent[]
): void {
    const hits = motion.clip.collectEvents(
        resolveMotionTime(prevTime * motion.timeScale, motion.clip.duration, motion.cycleOffset, loop),
        resolveMotionTime(currTime * motion.timeScale, motion.clip.duration, motion.cycleOffset, loop),
        loop
    );
    for (let index = 0; index < hits.length; index += 1) {
        const event = hits[index]!;
        out.push({ ...event, layerId, stateId, layerWeight: resolvedLayerWeight, motionWeight: resolvedMotionWeight } satisfies AnimationControllerEvent);
    }
}

function collectBlend1DEvents(
    motion: AnimationCompiledBlend1DMotion,
    prevTime: number,
    currTime: number,
    loop: boolean,
    parameters: AnimationParameterStore,
    resolvedLayerWeight: number,
    resolvedMotionWeight: number,
    layerId: string,
    stateId: string,
    out: AnimationControllerEvent[],
    depth: number
): void {
    const input = resolveParameterScalar(parameters, motion.parameter);
    const segment = findBlend1DSegment(motion.children, input);
    if (segment < 0) {
        collectMotionEvents(motion.children[-segment - 1]!.motion, prevTime, currTime, loop, parameters, layerId, stateId, resolvedLayerWeight, resolvedMotionWeight, out, depth + 1);
        return;
    }
    const alpha = resolveBlend1DAlpha(motion.children, segment, input);
    collectMotionEvents(motion.children[segment]!.motion, prevTime, currTime, loop, parameters, layerId, stateId, resolvedLayerWeight, resolvedMotionWeight * (1 - alpha), out, depth + 1);
    collectMotionEvents(motion.children[segment + 1]!.motion, prevTime, currTime, loop, parameters, layerId, stateId, resolvedLayerWeight, resolvedMotionWeight * alpha, out, depth + 1);
}

function collectBlend2DEvents(
    motion: AnimationCompiledBlend2DMotion,
    prevTime: number,
    currTime: number,
    loop: boolean,
    parameters: AnimationParameterStore,
    resolvedLayerWeight: number,
    resolvedMotionWeight: number,
    layerId: string,
    stateId: string,
    out: AnimationControllerEvent[],
    depth: number,
    weights: number[]
): void {
    computeBlend2DWeights(resolveParameterScalar(parameters, motion.parameterX), resolveParameterScalar(parameters, motion.parameterY), motion.children, weights);
    for (let index = 0; index < motion.children.length; index += 1) {
        const childWeight = weights[index] ?? 0;
        if (childWeight <= 0) continue;
        collectMotionEvents(motion.children[index]!.motion, prevTime, currTime, loop, parameters, layerId, stateId, resolvedLayerWeight, resolvedMotionWeight * childWeight, out, depth + 1);
    }
}

function collectDirectEvents(
    motion: AnimationCompiledDirectMotion,
    prevTime: number,
    currTime: number,
    loop: boolean,
    parameters: AnimationParameterStore,
    resolvedLayerWeight: number,
    resolvedMotionWeight: number,
    layerId: string,
    stateId: string,
    out: AnimationControllerEvent[],
    depth: number
): void {
    for (let index = 0; index < motion.children.length; index += 1) {
        const child = motion.children[index]!;
        const childWeight = resolveDirectChildWeight(parameters, child.parameter, child.weight);
        if (childWeight <= 0) continue;
        collectMotionEvents(child.motion, prevTime, currTime, loop, parameters, layerId, stateId, resolvedLayerWeight, resolvedMotionWeight * childWeight, out, depth + 1);
    }
}

function collectAdditiveEvents(
    motion: AnimationCompiledAdditiveMotion,
    prevTime: number,
    currTime: number,
    loop: boolean,
    parameters: AnimationParameterStore,
    resolvedLayerWeight: number,
    resolvedMotionWeight: number,
    layerId: string,
    stateId: string,
    out: AnimationControllerEvent[],
    depth: number
): void {
    collectMotionEvents(motion.base, prevTime, currTime, loop, parameters, layerId, stateId, resolvedLayerWeight, resolvedMotionWeight, out, depth + 1);
    const additiveWeight = resolveAdditiveWeight(parameters, motion.parameter, motion.weight);
    if (additiveWeight > 0) {
        collectMotionEvents(motion.additive, prevTime, currTime, loop, parameters, layerId, stateId, resolvedLayerWeight, resolvedMotionWeight * additiveWeight, out, depth + 1);
    }
}

const _eventWeights: number[] = [];

interface EventContext {
    readonly previousNormalizedTime: number;
    readonly currentNormalizedTime: number;
    readonly loop: boolean;
    readonly parameters: AnimationParameterStore;
    readonly resolvedLayerWeight: number;
    readonly resolvedMotionWeight: number;
    readonly layerId: string;
    readonly stateId: string;
    readonly out: AnimationControllerEvent[];
    readonly depth: number;
    readonly weights: number[];
}

const eventVisitor: BlendMotionVisitor<EventContext, void> = {
    visitClip: (motion: AnimationCompiledClipMotion, ctx: EventContext) => collectClipEvents(motion, ctx.previousNormalizedTime, ctx.currentNormalizedTime, ctx.loop, ctx.resolvedLayerWeight, ctx.resolvedMotionWeight, ctx.layerId, ctx.stateId, ctx.out),
    visitBlend1d: (motion: AnimationCompiledBlend1DMotion, ctx: EventContext) => collectBlend1DEvents(motion, ctx.previousNormalizedTime, ctx.currentNormalizedTime, ctx.loop, ctx.parameters, ctx.resolvedLayerWeight, ctx.resolvedMotionWeight, ctx.layerId, ctx.stateId, ctx.out, ctx.depth),
    visitBlend2d: (motion: AnimationCompiledBlend2DMotion, ctx: EventContext) => collectBlend2DEvents(motion, ctx.previousNormalizedTime, ctx.currentNormalizedTime, ctx.loop, ctx.parameters, ctx.resolvedLayerWeight, ctx.resolvedMotionWeight, ctx.layerId, ctx.stateId, ctx.out, ctx.depth, ctx.weights),
    visitDirect: (motion: AnimationCompiledDirectMotion, ctx: EventContext) => collectDirectEvents(motion, ctx.previousNormalizedTime, ctx.currentNormalizedTime, ctx.loop, ctx.parameters, ctx.resolvedLayerWeight, ctx.resolvedMotionWeight, ctx.layerId, ctx.stateId, ctx.out, ctx.depth),
    visitAdditive: (motion: AnimationCompiledAdditiveMotion, ctx: EventContext) => collectAdditiveEvents(motion, ctx.previousNormalizedTime, ctx.currentNormalizedTime, ctx.loop, ctx.parameters, ctx.resolvedLayerWeight, ctx.resolvedMotionWeight, ctx.layerId, ctx.stateId, ctx.out, ctx.depth),
};

export const collectMotionEvents = (
    motion: AnimationCompiledMotion,
    previousNormalizedTime: number,
    currentNormalizedTime: number,
    loop: boolean,
    parameters: AnimationParameterStore,
    layerId: string,
    stateId: string,
    layerWeight: number,
    motionWeight: number,
    out: AnimationControllerEvent[] = [],
    depth: number = 0
): readonly AnimationControllerEvent[] => {
    const resolvedLayerWeight = Math.max(0, Math.min(1, Number.isFinite(layerWeight) ? layerWeight : 0));
    const resolvedMotionWeight = Math.max(0, Number.isFinite(motionWeight) ? motionWeight : 0);
    if (resolvedLayerWeight <= 0 || resolvedMotionWeight <= 0) {
        return out;
    }
    dispatchMotion(motion, eventVisitor, {
        previousNormalizedTime,
        currentNormalizedTime,
        loop,
        parameters,
        resolvedLayerWeight,
        resolvedMotionWeight,
        layerId,
        stateId,
        out,
        depth,
        weights: _eventWeights,
    });
    return out;
};
