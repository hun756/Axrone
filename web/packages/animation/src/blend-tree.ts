import { ObjectPool } from '@axrone/memory';
import { assertNever, AnimationValidationError } from './errors';
import { quatAccumulateWeighted, quatFinalizeWeighted, quatIdentity, quatMultiply, quatNormalize, quatSlerp } from './math';
import { AnimationParameterStore } from './parameters';
import { applyAdditiveFrame, blendFrame, blendWeightedFrames, AnimationFrame, type AnimationCurveLayout } from './pose';
import type { AnimationRig } from './rig';
import { AnimationClip } from './clip';
import type {
    AnimationControllerClipActivity,
    AnimationBlendTreeDefinition,
    AnimationControllerEvent,
    AnimationMotionDefinition,
} from './types';

export interface AnimationMotionEvaluationContext {
    readonly rig: AnimationRig;
    readonly parameters: AnimationParameterStore;
    readonly restFrame: AnimationFrame;
    readonly scratch: AnimationScratchPool;
}

export class AnimationScratchPool {
    private readonly _framePool: ObjectPool<AnimationFrame>;
    private readonly _activeFrames: AnimationFrame[] = [];

    constructor(
        private readonly _rig: AnimationRig,
        private readonly _curveLayout: AnimationCurveLayout,
        private readonly _curveDefaults?: ArrayLike<number>
    ) {
        this._framePool = new ObjectPool<AnimationFrame>({
            initialCapacity: 8,
            maxCapacity: 256,
            minFree: 8,
            expansionStrategy: 'multiplicative',
            expansionFactor: 1.5,
            allocationStrategy: 'least-recently-used',
            evictionPolicy: 'lru',
            resetOnRecycle: true,
            preallocate: false,
            autoExpand: true,
            enableMetrics: false,
            name: 'AnimationScratchPool',
            factory: () => new AnimationFrame(this._rig, this._curveLayout),
            resetHandler: (frame) => {
                frame.reset(this._rig, this._curveDefaults);
            },
        });
    }

    reset(): void {
        for (let index = this._activeFrames.length - 1; index >= 0; index -= 1) {
            this._framePool.release(this._activeFrames[index]!);
        }
        this._activeFrames.length = 0;
    }

    acquire(): AnimationFrame {
        const frame = this._framePool.acquire();
        frame.reset(this._rig, this._curveDefaults);
        this._activeFrames.push(frame);
        return frame;
    }
}

export type AnimationCompiledMotion =
    | {
          readonly kind: 'clip';
          readonly clip: AnimationClip;
          readonly timeScale: number;
          readonly cycleOffset: number;
      }
    | {
          readonly kind: 'blend1d';
          readonly parameter: string;
          readonly children: readonly {
              readonly threshold: number;
              readonly motion: AnimationCompiledMotion;
          }[];
      }
    | {
          readonly kind: 'blend2d';
          readonly parameterX: string;
          readonly parameterY: string;
          readonly children: readonly {
              readonly x: number;
              readonly y: number;
              readonly motion: AnimationCompiledMotion;
          }[];
      }
    | {
          readonly kind: 'direct';
          readonly children: readonly {
              readonly parameter?: string;
              readonly weight: number;
              readonly motion: AnimationCompiledMotion;
          }[];
      }
    | {
          readonly kind: 'additive';
          readonly base: AnimationCompiledMotion;
          readonly additive: AnimationCompiledMotion;
          readonly parameter?: string;
          readonly weight: number;
      };

const resolveMotionTime = (
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
            });
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
            });
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
            });
        case 'additive':
            return Object.freeze({
                kind: 'additive',
                base: compileMotion(definition.base, clips),
                additive: compileMotion(definition.additive, clips),
                parameter: definition.parameter,
                weight: definition.weight ?? 1,
            });
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
        });
    }
    return compileBlendTree(definition, clips);
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

const resolveParameterScalar = (parameters: AnimationParameterStore, name: string): number => {
    const value = parameters.get(name);
    return typeof value === 'number' ? value : value ? 1 : 0;
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

/**
 * Locates the active blend1d segment for the given input. Returns the left
 * child index of the blending pair, or `-(index + 1)` when the single child at
 * `index` fully drives the motion (input outside the blended threshold range).
 * Number-encoded on purpose so per-frame traversals stay allocation-free.
 */
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
    return (input - left.threshold) / Math.max(1e-6, right.threshold - left.threshold);
};

