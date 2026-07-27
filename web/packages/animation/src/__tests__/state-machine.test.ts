import { describe, expect, it } from 'vitest';
import { AnimationClip } from '../clip';
import { AnimationController } from '../controller';
import { AnimationStateMachineError } from '../errors';
import { AnimationParameterStore } from '../parameters';
import { AnimationCurveLayout } from '../pose';
import { AnimationRig } from '../rig';
import {
    compileStateMachine,
    commitLayerRuntime,
    createLayerRuntime,
    updateLayerRuntime,
} from '../state-machine';
import type { AnimationStateMachineDefinition, AnimationTransitionDefinition, AnimationTransitionOperator } from '../types';

const rig = new AnimationRig({ bones: [{ name: 'root' }] });
const curveLayout = new AnimationCurveLayout();

const makeClip = (id: string): AnimationClip =>
    new AnimationClip(
        {
            id,
            tracks: [
                {
                    target: 'root',
                    path: 'translation',
                    times: [0, 1],
                    values: [0, 0, 0, 1, 0, 0],
                },
            ],
        },
        rig,
        curveLayout
    );

const clips = new Map<string, AnimationClip>([
    ['a', makeClip('a')],
    ['b', makeClip('b')],
    ['c', makeClip('c')],
]);

const makeParameters = (): AnimationParameterStore =>
    new AnimationParameterStore([
        { name: 'toB', kind: 'trigger' },
        { name: 'toC', kind: 'trigger' },
    ]);

const makeMachineDefinition = (
    toBOverrides: Partial<AnimationTransitionDefinition> = {}
): AnimationStateMachineDefinition => ({
    entryState: 'A',
    states: [
        {
            id: 'A',
            motion: { kind: 'clip', clipId: 'a' },
            transitions: [
                {
                    to: 'B',
                    duration: 1,
                    fixedDuration: true,
                    conditions: [{ kind: 'trigger', parameter: 'toB' }],
                    ...toBOverrides,
                },
                {
                    to: 'C',
                    duration: 0.5,
                    fixedDuration: true,
                    conditions: [{ kind: 'trigger', parameter: 'toC' }],
                },
            ],
        },
        { id: 'B', motion: { kind: 'clip', clipId: 'b' } },
        { id: 'C', motion: { kind: 'clip', clipId: 'c' } },
    ],
});

describe('state machine transition interruption (canInterrupt)', () => {
    it('keeps non-interruptible transitions running when other conditions fire', () => {
        const machine = compileStateMachine(makeMachineDefinition({ canInterrupt: false }), clips);
        const runtime = createLayerRuntime(machine);
        const parameters = makeParameters();

        parameters.setTrigger('toB');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        expect(runtime.transition?.targetStateIndex).toBe(1);

        parameters.setTrigger('toC');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        expect(runtime.transition?.targetStateIndex).toBe(1);
        expect(runtime.transition?.sourceStateIndex).toBe(0);
    });

    it('interrupts an interruptible transition toward a different target and consumes its trigger', () => {
        const machine = compileStateMachine(makeMachineDefinition({ canInterrupt: true }), clips);
        const runtime = createLayerRuntime(machine);
        const parameters = makeParameters();

        parameters.setTrigger('toB');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        expect(runtime.transition?.targetStateIndex).toBe(1);

        parameters.setTrigger('toC');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        expect(runtime.transition?.targetStateIndex).toBe(2);
        expect(runtime.transition?.sourceStateIndex).toBe(0);
        expect(runtime.transition?.progress).toBe(0);
        expect(parameters.get('toC')).toBe(false);
    });

    it('sources the interrupting transition from the previous target once progress reaches 0.5', () => {
        const machine = compileStateMachine(makeMachineDefinition({ canInterrupt: true }), clips);
        const runtime = createLayerRuntime(machine);
        const parameters = makeParameters();

        parameters.setTrigger('toB');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        updateLayerRuntime(machine, runtime, parameters, 0.5);
        expect(runtime.transition?.progress).toBeCloseTo(0.5, 5);

        parameters.setTrigger('toC');
        updateLayerRuntime(machine, runtime, parameters, 0.05);
        expect(runtime.transition?.sourceStateIndex).toBe(1);
        expect(runtime.transition?.targetStateIndex).toBe(2);
    });

    it('does not restart an interruptible transition toward the same target', () => {
        const machine = compileStateMachine(makeMachineDefinition({ canInterrupt: true }), clips);
        const runtime = createLayerRuntime(machine);
        const parameters = makeParameters();

        parameters.setTrigger('toB');
        updateLayerRuntime(machine, runtime, parameters, 0.1);

        parameters.setTrigger('toB');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        expect(runtime.transition?.targetStateIndex).toBe(1);
        expect(runtime.transition?.sourceStateIndex).toBe(0);
        expect(runtime.transition?.progress).toBeCloseTo(0.1, 5);
    });

    it('commits interrupted transitions to their final target', () => {
        const machine = compileStateMachine(makeMachineDefinition({ canInterrupt: true }), clips);
        const runtime = createLayerRuntime(machine);
        const parameters = makeParameters();

        parameters.setTrigger('toB');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        parameters.setTrigger('toC');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        updateLayerRuntime(machine, runtime, parameters, 1);
        expect(runtime.transition?.complete).toBe(true);
        commitLayerRuntime(runtime);
        expect(runtime.transition).toBeNull();
        expect(runtime.currentStateIndex).toBe(2);
    });
});

