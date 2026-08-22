import { AnimationValidationError } from './errors';
import { AnimationClip } from './clip';
import type {
    AnimationCompiledMotion,
    AnimationCompiledBlend1DMotion,
    AnimationCompiledBlend2DMotion,
    AnimationCompiledDirectMotion,
    AnimationCompiledAdditiveMotion,
    AnimationCompiledClipMotion,
} from './blend-types';
import type { AnimationBlendTreeDefinition, AnimationMotionDefinition } from './types';
import { assertNever } from './errors';

const compileBlendTree = (
    definition: AnimationBlendTreeDefinition,
    clips: ReadonlyMap<string, AnimationClip>
): AnimationCompiledMotion => {
    switch (definition.kind) {
        case 'blend1d':
            if (definition.children.length === 0) {
                throw new AnimationValidationError('1D blend trees require at least one child');
            }
            return Object.freeze({
                kind: 'blend1d',
                parameter: definition.parameter,
                children: Object.freeze(
                    [...definition.children]
                        .map((child) =>
                            Object.freeze({
                                threshold: child.threshold,
                                motion: compileMotion(child.motion, clips),
                            })
                        )
                        .sort((left, right) => left.threshold - right.threshold)
                ),
            } satisfies AnimationCompiledBlend1DMotion);
        case 'blend2d':
            if (definition.children.length === 0) {
                throw new AnimationValidationError('2D blend trees require at least one child');
            }
            return Object.freeze({
                kind: 'blend2d',
                parameterX: definition.parameterX,
                parameterY: definition.parameterY,
                children: Object.freeze(
                    definition.children.map((child) =>
                        Object.freeze({
                            x: child.position[0],
                            y: child.position[1],
                            motion: compileMotion(child.motion, clips),
                        })
                    )
                ),
            } satisfies AnimationCompiledBlend2DMotion);
        case 'direct':
            if (definition.children.length === 0) {
                throw new AnimationValidationError('Direct blend trees require at least one child');
            }
            return Object.freeze({
                kind: 'direct',
                children: Object.freeze(
                    definition.children.map((child) =>
                        Object.freeze({
                            parameter: child.parameter,
                            weight: child.weight ?? 1,
                            motion: compileMotion(child.motion, clips),
                        })
                    )
                ),
            } satisfies AnimationCompiledDirectMotion);
        case 'additive':
            return Object.freeze({
                kind: 'additive',
                base: compileMotion(definition.base, clips),
                additive: compileMotion(definition.additive, clips),
                parameter: definition.parameter,
                weight: definition.weight ?? 1,
            } satisfies AnimationCompiledAdditiveMotion);
        default:
            return assertNever(definition, 'Unsupported blend tree');
    }
};

export const compileMotion = (
    definition: AnimationMotionDefinition,
    clips: ReadonlyMap<string, AnimationClip>
): AnimationCompiledMotion => {
    if (definition.kind === 'clip') {
        const clip = clips.get(definition.clipId);
        if (!clip) {
            throw new AnimationValidationError(`Unknown animation clip '${definition.clipId}'`);
        }
        return Object.freeze({
            kind: 'clip',
            clip,
            timeScale: definition.timeScale ?? 1,
            cycleOffset: definition.cycleOffset ?? 0,
        } satisfies AnimationCompiledClipMotion);
    }
    return compileBlendTree(definition, clips);
};