const resolveBlend2DWeights = (
    x: number,
    y: number,
    children: AnimationCompiledMotion extends never ? never : readonly {
        readonly x: number;
        readonly y: number;
        readonly motion: AnimationCompiledMotion;
    }[]
): number[] => {
    const distances = new Array<number>(children.length);
    let total = 0;
    for (let index = 0; index < children.length; index += 1) {
        const child = children[index]!;
        const dx = x - child.x;
        const dy = y - child.y;
        const distanceSquared = dx * dx + dy * dy;
        if (distanceSquared <= 1e-12) {
            const result = new Array<number>(children.length).fill(0);
            result[index] = 1;
            return result;
        }
        const inverseDistance = 1 / Math.sqrt(distanceSquared);
        distances[index] = inverseDistance;
        total += inverseDistance;
    }
    if (total <= 0) {
        return new Array<number>(children.length).fill(1 / Math.max(1, children.length));
    }
    return distances.map((value) => value / total);
};

export const resolveMotionDuration = (
    motion: AnimationCompiledMotion,
    parameters: AnimationParameterStore
): number => {
    switch (motion.kind) {
        case 'clip':
            return motion.clip.duration / Math.max(Math.abs(motion.timeScale), 1e-6);
        case 'blend1d': {
            const input = resolveParameterScalar(parameters, motion.parameter);
            const segment = findBlend1DSegment(motion.children, input);
            if (segment < 0) {
                return resolveMotionDuration(motion.children[-segment - 1]!.motion, parameters);
            }
            const alpha = resolveBlend1DAlpha(motion.children, segment, input);
            return (
                resolveMotionDuration(motion.children[segment]!.motion, parameters) * (1 - alpha) +
                resolveMotionDuration(motion.children[segment + 1]!.motion, parameters) * alpha
            );
        }
        case 'blend2d': {
            const weights = resolveBlend2DWeights(
                resolveParameterScalar(parameters, motion.parameterX),
                resolveParameterScalar(parameters, motion.parameterY),
                motion.children
            );
            let total = 0;
            for (let index = 0; index < motion.children.length; index += 1) {
                total += resolveMotionDuration(motion.children[index]!.motion, parameters) * weights[index]!;
            }
            return total;
        }
        case 'direct': {
            let weightedDuration = 0;
            let totalWeight = 0;
            for (let index = 0; index < motion.children.length; index += 1) {
                const child = motion.children[index]!;
                const weight = resolveDirectChildWeight(parameters, child.parameter, child.weight);
                if (weight <= 0) {
                    continue;
                }
                totalWeight += weight;
                weightedDuration += resolveMotionDuration(child.motion, parameters) * weight;
            }
            return totalWeight > 0 ? weightedDuration / totalWeight : 0;
        }
        case 'additive':
            return resolveMotionDuration(motion.base, parameters);
        default:
            return assertNever(motion, 'Unsupported motion kind');
    }
};

