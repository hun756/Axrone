import { describe, expect, it } from 'vitest';
import { Mat4 } from '@axrone/numeric';
import { RenderFrameClassifier, type RenderFrameClassifierOptions } from '../render-frame-classifier';
import { ReusableList } from '../memory';
import type { RenderFrameInput, RenderPrimitiveInstance, RenderLight, RenderReflectionProbe } from '../types';

const defaultOptions: RenderFrameClassifierOptions = {
    maxTransparentPrimitives: 64,
    maxActiveLocalLights: 8,
    maxActiveReflectionProbes: 4,
    maxShadowedLights: 2,
};

const makeCamera = (overrides: Partial<RenderFrameInput['camera']> = {}) => ({
    id: 'cam:main',
    viewMatrix: new Mat4(),
    projectionMatrix: new Mat4(),
    position: [0, 0, 0] as const,
    near: 0.1,
    far: 1000,
    clearState: { color: [0, 0, 0, 1] as const, depth: 1 },
    ...overrides,
});

const makeOpaquePrimitive = (id: string, overrides: Partial<RenderPrimitiveInstance> = {}): RenderPrimitiveInstance => ({
    id: `prim:${id}`,
    meshId: `mesh:${id}`,
    worldMatrix: new Mat4(),
    material: {
        id: `mat:${id}`,
        model: 'pbr',
        renderQueue: 2000,
        castsShadows: true,
    },
    ...overrides,
} as RenderPrimitiveInstance);

const makeTransparentPrimitive = (id: string): RenderPrimitiveInstance => ({
    id: `prim:${id}`,
    meshId: `mesh:${id}`,
    worldMatrix: new Mat4(),
    material: {
        id: `mat:${id}`,
        model: 'pbr',
        transparent: true,
        renderQueue: 3000,
        castsShadows: false,
    },
} as RenderPrimitiveInstance);

const makeDirectionalLight = (id: string, castsShadows = false): RenderLight => ({
    id: `light:${id}`,
    type: 'directional',
    intensity: 1,
    castsShadows,
} as RenderLight);

const makePointLight = (id: string, x: number, intensity = 1, range = 10): RenderLight => ({
    id: `light:${id}`,
    type: 'point',
    position: [x, 0, 0] as const,
    intensity,
    range,
    castsShadows: false,
} as RenderLight);

const makeProbe = (id: string, overrides: Partial<RenderReflectionProbe> = {}): RenderReflectionProbe => ({
    id: `probe:${id}`,
    position: [0, 0, 0] as const,
    intensity: 1,
    mode: 'realtime',
    priority: 0,
    updateInterval: 30,
    ...overrides,
} as RenderReflectionProbe);

const makeFrameInput = (overrides: Partial<RenderFrameInput> = {}): RenderFrameInput => ({
    camera: makeCamera(),
    primitives: [],
    lights: [],
    environment: undefined,
    ...overrides,
});

