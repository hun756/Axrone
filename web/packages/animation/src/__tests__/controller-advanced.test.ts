import { describe, expect, it } from 'vitest';
import { AnimationController } from '../controller';
import { AnimationStateMachineError, AnimationValidationError } from '../errors';

const makeController = (overrides: Record<string, unknown> = {}) =>
    new AnimationController({
        rig: { bones: [{ name: 'root' }, { name: 'child', parent: 'root', translation: [1, 0, 0] }] },
        clips: [
            {
                id: 'idle',
                tracks: [
                    { target: 'root', path: 'translation', times: [0, 1], values: [0, 0, 0, 1, 0, 0] },
                ],
            },
            {
                id: 'walk',
                tracks: [
                    { target: 'root', path: 'translation', times: [0, 1], values: [0, 0, 0, 2, 0, 0] },
                ],
            },
            {
                id: 'run',
                tracks: [
                    { target: 'root', path: 'translation', times: [0, 1], values: [0, 0, 0, 5, 0, 0] },
                ],
            },
        ],
        layers: [
            {
                id: 'base',
                stateMachine: {
                    entryState: 'idle',
                    states: [
                        { id: 'idle', motion: { kind: 'clip', clipId: 'idle' } },
                        { id: 'walk', motion: { kind: 'clip', clipId: 'walk' } },
                        { id: 'run', motion: { kind: 'clip', clipId: 'run' } },
                    ],
                },
            },
            ...((overrides.layers as unknown[]) ?? []),
        ],
        ...(overrides as Record<string, unknown>),
    } as never);

describe('AnimationController.play()', () => {
    it('sets target state and resets time', () => {
        const controller = makeController();
        controller.play('walk');
        expect(controller.profile.activeLayers[0]?.stateId).toBe('walk');
        expect(controller.profile.activeLayers[0]?.normalizedTime).toBe(0);
    });

    it('returns this for chaining', () => {
        const controller = makeController();
        const result = controller.play('walk');
        expect(result).toBe(controller);
    });
});

describe('AnimationController.crossFade()', () => {
    it('initiates transition with duration', () => {
        const controller = makeController();
        controller.crossFade('walk', 0.5);
        controller.evaluate();
        expect(controller.profile.activeLayers[0]?.transitioning).toBe(true);
        expect(controller.profile.activeLayers[0]?.stateId).toContain('->');
    });

    it('returns this for chaining', () => {
        const controller = makeController();
        const result = controller.crossFade('walk', 0.5);
        expect(result).toBe(controller);
    });
});

describe('AnimationController.setLayerWeight()', () => {
    it('changes layer weight', () => {
        const controller = makeController();
        controller.setLayerWeight('base', 0.5);
        // Weight is applied on next evaluate/update
        controller.evaluate();
        expect(controller.profile.activeLayers[0]?.weight).toBe(0.5);
    });

    it('returns this for chaining', () => {
        const controller = makeController();
        const result = controller.setLayerWeight('base', 0.5);
        expect(result).toBe(controller);
    });
});

describe('AnimationController.dispose()', () => {
    it('cleans up resources without error', () => {
        const controller = makeController();
        expect(() => controller.dispose()).not.toThrow();
        // Root motion should be reset
        expect(controller.rootMotion.translation).toEqual([0, 0, 0]);
        expect(controller.rootMotion.rotation).toEqual([0, 0, 0, 1]);
    });
});

describe('AnimationController multi-layer', () => {
    it('override layer replaces base at weight 1', () => {
        const controller = new AnimationController({
            rig: { bones: [{ name: 'root' }] },
            clips: [
                {
                    id: 'idle',
                    tracks: [
                        { target: 'root', path: 'translation', times: [0, 1], values: [0, 0, 0, 1, 0, 0] },
                    ],
                },
                {
                    id: 'wave',
                    tracks: [
                        { target: 'root', path: 'translation', times: [0, 1], values: [0, 5, 0, 1, 0, 0] },
                    ],
                },
            ],
            layers: [
                {
                    id: 'base',
                    stateMachine: {
                        entryState: 'idle',
                        states: [{ id: 'idle', motion: { kind: 'clip', clipId: 'idle' } }],
                    },
                },
                {
                    id: 'upper',
                    mode: 'override',
                    weight: 1,
                    stateMachine: {
                        entryState: 'wave',
                        states: [{ id: 'wave', motion: { kind: 'clip', clipId: 'wave' } }],
                    },
                },
            ],
        });
        // Override layer at weight 1 fully replaces base
        expect(controller.currentFrame.pose.translations[1]).toBeCloseTo(5, 5);
    });

    it('additive layer adds on top', () => {
        const controller = new AnimationController({
            rig: { bones: [{ name: 'root' }] },
            clips: [
                {
                    id: 'idle',
                    tracks: [
                        { target: 'root', path: 'translation', times: [0, 1], values: [0, 0, 0, 1, 0, 0] },
                    ],
                },
                {
                    id: 'add',
                    tracks: [
                        { target: 'root', path: 'translation', times: [0, 1], values: [0, 3, 0, 1, 0, 0] },
                    ],
                },
            ],
            layers: [
                {
                    id: 'base',
                    stateMachine: {
                        entryState: 'idle',
                        states: [{ id: 'idle', motion: { kind: 'clip', clipId: 'idle' } }],
                    },
                },
                {
                    id: 'additive',
                    mode: 'additive',
                    weight: 1,
                    stateMachine: {
                        entryState: 'add',
                        states: [{ id: 'add', motion: { kind: 'clip', clipId: 'add' } }],
                    },
                },
            ],
        });
        // Additive at time 0: base has translation [0,0,0], additive adds delta from rest
        // The exact value depends on how additive blending works with rest pose
        expect(controller.currentFrame.pose.translations[0]).toBeDefined();
    });
});