export const evaluateMotion = (
    motion: AnimationCompiledMotion,
    normalizedTime: number,
    context: AnimationMotionEvaluationContext,
    out: AnimationFrame,
    loop: boolean = true
): AnimationFrame => {
    switch (motion.kind) {
        case 'clip': {
            out.reset(context.rig, context.restFrame.curves.values);
            const time = resolveMotionTime(
                normalizedTime * motion.timeScale,
                motion.clip.duration,
                motion.cycleOffset,
                loop
            );
            return motion.clip.sampleTime(time, out);
        }
        case 'blend1d': {
            const input = resolveParameterScalar(context.parameters, motion.parameter);
            const segment = findBlend1DSegment(motion.children, input);
            if (segment < 0) {
                return evaluateMotion(
                    motion.children[-segment - 1]!.motion,
                    normalizedTime,
                    context,
                    out,
                    loop
                );
            }
            const alpha = resolveBlend1DAlpha(motion.children, segment, input);
            const leftFrame = context.scratch.acquire();
            const rightFrame = context.scratch.acquire();
            evaluateMotion(motion.children[segment]!.motion, normalizedTime, context, leftFrame, loop);
            evaluateMotion(motion.children[segment + 1]!.motion, normalizedTime, context, rightFrame, loop);
            return blendFrame(out, leftFrame, rightFrame, alpha);
        }
        case 'blend2d': {
            const weights = resolveBlend2DWeights(
                resolveParameterScalar(context.parameters, motion.parameterX),
                resolveParameterScalar(context.parameters, motion.parameterY),
                motion.children
            );
            const frames = new Array<AnimationFrame>(motion.children.length);
            for (let index = 0; index < motion.children.length; index += 1) {
                const frame = context.scratch.acquire();
                evaluateMotion(motion.children[index]!.motion, normalizedTime, context, frame, loop);
                frames[index] = frame;
            }
            return blendWeightedFrames(out, frames, weights, context.restFrame);
        }
        case 'direct': {
            const frames: AnimationFrame[] = [];
            const weights: number[] = [];
            for (let index = 0; index < motion.children.length; index += 1) {
                const child = motion.children[index]!;
                const weight = resolveDirectChildWeight(context.parameters, child.parameter, child.weight);
                if (weight <= 0) {
                    continue;
                }
                const frame = context.scratch.acquire();
                evaluateMotion(child.motion, normalizedTime, context, frame, loop);
                frames.push(frame);
                weights.push(weight);
            }
            if (frames.length === 0) {
                return out.copyFrom(context.restFrame);
            }
            if (frames.length === 1) {
                return out.copyFrom(frames[0]!);
            }
            return blendWeightedFrames(out, frames, weights, context.restFrame);
        }
        case 'additive': {
            const baseFrame = context.scratch.acquire();
            const additiveFrame = context.scratch.acquire();
            evaluateMotion(motion.base, normalizedTime, context, baseFrame, loop);
            evaluateMotion(motion.additive, normalizedTime, context, additiveFrame, loop);
            const resolvedWeight = resolveAdditiveWeight(context.parameters, motion.parameter, motion.weight);
            return applyAdditiveFrame(out, baseFrame, additiveFrame, context.restFrame, resolvedWeight);
        }
        default:
            return assertNever(motion, 'Unsupported motion kind');
    }
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
    out: AnimationControllerEvent[] = []
): readonly AnimationControllerEvent[] => {
    const resolvedLayerWeight = Math.max(0, Math.min(1, layerWeight));
    const resolvedMotionWeight = Math.max(0, motionWeight);
    if (resolvedLayerWeight <= 0 || resolvedMotionWeight <= 0) {
        return out;
    }

    switch (motion.kind) {
        case 'clip': {
            const hits = motion.clip.collectEvents(
                resolveMotionTime(
                    previousNormalizedTime * motion.timeScale,
                    motion.clip.duration,
                    motion.cycleOffset,
                    loop
                ),
                resolveMotionTime(
                    currentNormalizedTime * motion.timeScale,
                    motion.clip.duration,
                    motion.cycleOffset,
                    loop
                ),
                loop
            );
            for (let index = 0; index < hits.length; index += 1) {
                const event = hits[index]!;
                // Hot path: no Object.freeze here; immutability is enforced by the
                // readonly type surface to avoid per-frame hidden-class churn.
                out.push({
                    ...event,
                    layerId,
                    stateId,
                    layerWeight: resolvedLayerWeight,
                    motionWeight: resolvedMotionWeight,
                } satisfies AnimationControllerEvent);
            }
            return out;
        }
        case 'blend1d': {
            const input = resolveParameterScalar(parameters, motion.parameter);
            const segment = findBlend1DSegment(motion.children, input);
            if (segment < 0) {
                return collectMotionEvents(
                    motion.children[-segment - 1]!.motion,
                    previousNormalizedTime,
                    currentNormalizedTime,
                    loop,
                    parameters,
                    layerId,
                    stateId,
                    resolvedLayerWeight,
                    resolvedMotionWeight,
                    out
                );
            }
            const alpha = resolveBlend1DAlpha(motion.children, segment, input);
            collectMotionEvents(
                motion.children[segment]!.motion,
                previousNormalizedTime,
                currentNormalizedTime,
                loop,
                parameters,
                layerId,
                stateId,
                resolvedLayerWeight,
                resolvedMotionWeight * (1 - alpha),
                out
            );
            collectMotionEvents(
                motion.children[segment + 1]!.motion,
                previousNormalizedTime,
                currentNormalizedTime,
                loop,
                parameters,
                layerId,
                stateId,
                resolvedLayerWeight,
                resolvedMotionWeight * alpha,
                out
            );
            return out;
        }
        case 'blend2d': {
            const weights = resolveBlend2DWeights(
                resolveParameterScalar(parameters, motion.parameterX),
                resolveParameterScalar(parameters, motion.parameterY),
                motion.children
            );
            for (let index = 0; index < motion.children.length; index += 1) {
                const childWeight = weights[index] ?? 0;
                if (childWeight <= 0) {
                    continue;
                }
                collectMotionEvents(
                    motion.children[index]!.motion,
                    previousNormalizedTime,
                    currentNormalizedTime,
                    loop,
                    parameters,
                    layerId,
                    stateId,
                    resolvedLayerWeight,
                    resolvedMotionWeight * childWeight,
                    out
                );
            }
            return out;
        }
        case 'direct': {
            for (let index = 0; index < motion.children.length; index += 1) {
                const child = motion.children[index]!;
                const childWeight = resolveDirectChildWeight(parameters, child.parameter, child.weight);
                if (childWeight <= 0) {
                    continue;
                }
                collectMotionEvents(
                    child.motion,
                    previousNormalizedTime,
                    currentNormalizedTime,
                    loop,
                    parameters,
                    layerId,
                    stateId,
                    resolvedLayerWeight,
                    resolvedMotionWeight * childWeight,
                    out
                );
            }
            return out;
        }
        case 'additive': {
            collectMotionEvents(
                motion.base,
                previousNormalizedTime,
                currentNormalizedTime,
                loop,
                parameters,
                layerId,
                stateId,
                resolvedLayerWeight,
                resolvedMotionWeight,
                out
            );
            const additiveWeight = resolveAdditiveWeight(parameters, motion.parameter, motion.weight);
            if (additiveWeight > 0) {
                collectMotionEvents(
                    motion.additive,
                    previousNormalizedTime,
                    currentNormalizedTime,
                    loop,
                    parameters,
                    layerId,
                    stateId,
                    resolvedLayerWeight,
                    resolvedMotionWeight * additiveWeight,
                    out
                );
            }
            return out;
        }
        default:
            return assertNever(motion, 'Unsupported motion kind');
    }
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
    out: AnimationControllerClipActivity[] = []
): readonly AnimationControllerClipActivity[] => {
    const resolvedLayerWeight = Math.max(0, Math.min(1, layerWeight));
    const resolvedMotionWeight = Math.max(0, motionWeight);
    if (resolvedLayerWeight <= 0 || resolvedMotionWeight <= 0) {
        return out;
    }

    switch (motion.kind) {
        case 'clip': {
            const time = resolveMotionTime(
                normalizedTime * motion.timeScale,
                motion.clip.duration,
                motion.cycleOffset,
                loop
            );
            // Hot path: no Object.freeze here; immutability is enforced by the
            // readonly type surface to avoid per-frame hidden-class churn.
            out.push({
                clipId: motion.clip.id,
                layerId,
                stateId,
                layerWeight: resolvedLayerWeight,
                motionWeight: resolvedMotionWeight,
                loop,
                time,
                normalizedTime: motion.clip.duration > 0 ? time / motion.clip.duration : 0,
            } satisfies AnimationControllerClipActivity);
            return out;
        }
        case 'blend1d': {
            const input = resolveParameterScalar(parameters, motion.parameter);
            const segment = findBlend1DSegment(motion.children, input);
            if (segment < 0) {
                return collectMotionClipActivities(
                    motion.children[-segment - 1]!.motion,
                    normalizedTime,
                    loop,
                    parameters,
                    layerId,
                    stateId,
                    resolvedLayerWeight,
                    resolvedMotionWeight,
                    out
                );
            }
            const alpha = resolveBlend1DAlpha(motion.children, segment, input);
            collectMotionClipActivities(
                motion.children[segment]!.motion,
                normalizedTime,
                loop,
                parameters,
                layerId,
                stateId,
                resolvedLayerWeight,
                resolvedMotionWeight * (1 - alpha),
                out
            );
            collectMotionClipActivities(
                motion.children[segment + 1]!.motion,
                normalizedTime,
                loop,
                parameters,
                layerId,
                stateId,
                resolvedLayerWeight,
                resolvedMotionWeight * alpha,
                out
            );
            return out;
        }
        case 'blend2d': {
            const weights = resolveBlend2DWeights(
                resolveParameterScalar(parameters, motion.parameterX),
                resolveParameterScalar(parameters, motion.parameterY),
                motion.children
            );
            for (let index = 0; index < motion.children.length; index += 1) {
                const childWeight = weights[index] ?? 0;
                if (childWeight <= 0) {
                    continue;
                }
                collectMotionClipActivities(
                    motion.children[index]!.motion,
                    normalizedTime,
                    loop,
                    parameters,
                    layerId,
                    stateId,
                    resolvedLayerWeight,
                    resolvedMotionWeight * childWeight,
                    out
                );
            }
            return out;
        }
        case 'direct': {
            for (let index = 0; index < motion.children.length; index += 1) {
                const child = motion.children[index]!;
                const childWeight = resolveDirectChildWeight(parameters, child.parameter, child.weight);
                if (childWeight <= 0) {
                    continue;
                }
                collectMotionClipActivities(
                    child.motion,
                    normalizedTime,
                    loop,
                    parameters,
                    layerId,
                    stateId,
                    resolvedLayerWeight,
                    resolvedMotionWeight * childWeight,
                    out
                );
            }
            return out;
        }
        case 'additive': {
            collectMotionClipActivities(
                motion.base,
                normalizedTime,
                loop,
                parameters,
                layerId,
                stateId,
                resolvedLayerWeight,
                resolvedMotionWeight,
                out
            );
            const additiveWeight = resolveAdditiveWeight(parameters, motion.parameter, motion.weight);
            if (additiveWeight > 0) {
                collectMotionClipActivities(
                    motion.additive,
                    normalizedTime,
                    loop,
                    parameters,
                    layerId,
                    stateId,
                    resolvedLayerWeight,
                    resolvedMotionWeight * additiveWeight,
                    out
                );
            }
            return out;
        }
        default:
            return assertNever(motion, 'Unsupported motion kind');
    }
};