describe('state machine condition evaluation', () => {
    it('prefers higher priority transitions when several match', () => {
        const machine = compileStateMachine(
            {
                entryState: 'A',
                states: [
                    {
                        id: 'A',
                        motion: { kind: 'clip', clipId: 'a' },
                        transitions: [
                            {
                                to: 'B',
                                priority: 1,
                                conditions: [{ kind: 'trigger', parameter: 'toB' }],
                            },
                            {
                                to: 'C',
                                priority: 5,
                                conditions: [{ kind: 'trigger', parameter: 'toB' }],
                            },
                        ],
                    },
                    { id: 'B', motion: { kind: 'clip', clipId: 'b' } },
                    { id: 'C', motion: { kind: 'clip', clipId: 'c' } },
                ],
            },
            clips
        );
        const runtime = createLayerRuntime(machine);
        const parameters = makeParameters();

        parameters.setTrigger('toB');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        expect(runtime.transition?.targetStateIndex).toBe(2);
    });

    it('throws on unsupported runtime operators instead of failing silently', () => {
        const machine = compileStateMachine(
            {
                entryState: 'A',
                states: [
                    {
                        id: 'A',
                        motion: { kind: 'clip', clipId: 'a' },
                        transitions: [
                            {
                                to: 'B',
                                conditions: [
                                    {
                                        kind: 'float',
                                        parameter: 'speed',
                                        operator: '~=' as unknown as AnimationTransitionOperator,
                                        value: 1,
                                    },
                                ],
                            },
                        ],
                    },
                    { id: 'B', motion: { kind: 'clip', clipId: 'b' } },
                ],
            },
            clips
        );
        const runtime = createLayerRuntime(machine);
        const parameters = new AnimationParameterStore([{ name: 'speed', kind: 'float' }]);

        expect(() => updateLayerRuntime(machine, runtime, parameters, 0.1)).toThrow(
            AnimationStateMachineError
        );
    });
});

describe('controller seek', () => {
    it('normalizes seek time against blend tree durations', () => {
        const controller = new AnimationController({
            rig: { bones: [{ name: 'root' }] },
            clips: [
                {
                    id: 'slow',
                    tracks: [
                        { target: 'root', path: 'translation', times: [0, 2], values: [0, 0, 0, 1, 0, 0] },
                    ],
                },
                {
                    id: 'fast',
                    tracks: [
                        { target: 'root', path: 'translation', times: [0, 4], values: [0, 0, 0, 2, 0, 0] },
                    ],
                },
            ],
            parameters: [{ name: 'blend', kind: 'float', defaultValue: 0.5 }],
            layers: [
                {
                    id: 'base',
                    stateMachine: {
                        entryState: 'move',
                        states: [
                            {
                                id: 'move',
                                motion: {
                                    kind: 'blend1d',
                                    parameter: 'blend',
                                    children: [
                                        { threshold: 0, motion: { kind: 'clip', clipId: 'slow' } },
                                        { threshold: 1, motion: { kind: 'clip', clipId: 'fast' } },
                                    ],
                                },
                            },
                        ],
                    },
                },
            ],
        });

        // Blended duration = 2 * 0.5 + 4 * 0.5 = 3 seconds.
        controller.seek(1.5);
        expect(controller.profile.activeLayers[0]?.normalizedTime).toBeCloseTo(0.5, 5);
    });
});