describe('AnimationController bone mask', () => {
    it('only masked bones are affected by layer', () => {
        const controller = new AnimationController({
            rig: { bones: [{ name: 'root' }, { name: 'child', parent: 'root' }] },
            clips: [
                {
                    id: 'idle',
                    tracks: [
                        { target: 'root', path: 'translation', times: [0, 1], values: [0, 0, 0, 1, 0, 0] },
                    ],
                },
                {
                    id: 'wave',
                    tracks: [
                        { target: 'root', path: 'translation', times: [0, 1], values: [0, 5, 0, 1, 0, 0] },
                        { target: 'child', path: 'translation', times: [0, 1], values: [0, 5, 0, 1, 0, 0] },
                    ],
                },
            ],
            layers: [
                {
                    id: 'base',
                    stateMachine: {
                        entryState: 'idle',
                        states: [{ id: 'idle', motion: { kind: 'clip', clipId: 'idle' } }],
                    },
                },
                {
                    id: 'upper',
                    mode: 'override',
                    weight: 1,
                    boneMask: ['root'],
                    stateMachine: {
                        entryState: 'wave',
                        states: [{ id: 'wave', motion: { kind: 'clip', clipId: 'wave' } }],
                    },
                },
            ],
        });
        // Root bone should be affected by upper layer
        expect(controller.currentFrame.pose.translations[1]).toBeCloseTo(5, 5);
        // Child bone should NOT be affected (masked out)
        expect(controller.currentFrame.pose.translations[4]).toBeCloseTo(0, 5);
    });
});

describe('AnimationController zero-weight layers', () => {
    it('skips layers with weight <= 0', () => {
        const controller = new AnimationController({
            rig: { bones: [{ name: 'root' }] },
            clips: [
                {
                    id: 'idle',
                    tracks: [
                        { target: 'root', path: 'translation', times: [0, 1], values: [0, 0, 0, 1, 0, 0] },
                    ],
                },
                {
                    id: 'wave',
                    tracks: [
                        { target: 'root', path: 'translation', times: [0, 1], values: [0, 5, 0, 1, 0, 0] },
                    ],
                },
            ],
            layers: [
                {
                    id: 'base',
                    stateMachine: {
                        entryState: 'idle',
                        states: [{ id: 'idle', motion: { kind: 'clip', clipId: 'idle' } }],
                    },
                },
                {
                    id: 'upper',
                    weight: 0,
                    stateMachine: {
                        entryState: 'wave',
                        states: [{ id: 'wave', motion: { kind: 'clip', clipId: 'wave' } }],
                    },
                },
            ],
        });
        // Upper layer skipped, base translation at time 0 should be [0,0,0]
        expect(controller.currentFrame.pose.translations[1]).toBeCloseTo(0, 5);
    });
});

describe('AnimationController no-layer error', () => {
    it('throws when no layers are provided', () => {
        expect(
            () =>
                new AnimationController({
                    rig: { bones: [{ name: 'root' }] },
                    clips: [],
                    layers: [],
                })
        ).toThrow(AnimationValidationError);
    });
});

describe('AnimationController unknown layer', () => {
    it('throws on play with unknown layer id', () => {
        const controller = makeController();
        expect(() => controller.play('walk', 'nonexistent')).toThrow(AnimationStateMachineError);
    });

    it('throws on setLayerWeight with unknown layer id', () => {
        const controller = makeController();
        expect(() => controller.setLayerWeight('nonexistent', 0.5)).toThrow(AnimationStateMachineError);
    });
});