interface AnimationRootDeltaScratch {
    readonly translation: Float32Array;
    readonly rotation: Float32Array;
}

// Depth-indexed scratch slots for extractMotionRootDelta so the recursive root
// motion extraction stays allocation-free on the per-frame hot path. Each
// recursion depth owns two slots; child recursions use the next depth's slots.
const rootDeltaScratchSlots: AnimationRootDeltaScratch[] = [];

const acquireRootDeltaScratch = (slotIndex: number): AnimationRootDeltaScratch => {
    let slot = rootDeltaScratchSlots[slotIndex];
    if (!slot) {
        slot = {
            translation: new Float32Array(3),
            rotation: new Float32Array(4),
        };
        rootDeltaScratchSlots[slotIndex] = slot;
    }
    return slot;
};

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
    depth: number = 0
): void => {
    switch (motion.kind) {
        case 'clip': {
            motion.clip.extractBoneDelta(
                rootBoneIndex,
                resolveMotionTime(
                    previousNormalizedTime * motion.timeScale,
                    motion.clip.duration,
                    motion.cycleOffset,
                    loop
                ),
                resolveMotionTime(
                    currentNormalizedTime * motion.timeScale,
                    motion.clip.duration,
                    motion.cycleOffset,
                    loop
                ),
                loop,
                rig,
                outTranslation,
                outRotation
            );
            return;
        }
        case 'blend1d': {
            const input = resolveParameterScalar(parameters, motion.parameter);
            const segment = findBlend1DSegment(motion.children, input);
            if (segment < 0) {
                return extractMotionRootDelta(
                    motion.children[-segment - 1]!.motion,
                    previousNormalizedTime,
                    currentNormalizedTime,
                    loop,
                    rootBoneIndex,
                    rig,
                    parameters,
                    outTranslation,
                    outRotation,
                    depth
                );
            }
            const alpha = resolveBlend1DAlpha(motion.children, segment, input);
            const leftScratch = acquireRootDeltaScratch(depth * 2);
            const rightScratch = acquireRootDeltaScratch(depth * 2 + 1);
            extractMotionRootDelta(
                motion.children[segment]!.motion,
                previousNormalizedTime,
                currentNormalizedTime,
                loop,
                rootBoneIndex,
                rig,
                parameters,
                leftScratch.translation,
                leftScratch.rotation,
                depth + 1
            );
            extractMotionRootDelta(
                motion.children[segment + 1]!.motion,
                previousNormalizedTime,
                currentNormalizedTime,
                loop,
                rootBoneIndex,
                rig,
                parameters,
                rightScratch.translation,
                rightScratch.rotation,
                depth + 1
            );
            outTranslation[0] =
                leftScratch.translation[0]! +
                (rightScratch.translation[0]! - leftScratch.translation[0]!) * alpha;
            outTranslation[1] =
                leftScratch.translation[1]! +
                (rightScratch.translation[1]! - leftScratch.translation[1]!) * alpha;
            outTranslation[2] =
                leftScratch.translation[2]! +
                (rightScratch.translation[2]! - leftScratch.translation[2]!) * alpha;
            quatSlerp(outRotation, 0, leftScratch.rotation, 0, rightScratch.rotation, 0, alpha);
            return;
        }
        case 'blend2d': {
            const weights = resolveBlend2DWeights(
                resolveParameterScalar(parameters, motion.parameterX),
                resolveParameterScalar(parameters, motion.parameterY),
                motion.children
            );
            const childScratch = acquireRootDeltaScratch(depth * 2);
            const referenceScratch = acquireRootDeltaScratch(depth * 2 + 1);
            outTranslation.fill(0);
            let totalRotationWeight = 0;
            for (let index = 0; index < motion.children.length; index += 1) {
                extractMotionRootDelta(
                    motion.children[index]!.motion,
                    previousNormalizedTime,
                    currentNormalizedTime,
                    loop,
                    rootBoneIndex,
                    rig,
                    parameters,
                    childScratch.translation,
                    childScratch.rotation,
                    depth + 1
                );
                const weight = Math.max(0, weights[index] ?? 0);
                outTranslation[0] += childScratch.translation[0]! * weights[index]!;
                outTranslation[1] += childScratch.translation[1]! * weights[index]!;
                outTranslation[2] += childScratch.translation[2]! * weights[index]!;
                if (weight <= 0) {
                    continue;
                }
                quatAccumulateWeighted(
                    outRotation,
                    0,
                    referenceScratch.rotation,
                    0,
                    childScratch.rotation,
                    0,
                    weight,
                    totalRotationWeight <= 0
                );
                totalRotationWeight += weight;
            }
            quatFinalizeWeighted(outRotation, 0, outRotation, 0, totalRotationWeight);
            return;
        }
        case 'direct': {
            const childScratch = acquireRootDeltaScratch(depth * 2);
            const referenceScratch = acquireRootDeltaScratch(depth * 2 + 1);
            outTranslation.fill(0);
            let totalWeight = 0;
            for (let index = 0; index < motion.children.length; index += 1) {
                const child = motion.children[index]!;
                const weight = resolveDirectChildWeight(parameters, child.parameter, child.weight);
                if (weight <= 0) {
                    continue;
                }
                extractMotionRootDelta(
                    child.motion,
                    previousNormalizedTime,
                    currentNormalizedTime,
                    loop,
                    rootBoneIndex,
                    rig,
                    parameters,
                    childScratch.translation,
                    childScratch.rotation,
                    depth + 1
                );
                outTranslation[0] += childScratch.translation[0]! * weight;
                outTranslation[1] += childScratch.translation[1]! * weight;
                outTranslation[2] += childScratch.translation[2]! * weight;
                quatAccumulateWeighted(
                    outRotation,
                    0,
                    referenceScratch.rotation,
                    0,
                    childScratch.rotation,
                    0,
                    weight,
                    totalWeight <= 0
                );
                totalWeight += weight;
            }
            if (totalWeight > 0) {
                outTranslation[0] /= totalWeight;
                outTranslation[1] /= totalWeight;
                outTranslation[2] /= totalWeight;
            }
            quatFinalizeWeighted(outRotation, 0, outRotation, 0, totalWeight);
            return;
        }
        case 'additive': {
            const baseScratch = acquireRootDeltaScratch(depth * 2);
            const additiveScratch = acquireRootDeltaScratch(depth * 2 + 1);
            extractMotionRootDelta(
                motion.base,
                previousNormalizedTime,
                currentNormalizedTime,
                loop,
                rootBoneIndex,
                rig,
                parameters,
                baseScratch.translation,
                baseScratch.rotation,
                depth + 1
            );
            extractMotionRootDelta(
                motion.additive,
                previousNormalizedTime,
                currentNormalizedTime,
                loop,
                rootBoneIndex,
                rig,
                parameters,
                additiveScratch.translation,
                additiveScratch.rotation,
                depth + 1
            );
            const resolvedWeight = resolveAdditiveWeight(parameters, motion.parameter, motion.weight);
            outTranslation[0] = baseScratch.translation[0]! + additiveScratch.translation[0]! * resolvedWeight;
            outTranslation[1] = baseScratch.translation[1]! + additiveScratch.translation[1]! * resolvedWeight;
            outTranslation[2] = baseScratch.translation[2]! + additiveScratch.translation[2]! * resolvedWeight;
            quatIdentity(outRotation, 0);
            quatSlerp(outRotation, 0, outRotation, 0, additiveScratch.rotation, 0, resolvedWeight);
            quatMultiply(outRotation, 0, baseScratch.rotation, 0, outRotation, 0);
            quatNormalize(outRotation, 0, outRotation, 0);
            return;
        }
        default:
            return assertNever(motion, 'Unsupported motion kind');
    }
};
