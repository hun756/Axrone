import { describe, expect, it } from 'vitest';
import { compileMotion, extractMotionRootDelta } from '../blend-tree';
import { AnimationScratchPool, BlendScratchContext } from '../blend-scratch';
import { AnimationClip } from '../clip';
import { AnimationParameterStore } from '../parameters';
import { AnimationCurveLayout, AnimationFrame } from '../pose';
import { AnimationRig } from '../rig';
import type { AnimationMotionDefinition } from '../types';
import type { AnimationMotionEvaluationContext } from '../blend-scratch';

const rig = new AnimationRig({ bones: [{ name: 'root' }] });
const curveLayout = new AnimationCurveLayout();

const makeTranslationClip = (id: string, distancePerCycle: number): AnimationClip =>
    new AnimationClip(
        {
            id,
            tracks: [
                {
                    target: 'root',
                    path: 'translation',
                    times: [0, 1],
                    values: [0, 0, 0, distancePerCycle, 0, 0],
                },
            ],
        },
        rig,
        curveLayout
    );

const clips = new Map<string, AnimationClip>([
    ['slow', makeTranslationClip('slow', 1)],
    ['fast', makeTranslationClip('fast', 2)],
]);

const parameters = new AnimationParameterStore([
    { name: 'blend', kind: 'float', defaultValue: 0.5 },
    { name: 'x', kind: 'float', defaultValue: 0 },
    { name: 'y', kind: 'float', defaultValue: 0 },
]);

const scratchPool = new AnimationScratchPool(rig, curveLayout);
const blendScratch = new BlendScratchContext();
const restFrame = new AnimationFrame(rig, curveLayout);
const context: AnimationMotionEvaluationContext = {
    rig,
    parameters,
    restFrame,
    scratch: scratchPool,
    blendScratch,
};

const extract = (definition: AnimationMotionDefinition): { translation: Float32Array; rotation: Float32Array } => {
    const translation = new Float32Array(3);
    const rotation = new Float32Array(4);
    extractMotionRootDelta(
        compileMotion(definition, clips),
        0,
        0.25,
        false,
        rig.indexOfBone('root'),
        rig,
        parameters,
        translation,
        rotation,
        context
    );
    return { translation, rotation };
};

describe('extractMotionRootDelta numerical behaviour', () => {
    it('extracts a clip root delta over the sampled window', () => {
        const { translation, rotation } = extract({ kind: 'clip', clipId: 'slow' });
        expect(translation[0]).toBeCloseTo(0.25, 5);
        expect(translation[1]).toBeCloseTo(0, 5);
        expect(rotation[3]).toBeCloseTo(1, 5);
    });

    it('interpolates blend1d root deltas between thresholds', () => {
        const { translation, rotation } = extract({
            kind: 'blend1d',
            parameter: 'blend',
            children: [
                { threshold: 0, motion: { kind: 'clip', clipId: 'slow' } },
                { threshold: 1, motion: { kind: 'clip', clipId: 'fast' } },
            ],
        });
        expect(translation[0]).toBeCloseTo(0.25 * 0.5 + 0.5 * 0.5, 5);
        expect(rotation[3]).toBeCloseTo(1, 5);
    });

    it('weights blend2d root deltas by inverse distance', () => {
        const { translation, rotation } = extract({
            kind: 'blend2d',
            parameterX: 'x',
            parameterY: 'y',
            children: [
                { position: [0, 0], motion: { kind: 'clip', clipId: 'slow' } },
                { position: [1, 0], motion: { kind: 'clip', clipId: 'fast' } },
            ],
        });
        expect(translation[0]).toBeCloseTo(0.25, 5);
        expect(rotation[3]).toBeCloseTo(1, 5);
    });

    it('averages direct blend root deltas by normalized weight', () => {
        const { translation, rotation } = extract({
            kind: 'direct',
            children: [
                { motion: { kind: 'clip', clipId: 'slow' }, weight: 1 },
                { motion: { kind: 'clip', clipId: 'fast' }, weight: 3 },
            ],
        });
        expect(translation[0]).toBeCloseTo((0.25 * 1 + 0.5 * 3) / 4, 5);
        expect(rotation[3]).toBeCloseTo(1, 5);
    });

    it('adds weighted additive root deltas onto the base delta', () => {
        const { translation, rotation } = extract({
            kind: 'additive',
            base: { kind: 'clip', clipId: 'slow' },
            additive: { kind: 'clip', clipId: 'fast' },
            weight: 0.5,
        });
        expect(translation[0]).toBeCloseTo(0.25 + 0.5 * 0.5, 5);
        expect(rotation[3]).toBeCloseTo(1, 5);
    });
});