describe('RenderFrameClassifier', () => {
    describe('primitive classification', () => {
        it('separates opaque and transparent primitives', () => {
            const classifier = new RenderFrameClassifier(defaultOptions);
            const warnings = new ReusableList<string>();
            const input = makeFrameInput({
                primitives: [
                    makeOpaquePrimitive('a'),
                    makeTransparentPrimitive('b'),
                    makeOpaquePrimitive('c'),
                ],
            });

            classifier.classify(input, 0, warnings);
            expect(classifier.opaque.length).toBe(2);
            expect(classifier.transparent.length).toBe(1);
        });

        it('skips invisible primitives', () => {
            const classifier = new RenderFrameClassifier(defaultOptions);
            const warnings = new ReusableList<string>();
            const input = makeFrameInput({
                primitives: [
                    makeOpaquePrimitive('a'),
                    makeOpaquePrimitive('b', { visible: false } as any),
                ],
            });

            classifier.classify(input, 0, warnings);
            expect(classifier.opaque.length).toBe(1);
        });

        it('skips primitives not matching camera layer mask', () => {
            const classifier = new RenderFrameClassifier(defaultOptions);
            const warnings = new ReusableList<string>();
            const camera = makeCamera({ layerMask: 0x01 });
            const input = makeFrameInput({
                camera,
                primitives: [
                    makeOpaquePrimitive('visible', { layerMask: 0x01 } as any),
                    makeOpaquePrimitive('hidden', { layerMask: 0x02 } as any),
                ],
            });

            classifier.classify(input, 0, warnings);
            expect(classifier.opaque.length).toBe(1);
        });

        it('collects shadow casters', () => {
            const classifier = new RenderFrameClassifier(defaultOptions);
            const warnings = new ReusableList<string>();
            const input = makeFrameInput({
                primitives: [
                    makeOpaquePrimitive('a'),
                    makeOpaquePrimitive('b', { material: { id: 'mat:b', model: 'pbr', castsShadows: false } } as any),
                ],
            });

            classifier.classify(input, 0, warnings);
            expect(classifier.shadowCasters.length).toBe(1);
        });

        it('warns when transparent budget exceeded', () => {
            const classifier = new RenderFrameClassifier({ ...defaultOptions, maxTransparentPrimitives: 2 });
            const warnings = new ReusableList<string>();
            const primitives = [
                makeTransparentPrimitive('a'),
                makeTransparentPrimitive('b'),
                makeTransparentPrimitive('c'),
            ];
            classifier.classify(makeFrameInput({ primitives }), 0, warnings);
            expect(warnings.length).toBeGreaterThan(0);
            expect(classifier.transparent.length).toBe(2);
        });
    });

    describe('light classification', () => {
        it('includes directional lights automatically', () => {
            const classifier = new RenderFrameClassifier(defaultOptions);
            const warnings = new ReusableList<string>();
            const input = makeFrameInput({
                lights: [makeDirectionalLight('sun')],
            });

            classifier.classify(input, 0, warnings);
            expect(classifier.activeLights.length).toBe(1);
        });

        it('adds shadow-casting directional to shadowLights', () => {
            const classifier = new RenderFrameClassifier(defaultOptions);
            const warnings = new ReusableList<string>();
            const input = makeFrameInput({
                lights: [makeDirectionalLight('sun', true)],
            });

            classifier.classify(input, 0, warnings);
            expect(classifier.shadowLights.length).toBe(1);
        });

        it('limits shadow lights to maxShadowedLights', () => {
            const classifier = new RenderFrameClassifier({ ...defaultOptions, maxShadowedLights: 1 });
            const warnings = new ReusableList<string>();
            const input = makeFrameInput({
                lights: [
                    makeDirectionalLight('a', true),
                    makeDirectionalLight('b', true),
                ],
            });

            classifier.classify(input, 0, warnings);
            expect(classifier.shadowLights.length).toBe(1);
        });

        it('sorts local lights by importance and caps at maxActiveLocalLights', () => {
            const classifier = new RenderFrameClassifier({ ...defaultOptions, maxActiveLocalLights: 2 });
            const warnings = new ReusableList<string>();
            const input = makeFrameInput({
                lights: [
                    makePointLight('far', 100, 1, 10),
                    makePointLight('close', 1, 5, 10),
                    makePointLight('mid', 10, 2, 10),
                ],
            });

            classifier.classify(input, 0, warnings);
            expect(classifier.activeLights.length).toBe(2);
        });
    });

    describe('probe classification', () => {
        it('classifies probes by urgency', () => {
            const classifier = new RenderFrameClassifier(defaultOptions);
            const warnings = new ReusableList<string>();
            const input = makeFrameInput({
                environment: {
                    reflectionProbes: [
                        makeProbe('old', { lastUpdatedFrame: 0, dirty: true }),
                        makeProbe('new', { lastUpdatedFrame: 999 }),
                    ],
                },
            });

            classifier.classify(input, 1000, warnings);
            expect(classifier.activeProbes.length).toBe(2);
        });

        it('marks probes for update when dirty or interval elapsed', () => {
            const classifier = new RenderFrameClassifier(defaultOptions);
            const warnings = new ReusableList<string>();
            const input = makeFrameInput({
                environment: {
                    reflectionProbes: [
                        makeProbe('dirty', { dirty: true, mode: 'realtime' }),
                        makeProbe('stale', { lastUpdatedFrame: 0, updateInterval: 10, mode: 'realtime' }),
                        makeProbe('baked', { mode: 'baked' }),
                    ],
                },
            });

            classifier.classify(input, 100, warnings);
            expect(classifier.probeUpdates.length).toBe(2);
        });

        it('does not update baked probes', () => {
            const classifier = new RenderFrameClassifier(defaultOptions);
            const warnings = new ReusableList<string>();
            const input = makeFrameInput({
                environment: {
                    reflectionProbes: [makeProbe('baked', { mode: 'baked', dirty: true })],
                },
            });

            classifier.classify(input, 100, warnings);
            expect(classifier.probeUpdates.length).toBe(0);
        });
    });

    describe('reset / clear', () => {
        it('reset zeroes all list lengths', () => {
            const classifier = new RenderFrameClassifier(defaultOptions);
            const warnings = new ReusableList<string>();
            classifier.classify(makeFrameInput({
                primitives: [makeOpaquePrimitive('a')],
                lights: [makeDirectionalLight('sun')],
            }), 0, warnings);

            classifier.reset();
            expect(classifier.opaque.length).toBe(0);
            expect(classifier.activeLights.length).toBe(0);
        });

        it('clear zeroes everything including string cache', () => {
            const classifier = new RenderFrameClassifier(defaultOptions);
            const warnings = new ReusableList<string>();
            classifier.classify(makeFrameInput({
                primitives: [makeOpaquePrimitive('a')],
            }), 0, warnings);

            classifier.clear();
            expect(classifier.opaque.length).toBe(0);
        });
    });
});
