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
    resolveBlend2DWeights(
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

export const resolveMotionDuration = (
    motion: AnimationCompiledMotion,
    parameters: AnimationParameterStore,
    depth: number = 0
): number => {
    switch (motion.kind) {
        case 'clip': return resolveClipDuration(motion, parameters);
        case 'blend1d': return resolveBlend1DDuration(motion, parameters, depth);
        case 'blend2d': return resolveBlend2DDuration(motion, parameters, depth, _durationWeights);
        case 'direct': return resolveDirectDuration(motion, parameters, depth);
        case 'additive': return resolveAdditiveDuration(motion, parameters, depth);
        default: return assertNever(motion, 'Unsupported motion kind');
    }
};
