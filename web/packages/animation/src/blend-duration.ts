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
import { dispatchMotion } from './blend-visitor';
import type { BlendMotionVisitor } from './blend-types';
import {
    resolveParameterScalar,
    resolveDirectChildWeight,
    findBlend1DSegment,
    resolveBlend1DAlpha,
    computeBlend2DWeights,
} from './blend-helpers';

function resolveClipDuration(motion: AnimationCompiledClipMotion, parameters: AnimationParameterStore): number {
    return motion.clip.duration / Math.max(Math.abs(motion.timeScale), BLEND_EPSILON);
}

function resolveBlend1DDuration(motion: AnimationCompiledBlend1DMotion, parameters: AnimationParameterStore, depth: number): number {
    const input = resolveParameterScalar(parameters, motion.parameter);
    const segment = findBlend1DSegment(motion.children, input);
    if (segment < 0) {
        return resolveMotionDuration(motion.children[-segment - 1]!.motion, parameters, depth + 1);
    }
    const alpha = resolveBlend1DAlpha(motion.children, segment, input);
    return (
        resolveMotionDuration(motion.children[segment]!.motion, parameters, depth + 1) * (1 - alpha) +
        resolveMotionDuration(motion.children[segment + 1]!.motion, parameters, depth + 1) * alpha
    );
}

function resolveBlend2DDuration(motion: AnimationCompiledBlend2DMotion, parameters: AnimationParameterStore, depth: number, weights: number[]): number {
    computeBlend2DWeights(
        resolveParameterScalar(parameters, motion.parameterX),
        resolveParameterScalar(parameters, motion.parameterY),
        motion.children,
        weights
    );
    let total = 0;
    for (let index = 0; index < motion.children.length; index += 1) {
        total += resolveMotionDuration(motion.children[index]!.motion, parameters, depth + 1) * weights[index]!;
    }
    return total;
}

function resolveDirectDuration(motion: AnimationCompiledDirectMotion, parameters: AnimationParameterStore, depth: number): number {
    let weightedDuration = 0;
    let totalWeight = 0;
    for (let index = 0; index < motion.children.length; index += 1) {
        const child = motion.children[index]!;
        const weight = resolveDirectChildWeight(parameters, child.parameter, child.weight);
        if (weight <= 0) {
            continue;
        }
        totalWeight += weight;
        weightedDuration += resolveMotionDuration(child.motion, parameters, depth + 1) * weight;
    }
    return totalWeight > 0 ? weightedDuration / totalWeight : 0;
}

function resolveAdditiveDuration(motion: AnimationCompiledAdditiveMotion, parameters: AnimationParameterStore, depth: number): number {
    return resolveMotionDuration(motion.base, parameters, depth + 1);
}

const _durationWeights: number[] = [];

interface DurationContext {
    readonly parameters: AnimationParameterStore;
    readonly depth: number;
    readonly weights: number[];
}

const durationVisitor: BlendMotionVisitor<DurationContext, number> = {
    visitClip: (motion: AnimationCompiledClipMotion, ctx: DurationContext) => resolveClipDuration(motion, ctx.parameters),
    visitBlend1d: (motion: AnimationCompiledBlend1DMotion, ctx: DurationContext) => resolveBlend1DDuration(motion, ctx.parameters, ctx.depth),
    visitBlend2d: (motion: AnimationCompiledBlend2DMotion, ctx: DurationContext) => resolveBlend2DDuration(motion, ctx.parameters, ctx.depth, ctx.weights),
    visitDirect: (motion: AnimationCompiledDirectMotion, ctx: DurationContext) => resolveDirectDuration(motion, ctx.parameters, ctx.depth),
    visitAdditive: (motion: AnimationCompiledAdditiveMotion, ctx: DurationContext) => resolveAdditiveDuration(motion, ctx.parameters, ctx.depth),
};

export const resolveMotionDuration = (
    motion: AnimationCompiledMotion,
    parameters: AnimationParameterStore,
    depth: number = 0
): number => dispatchMotion(motion, durationVisitor, { parameters, depth, weights: _durationWeights });
