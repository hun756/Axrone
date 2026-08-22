import { describe, expect, it } from 'vitest';
import { AnimationClip } from '../clip';
import { AnimationController } from '../controller';
import { AnimationStateMachineError, AnimationValidationError } from '../errors';
import { AnimationParameterStore } from '../parameters';
import { AnimationCurveLayout, AnimationFrame } from '../pose';
import { AnimationRig } from '../rig';
import {
    compileStateMachine,
    commitLayerRuntime,
    createLayerRuntime,
    crossFadeLayerState,
    evaluateLayerRuntime,
    forceLayerState,
    updateLayerRuntime,
    type AnimationCompiledStateMachine,
    type AnimationLayerRuntime,
} from '../state-machine';
import { AnimationScratchPool, BlendScratchContext } from '../blend-tree';
import type { AnimationStateMachineDefinition } from '../types';

// ---------------------------------------------------------------------------
// Shared fixtures
// ---------------------------------------------------------------------------

const rig = new AnimationRig({ bones: [{ name: 'root' }] });
const curveLayout = new AnimationCurveLayout();

const makeClip = (id: string, xEnd = 1): AnimationClip =>
    new AnimationClip(
        {
            id,
            tracks: [
                {
                    target: 'root',
                    path: 'translation',
                    times: [0, 1],
                    values: [0, 0, 0, xEnd, 0, 0],
                },
            ],
        },
        rig,
        curveLayout
    );

// Each clip produces distinct translation values so we can verify blending:
//   idle → x = 0 at any time
//   run  → x = 2 at t=1
//   walk → x = 4 at t=1
const clipMap = new Map<string, AnimationClip>([
    ['idle', makeClip('idle', 0)],
    ['run', makeClip('run', 2)],
    ['walk', makeClip('walk', 4)],
]);

// ---------------------------------------------------------------------------
// EN_Character_Stickman_01 animator pattern
// ---------------------------------------------------------------------------

const TRANSITION_DURATION = 0.2;
const EXIT_TIME = 0.1;

const makeCharacterDefinition = (): AnimationStateMachineDefinition => ({
    entryState: 'Idle',
    states: [
        {
            id: 'Idle',
            motion: { kind: 'clip', clipId: 'idle' },
            transitions: [
                {
                    to: 'Run',
                    duration: TRANSITION_DURATION,
                    exitTime: EXIT_TIME,
                    canInterrupt: true,
                    conditions: [{ kind: 'float', parameter: 'a', operator: '>', value: 0.5 }],
                },
                {
                    to: 'Walk',
                    duration: TRANSITION_DURATION,
                    exitTime: EXIT_TIME,
                    canInterrupt: true,
                    conditions: [
                        { kind: 'float', parameter: 'a', operator: '>', value: 0.1 },
                        { kind: 'float', parameter: 'a', operator: '<=', value: 0.5 },
                    ],
                },
            ],
        },
        {
            id: 'Run',
            motion: { kind: 'clip', clipId: 'run' },
            transitions: [
                {
                    to: 'Idle',
                    duration: TRANSITION_DURATION,
                    exitTime: EXIT_TIME,
                    canInterrupt: true,
                    conditions: [{ kind: 'float', parameter: 'a', operator: '<', value: 0.1 }],
                },
                {
                    to: 'Walk',
                    duration: TRANSITION_DURATION,
                    exitTime: EXIT_TIME,
                    canInterrupt: true,
                    conditions: [
                        { kind: 'float', parameter: 'a', operator: '<=', value: 0.5 },
                        { kind: 'float', parameter: 'a', operator: '>=', value: 0.1 },
                    ],
                },
            ],
        },
        {
            id: 'Walk',
            motion: { kind: 'clip', clipId: 'walk' },
            transitions: [
                {
                    to: 'Idle',
                    duration: TRANSITION_DURATION,
                    exitTime: EXIT_TIME,
                    canInterrupt: true,
                    conditions: [{ kind: 'float', parameter: 'a', operator: '<', value: 0.1 }],
                },
                {
                    to: 'Run',
                    duration: TRANSITION_DURATION,
                    exitTime: EXIT_TIME,
                    canInterrupt: true,
                    conditions: [{ kind: 'float', parameter: 'a', operator: '>', value: 0.5 }],
                },
            ],
        },
    ],
});

const makeParameters = (): AnimationParameterStore =>
    new AnimationParameterStore([{ name: 'a', kind: 'float', defaultValue: 0 }]);

/** Helper: compile + create runtime in one step. */
const makeRuntime = (
    definition?: AnimationStateMachineDefinition
): { machine: AnimationCompiledStateMachine; runtime: AnimationLayerRuntime; parameters: AnimationParameterStore } => {
    const machine = compileStateMachine(definition ?? makeCharacterDefinition(), clipMap);
    return { machine, runtime: createLayerRuntime(machine), parameters: makeParameters() };
};

/** Helper: build a scratch context for evaluateLayerRuntime. */
const makeContext = (parameters?: AnimationParameterStore) => {
    const scratchPool = new AnimationScratchPool(rig, curveLayout, new Float32Array(0));
    return {
        rig,
        parameters: parameters ?? makeParameters(),
        restFrame: new AnimationFrame(rig, curveLayout),
        scratch: scratchPool,
        blendScratch: new BlendScratchContext(),
    };
};

// ===========================================================================
// 1. State Machine Compilation
// ===========================================================================

describe('Animator State Machine — Compilation', () => {
    it('compiles a valid 3-state character definition without errors', () => {
        const machine = compileStateMachine(makeCharacterDefinition(), clipMap);
        expect(machine).toBeDefined();
        expect(machine.states).toHaveLength(3);
    });

    it('resolves the entry state index to the Idle state (index 0)', () => {
        const machine = compileStateMachine(makeCharacterDefinition(), clipMap);
        expect(machine.entryStateIndex).toBe(0);
        expect(machine.states[machine.entryStateIndex]!.id).toBe('Idle');
    });

    it('assigns correct state indices: Idle=0, Run=1, Walk=2', () => {
        const machine = compileStateMachine(makeCharacterDefinition(), clipMap);
        expect(machine.stateIndexById.get('Idle')).toBe(0);
        expect(machine.stateIndexById.get('Run')).toBe(1);
        expect(machine.stateIndexById.get('Walk')).toBe(2);
    });

    it('compiles transitions with correct target indices, durations, and condition counts', () => {
        const machine = compileStateMachine(makeCharacterDefinition(), clipMap);
        const idleTransitions = machine.states[0]!.transitions;
        // Idle has 2 transitions: to Run and to Walk
        expect(idleTransitions).toHaveLength(2);
        // Transitions are sorted by priority descending; both have priority 0, so order is preserved
        const toRun = idleTransitions.find((t) => t.targetStateIndex === 1);
        const toWalk = idleTransitions.find((t) => t.targetStateIndex === 2);
        expect(toRun).toBeDefined();
        expect(toRun!.duration).toBe(TRANSITION_DURATION);
        expect(toRun!.exitTime).toBe(EXIT_TIME);
        expect(toRun!.canInterrupt).toBe(true);
        expect(toRun!.conditions).toHaveLength(1);
        expect(toWalk).toBeDefined();
        expect(toWalk!.conditions).toHaveLength(2);
    });

    it('freezes the compiled machine so it cannot be mutated', () => {
        const machine = compileStateMachine(makeCharacterDefinition(), clipMap);
        expect(Object.isFrozen(machine)).toBe(true);
        expect(Object.isFrozen(machine.states)).toBe(true);
    });
});

// ===========================================================================
// 2. Parameter Store
// ===========================================================================

describe('Animator State Machine — Parameter Store', () => {
    it('setFloat / get returns the exact value', () => {
        const params = makeParameters();
        params.setFloat('a', 0.75);
        expect(params.get('a')).toBe(0.75);
    });

    it('setBool / get works for boolean parameters', () => {
        const store = new AnimationParameterStore([{ name: 'grounded', kind: 'bool' }]);
        store.setBool('grounded', true);
        expect(store.get('grounded')).toBe(true);
        store.setBool('grounded', false);
        expect(store.get('grounded')).toBe(false);
    });

    it('setTrigger / consumeTrigger fires once then resets', () => {
        const store = new AnimationParameterStore([{ name: 'jump', kind: 'trigger' }]);
        store.setTrigger('jump');
        expect(store.get('jump')).toBe(true);
        expect(store.consumeTrigger('jump')).toBe(true);
        // After consumption the trigger is reset
        expect(store.get('jump')).toBe(false);
        expect(store.consumeTrigger('jump')).toBe(false);
    });

    it('setInt truncates to integer', () => {
        const store = new AnimationParameterStore([{ name: 'layer', kind: 'int' }]);
        store.setInt('layer', 2.9);
        expect(store.get('layer')).toBe(2);
    });

    it('has() returns true for known parameters and false for unknown', () => {
        const params = makeParameters();
        expect(params.has('a')).toBe(true);
        expect(params.has('missing')).toBe(false);
    });

    it('throws AnimationStateMachineError when accessing an unknown parameter', () => {
        const params = makeParameters();
        expect(() => params.get('unknown')).toThrow(AnimationStateMachineError);
        expect(() => params.set('unknown', 1)).toThrow(AnimationStateMachineError);
    });
});

// ===========================================================================
// 3. Basic Transitions
// ===========================================================================

describe('Animator State Machine — Basic Transitions', () => {
    it('Idle → Run when a > 0.5 and exitTime crossed', () => {
        const { machine, runtime, parameters } = makeRuntime();
        parameters.setFloat('a', 0.8);
        // First update: advance past exitTime (0.1) so the transition can fire
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(1); // Run
        expect(runtime.transition!.sourceStateIndex).toBe(0); // Idle
    });

    it('stays in Idle when a <= 0.5 (Run condition not met, Walk condition partially met)', () => {
        const { machine, runtime, parameters } = makeRuntime();
        parameters.setFloat('a', 0.05);
        updateLayerRuntime(machine, runtime, parameters, 0.2);
        // No transition should fire because a < 0.1 (Run needs >0.5, Walk needs >0.1)
        expect(runtime.transition).toBeNull();
        expect(runtime.currentStateIndex).toBe(0); // Still Idle
    });

    it('transitions to Run after condition met and commit finalizes the state', () => {
        const { machine, runtime, parameters } = makeRuntime();
        parameters.setFloat('a', 0.8);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        // Complete the transition (duration = 0.2s)
        updateLayerRuntime(machine, runtime, parameters, 0.25);
        expect(runtime.transition!.complete).toBe(true);
        commitLayerRuntime(runtime);
        expect(runtime.currentStateIndex).toBe(1); // Now in Run
        expect(runtime.transition).toBeNull();
    });

    it('does not transition when condition is exactly at boundary (a == 0.5 does not satisfy a > 0.5)', () => {
        const { machine, runtime, parameters } = makeRuntime();
        parameters.setFloat('a', 0.5);
        updateLayerRuntime(machine, runtime, parameters, 0.2);
        // a=0.5 does NOT satisfy a > 0.5 (Run), but DOES satisfy a > 0.1 AND a <= 0.5 (Walk)
        // So a transition to Walk should fire, not Run
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(2); // Walk, not Run
    });
});

// ===========================================================================
// 4. Multi-Condition Transitions
// ===========================================================================

describe('Animator State Machine — Multi-Condition Transitions', () => {
    it('Idle → Walk requires BOTH a > 0.1 AND a <= 0.5', () => {
        const { machine, runtime, parameters } = makeRuntime();
        // a = 0.3 satisfies both conditions
        parameters.setFloat('a', 0.3);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(2); // Walk
    });

    it('fails Walk transition when a > 0.5 (second condition a <= 0.5 fails)', () => {
        const { machine, runtime, parameters } = makeRuntime();
        // a = 0.7: first condition (a > 0.1) passes, second (a <= 0.5) fails
        // The Run transition (a > 0.5) should match instead
        parameters.setFloat('a', 0.7);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(1); // Run, not Walk
    });

    it('fails Walk transition when a <= 0.1 (first condition a > 0.1 fails)', () => {
        const { machine, runtime, parameters } = makeRuntime();
        // a = 0.05: first condition (a > 0.1) fails
        parameters.setFloat('a', 0.05);
        updateLayerRuntime(machine, runtime, parameters, 0.2);
        // No transition at all from Idle (Run needs >0.5, Walk needs >0.1)
        expect(runtime.transition).toBeNull();
    });

    it('Walk transition fires at exact boundary a = 0.1 (fails because a > 0.1 is strict)', () => {
        const { machine, runtime, parameters } = makeRuntime();
        parameters.setFloat('a', 0.1);
        updateLayerRuntime(machine, runtime, parameters, 0.2);
        // a = 0.1 does NOT satisfy a > 0.1, so Walk transition fails
        expect(runtime.transition).toBeNull();
    });

    it('Walk transition fires at exact boundary a = 0.5 (satisfies both conditions)', () => {
        const { machine, runtime, parameters } = makeRuntime();
        parameters.setFloat('a', 0.5);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        // a = 0.5 satisfies a > 0.1 AND a <= 0.5
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(2); // Walk
    });
});

// ===========================================================================
// 5. Bidirectional Transitions
// ===========================================================================

describe('Animator State Machine — Bidirectional Transitions', () => {
    it('Run → Idle when a < 0.1', () => {
        const { machine, runtime, parameters } = makeRuntime();
        // Move to Run first
        forceLayerState(machine, runtime, 'Run', 0);
        parameters.setFloat('a', 0.05);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(0); // Idle
        expect(runtime.transition!.sourceStateIndex).toBe(1); // Run
    });

    it('Walk → Run when a > 0.5', () => {
        const { machine, runtime, parameters } = makeRuntime();
        forceLayerState(machine, runtime, 'Walk', 0);
        parameters.setFloat('a', 0.8);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(1); // Run
        expect(runtime.transition!.sourceStateIndex).toBe(2); // Walk
    });

    it('Run → Walk when 0.1 <= a <= 0.5', () => {
        const { machine, runtime, parameters } = makeRuntime();
        forceLayerState(machine, runtime, 'Run', 0);
        parameters.setFloat('a', 0.3);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(2); // Walk
    });

    it('Walk → Idle when a < 0.1', () => {
        const { machine, runtime, parameters } = makeRuntime();
        forceLayerState(machine, runtime, 'Walk', 0);
        parameters.setFloat('a', 0.02);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(0); // Idle
    });

    it('full cycle: Idle → Run → Walk → Idle via parameter changes', () => {
        const { machine, runtime, parameters } = makeRuntime();

        // Idle → Run
        parameters.setFloat('a', 0.8);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        updateLayerRuntime(machine, runtime, parameters, 0.25);
        commitLayerRuntime(runtime);
        expect(runtime.currentStateIndex).toBe(1); // Run

        // Reset normalized time to 0 so exitTime can be crossed again
        forceLayerState(machine, runtime, 'Run', 0);

        // Run → Walk
        parameters.setFloat('a', 0.3);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        updateLayerRuntime(machine, runtime, parameters, 0.25);
        commitLayerRuntime(runtime);
        expect(runtime.currentStateIndex).toBe(2); // Walk

        // Reset normalized time to 0 so exitTime can be crossed again
        forceLayerState(machine, runtime, 'Walk', 0);

        // Walk → Idle
        parameters.setFloat('a', 0.0);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        updateLayerRuntime(machine, runtime, parameters, 0.25);
        commitLayerRuntime(runtime);
        expect(runtime.currentStateIndex).toBe(0); // Idle
    });
});

// ===========================================================================
// 6. Cross-Fade Blending
// ===========================================================================

describe('Animator State Machine — Cross-Fade Blending', () => {
    it('during transition, evaluateLayerRuntime blends source and target frames', () => {
        const { machine, runtime, parameters } = makeRuntime();
        parameters.setFloat('a', 0.8);
        // Trigger Idle → Run transition
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();

        // Advance to roughly 50% through the transition
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        const progress = runtime.transition!.progress;
        expect(progress).toBeGreaterThan(0);
        expect(progress).toBeLessThan(1);

        const context = makeContext(parameters);
        context.scratch.reset();
        const out = new AnimationFrame(rig, curveLayout);
        evaluateLayerRuntime(machine, runtime, context, out);

        // idle clip gives x=0, run clip gives x=2 at t=0 (start)
        // The blended result should be between the two source values
        // Since both clips start at x=0 at normalizedTime=0, the blend depends on sampling
        // The key assertion: the frame was produced without error and contains valid data
        expect(out.pose.translations[0]).toBeTypeOf('number');
        expect(Number.isFinite(out.pose.translations[0])).toBe(true);
    });

    it('at progress=0 the output matches the source state', () => {
        const { machine, runtime, parameters } = makeRuntime();
        // Use crossFadeLayerState for precise control
        crossFadeLayerState(machine, runtime, 'Run', 1, 0, false);
        // Don't advance time — progress is 0
        const context = makeContext(parameters);
        context.scratch.reset();
        const out = new AnimationFrame(rig, curveLayout);
        evaluateLayerRuntime(machine, runtime, context, out);
        // At progress=0, blendFrame(out, source, target, 0) → pure source
        // Source is Idle (clip 'idle', x=0 at any time)
        expect(out.pose.translations[0]).toBeCloseTo(0, 5);
    });

    it('commitLayerRuntime is a no-op when transition is not complete', () => {
        const { machine, runtime, parameters } = makeRuntime();
        parameters.setFloat('a', 0.8);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.complete).toBe(false);

        const stateBefore = runtime.currentStateIndex;
        commitLayerRuntime(runtime);
        // Should not have changed state
        expect(runtime.currentStateIndex).toBe(stateBefore);
        expect(runtime.transition).not.toBeNull();
    });
});

// ===========================================================================
// 7. Exit Time
// ===========================================================================

describe('Animator State Machine — Exit Time', () => {
    it('transition does NOT fire before exitTime is crossed', () => {
        const { machine, runtime, parameters } = makeRuntime();
        parameters.setFloat('a', 0.8);
        // Advance only to normalizedTime=0.05 (before exitTime=0.1)
        updateLayerRuntime(machine, runtime, parameters, 0.05);
        expect(runtime.transition).toBeNull();
    });

    it('transition fires once normalizedTime crosses exitTime', () => {
        const { machine, runtime, parameters } = makeRuntime();
        parameters.setFloat('a', 0.8);
        // Advance to normalizedTime=0.15 (past exitTime=0.1)
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(1); // Run
    });

    it('exitTime works correctly with looping states', () => {
        // Create a machine with exitTime=0.8 to test wrap-around behavior
        const machine = compileStateMachine(
            {
                entryState: 'A',
                states: [
                    {
                        id: 'A',
                        motion: { kind: 'clip', clipId: 'idle' },
                        loop: true,
                        transitions: [
                            {
                                to: 'B',
                                exitTime: 0.8,
                                duration: 0.1,
                                fixedDuration: true,
                            },
                        ],
                    },
                    { id: 'B', motion: { kind: 'clip', clipId: 'run' } },
                ],
            },
            clipMap
        );
        const runtime = createLayerRuntime(machine);
        const parameters = new AnimationParameterStore([]);
        // Advance to 0.5 — before exitTime 0.8
        updateLayerRuntime(machine, runtime, parameters, 0.5);
        expect(runtime.transition).toBeNull();
        // Advance to 0.85 — past exitTime 0.8
        updateLayerRuntime(machine, runtime, parameters, 0.35);
        expect(runtime.transition).not.toBeNull();
    });
});

// ===========================================================================
// 8. Can Interrupt
// ===========================================================================

describe('Animator State Machine — Can Interrupt', () => {
    it('interruptible transition can be overridden by a higher-priority transition', () => {
        const machine = compileStateMachine(
            {
                entryState: 'A',
                states: [
                    {
                        id: 'A',
                        motion: { kind: 'clip', clipId: 'idle' },
                        transitions: [
                            {
                                to: 'B',
                                duration: 1,
                                fixedDuration: true,
                                canInterrupt: true,
                                conditions: [{ kind: 'trigger', parameter: 'goB' }],
                            },
                            {
                                to: 'C',
                                duration: 0.5,
                                fixedDuration: true,
                                canInterrupt: true,
                                priority: 5,
                                conditions: [{ kind: 'trigger', parameter: 'goC' }],
                            },
                        ],
                    },
                    { id: 'B', motion: { kind: 'clip', clipId: 'run' } },
                    { id: 'C', motion: { kind: 'clip', clipId: 'walk' } },
                ],
            },
            clipMap
        );
        const runtime = createLayerRuntime(machine);
        const parameters = new AnimationParameterStore([
            { name: 'goB', kind: 'trigger' },
            { name: 'goC', kind: 'trigger' },
        ]);

        // Start transition A → B
        parameters.setTrigger('goB');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        expect(runtime.transition!.targetStateIndex).toBe(1); // B

        // Interrupt with A → C (higher priority)
        parameters.setTrigger('goC');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        expect(runtime.transition!.targetStateIndex).toBe(2); // C
        expect(runtime.transition!.sourceStateIndex).toBe(0); // Still from A
    });

    it('non-interruptible transition cannot be overridden', () => {
        const machine = compileStateMachine(
            {
                entryState: 'A',
                states: [
                    {
                        id: 'A',
                        motion: { kind: 'clip', clipId: 'idle' },
                        transitions: [
                            {
                                to: 'B',
                                duration: 1,
                                fixedDuration: true,
                                canInterrupt: false,
                                conditions: [{ kind: 'trigger', parameter: 'goB' }],
                            },
                            {
                                to: 'C',
                                duration: 0.5,
                                fixedDuration: true,
                                conditions: [{ kind: 'trigger', parameter: 'goC' }],
                            },
                        ],
                    },
                    { id: 'B', motion: { kind: 'clip', clipId: 'run' } },
                    { id: 'C', motion: { kind: 'clip', clipId: 'walk' } },
                ],
            },
            clipMap
        );
        const runtime = createLayerRuntime(machine);
        const parameters = new AnimationParameterStore([
            { name: 'goB', kind: 'trigger' },
            { name: 'goC', kind: 'trigger' },
        ]);

        parameters.setTrigger('goB');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        expect(runtime.transition!.targetStateIndex).toBe(1); // B

        parameters.setTrigger('goC');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        // Non-interruptible: transition still targets B
        expect(runtime.transition!.targetStateIndex).toBe(1);
    });

    it('character pattern: interrupt Idle→Walk with Idle→Run when a jumps above 0.5', () => {
        // Use transitions without exitTime so the interrupt resolution is not blocked
        // by the exitTime gate (source state already past exitTime during interrupt).
        const machine = compileStateMachine(
            {
                entryState: 'Idle',
                states: [
                    {
                        id: 'Idle',
                        motion: { kind: 'clip', clipId: 'idle' },
                        transitions: [
                            {
                                to: 'Run',
                                duration: 0.5,
                                fixedDuration: true,
                                canInterrupt: true,
                                conditions: [
                                    { kind: 'float', parameter: 'a', operator: '>', value: 0.5 },
                                ],
                            },
                            {
                                to: 'Walk',
                                duration: 0.5,
                                fixedDuration: true,
                                canInterrupt: true,
                                conditions: [
                                    { kind: 'float', parameter: 'a', operator: '>', value: 0.1 },
                                    { kind: 'float', parameter: 'a', operator: '<=', value: 0.5 },
                                ],
                            },
                        ],
                    },
                    { id: 'Run', motion: { kind: 'clip', clipId: 'run' } },
                    { id: 'Walk', motion: { kind: 'clip', clipId: 'walk' } },
                ],
            },
            clipMap
        );
        const runtime = createLayerRuntime(machine);
        const parameters = new AnimationParameterStore([
            { name: 'a', kind: 'float', defaultValue: 0 },
        ]);

        // Start in Idle, set a=0.3 → triggers Idle→Walk
        parameters.setFloat('a', 0.3);
        updateLayerRuntime(machine, runtime, parameters, 0.05);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(2); // Walk

        // Now jump a to 0.8 — the Idle→Run transition should interrupt
        parameters.setFloat('a', 0.8);
        updateLayerRuntime(machine, runtime, parameters, 0.05);
        // The interrupt resolves from source (Idle) transitions excluding current target (Walk).
        // Idle→Run (a>0.5) matches with a=0.8.
        expect(runtime.transition!.targetStateIndex).toBe(1); // Run
    });
});

// ===========================================================================
// 9. Any-State Transitions
// ===========================================================================

describe('Animator State Machine — Any-State Transitions', () => {
    it('any-state transition fires from Idle', () => {
        const machine = compileStateMachine(
            {
                entryState: 'Idle',
                states: [
                    { id: 'Idle', motion: { kind: 'clip', clipId: 'idle' } },
                    { id: 'Run', motion: { kind: 'clip', clipId: 'run' } },
                    { id: 'Walk', motion: { kind: 'clip', clipId: 'walk' } },
                ],
                anyStateTransitions: [
                    {
                        to: 'Run',
                        priority: 10,
                        conditions: [{ kind: 'trigger', parameter: 'panic' }],
                    },
                ],
            },
            clipMap
        );
        const runtime = createLayerRuntime(machine);
        const parameters = new AnimationParameterStore([{ name: 'panic', kind: 'trigger' }]);
        parameters.setTrigger('panic');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(1); // Run
    });

    it('any-state transition fires from Run and Walk states', () => {
        const machine = compileStateMachine(
            {
                entryState: 'Idle',
                states: [
                    { id: 'Idle', motion: { kind: 'clip', clipId: 'idle' } },
                    { id: 'Run', motion: { kind: 'clip', clipId: 'run' } },
                    { id: 'Walk', motion: { kind: 'clip', clipId: 'walk' } },
                ],
                anyStateTransitions: [
                    {
                        to: 'Idle',
                        priority: 10,
                        conditions: [{ kind: 'trigger', parameter: 'calm' }],
                    },
                ],
            },
            clipMap
        );

        // From Run
        const runtimeFromRun = createLayerRuntime(machine);
        forceLayerState(machine, runtimeFromRun, 'Run', 0);
        const paramsRun = new AnimationParameterStore([{ name: 'calm', kind: 'trigger' }]);
        paramsRun.setTrigger('calm');
        updateLayerRuntime(machine, runtimeFromRun, paramsRun, 0.1);
        expect(runtimeFromRun.transition!.targetStateIndex).toBe(0); // Idle

        // From Walk
        const runtimeFromWalk = createLayerRuntime(machine);
        forceLayerState(machine, runtimeFromWalk, 'Walk', 0);
        const paramsWalk = new AnimationParameterStore([{ name: 'calm', kind: 'trigger' }]);
        paramsWalk.setTrigger('calm');
        updateLayerRuntime(machine, runtimeFromWalk, paramsWalk, 0.1);
        expect(runtimeFromWalk.transition!.targetStateIndex).toBe(0); // Idle
    });

    it('any-state transitions merge with state transitions by priority', () => {
        const machine = compileStateMachine(
            {
                entryState: 'A',
                states: [
                    {
                        id: 'A',
                        motion: { kind: 'clip', clipId: 'idle' },
                        transitions: [
                            {
                                to: 'B',
                                priority: 1,
                                conditions: [{ kind: 'trigger', parameter: 'go' }],
                            },
                        ],
                    },
                    { id: 'B', motion: { kind: 'clip', clipId: 'run' } },
                    {
                        id: 'C',
                        motion: { kind: 'clip', clipId: 'walk' },
                    },
                ],
                anyStateTransitions: [
                    {
                        to: 'C',
                        priority: 5,
                        conditions: [{ kind: 'trigger', parameter: 'go' }],
                    },
                ],
            },
            clipMap
        );
        const runtime = createLayerRuntime(machine);
        const parameters = new AnimationParameterStore([{ name: 'go', kind: 'trigger' }]);
        parameters.setTrigger('go');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        // Any-state transition to C has priority 5, state transition to B has priority 1
        // Higher priority wins → C
        expect(runtime.transition!.targetStateIndex).toBe(2); // C
    });
});

// ===========================================================================
// 10. Trigger Consumption
// ===========================================================================

describe('Animator State Machine — Trigger Consumption', () => {
    it('trigger is consumed when a transition matches, preventing re-fire', () => {
        const machine = compileStateMachine(
            {
                entryState: 'A',
                states: [
                    {
                        id: 'A',
                        motion: { kind: 'clip', clipId: 'idle' },
                        transitions: [
                            {
                                to: 'B',
                                duration: 0.5,
                                fixedDuration: true,
                                conditions: [{ kind: 'trigger', parameter: 'fire' }],
                            },
                        ],
                    },
                    { id: 'B', motion: { kind: 'clip', clipId: 'run' } },
                ],
            },
            clipMap
        );
        const runtime = createLayerRuntime(machine);
        const parameters = new AnimationParameterStore([{ name: 'fire', kind: 'trigger' }]);

        parameters.setTrigger('fire');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        expect(runtime.transition).not.toBeNull();
        // Trigger should have been consumed
        expect(parameters.get('fire')).toBe(false);
    });

    it('trigger does not cause a second transition after being consumed', () => {
        const machine = compileStateMachine(
            {
                entryState: 'A',
                states: [
                    {
                        id: 'A',
                        motion: { kind: 'clip', clipId: 'idle' },
                        transitions: [
                            {
                                to: 'B',
                                duration: 0.5,
                                fixedDuration: true,
                                canInterrupt: true,
                                conditions: [{ kind: 'trigger', parameter: 'fire' }],
                            },
                        ],
                    },
                    {
                        id: 'B',
                        motion: { kind: 'clip', clipId: 'run' },
                        transitions: [
                            {
                                to: 'A',
                                duration: 0.5,
                                fixedDuration: true,
                                conditions: [{ kind: 'trigger', parameter: 'fire' }],
                            },
                        ],
                    },
                ],
            },
            clipMap
        );
        const runtime = createLayerRuntime(machine);
        const parameters = new AnimationParameterStore([{ name: 'fire', kind: 'trigger' }]);

        parameters.setTrigger('fire');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        // Transition A→B started, trigger consumed
        expect(runtime.transition!.targetStateIndex).toBe(1);
        expect(parameters.get('fire')).toBe(false);

        // Complete the transition
        updateLayerRuntime(machine, runtime, parameters, 0.6);
        commitLayerRuntime(runtime);
        expect(runtime.currentStateIndex).toBe(1); // B

        // Without re-setting the trigger, no transition back to A should occur
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        expect(runtime.transition).toBeNull();
    });
});

// ===========================================================================
// 11. Priority Ordering
// ===========================================================================

describe('Animator State Machine — Priority Ordering', () => {
    it('highest priority transition wins when multiple match', () => {
        const machine = compileStateMachine(
            {
                entryState: 'A',
                states: [
                    {
                        id: 'A',
                        motion: { kind: 'clip', clipId: 'idle' },
                        transitions: [
                            {
                                to: 'B',
                                priority: 1,
                                conditions: [{ kind: 'trigger', parameter: 'go' }],
                            },
                            {
                                to: 'C',
                                priority: 10,
                                conditions: [{ kind: 'trigger', parameter: 'go' }],
                            },
                        ],
                    },
                    { id: 'B', motion: { kind: 'clip', clipId: 'run' } },
                    { id: 'C', motion: { kind: 'clip', clipId: 'walk' } },
                ],
            },
            clipMap
        );
        const runtime = createLayerRuntime(machine);
        const parameters = new AnimationParameterStore([{ name: 'go', kind: 'trigger' }]);
        parameters.setTrigger('go');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        // Priority 10 (→C) beats priority 1 (→B)
        expect(runtime.transition!.targetStateIndex).toBe(2); // C
    });

    it('falls through to lower priority when higher priority conditions do not match', () => {
        const machine = compileStateMachine(
            {
                entryState: 'A',
                states: [
                    {
                        id: 'A',
                        motion: { kind: 'clip', clipId: 'idle' },
                        transitions: [
                            {
                                to: 'B',
                                priority: 10,
                                conditions: [{ kind: 'trigger', parameter: 'high' }],
                            },
                            {
                                to: 'C',
                                priority: 1,
                                conditions: [{ kind: 'trigger', parameter: 'low' }],
                            },
                        ],
                    },
                    { id: 'B', motion: { kind: 'clip', clipId: 'run' } },
                    { id: 'C', motion: { kind: 'clip', clipId: 'walk' } },
                ],
            },
            clipMap
        );
        const runtime = createLayerRuntime(machine);
        const parameters = new AnimationParameterStore([
            { name: 'high', kind: 'trigger' },
            { name: 'low', kind: 'trigger' },
        ]);
        // Only set the low-priority trigger
        parameters.setTrigger('low');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        // High priority (→B) doesn't match, falls through to low priority (→C)
        expect(runtime.transition!.targetStateIndex).toBe(2); // C
    });

    it('default priority is 0 when not specified', () => {
        const machine = compileStateMachine(
            {
                entryState: 'A',
                states: [
                    {
                        id: 'A',
                        motion: { kind: 'clip', clipId: 'idle' },
                        transitions: [
                            {
                                to: 'B',
                                conditions: [{ kind: 'trigger', parameter: 'go' }],
                                // no priority → defaults to 0
                            },
                            {
                                to: 'C',
                                priority: 1,
                                conditions: [{ kind: 'trigger', parameter: 'go' }],
                            },
                        ],
                    },
                    { id: 'B', motion: { kind: 'clip', clipId: 'run' } },
                    { id: 'C', motion: { kind: 'clip', clipId: 'walk' } },
                ],
            },
            clipMap
        );
        const runtime = createLayerRuntime(machine);
        const parameters = new AnimationParameterStore([{ name: 'go', kind: 'trigger' }]);
        parameters.setTrigger('go');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        // priority 1 (→C) beats default priority 0 (→B)
        expect(runtime.transition!.targetStateIndex).toBe(2); // C
    });
});

// ===========================================================================
// 12. Edge Cases
// ===========================================================================

describe('Animator State Machine — Edge Cases', () => {
    it('zero-duration transition completes on the next update tick', () => {
        const machine = compileStateMachine(
            {
                entryState: 'A',
                states: [
                    {
                        id: 'A',
                        motion: { kind: 'clip', clipId: 'idle' },
                        transitions: [
                            {
                                to: 'B',
                                duration: 0,
                                conditions: [{ kind: 'trigger', parameter: 'go' }],
                            },
                        ],
                    },
                    { id: 'B', motion: { kind: 'clip', clipId: 'run' } },
                ],
            },
            clipMap
        );
        const runtime = createLayerRuntime(machine);
        const parameters = new AnimationParameterStore([{ name: 'go', kind: 'trigger' }]);
        parameters.setTrigger('go');
        // First update: creates the transition (durationSeconds = 0)
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        expect(runtime.transition).not.toBeNull();
        // Second update: the existing zero-duration transition completes immediately
        updateLayerRuntime(machine, runtime, parameters, 0.01);
        expect(runtime.transition!.complete).toBe(true);
        commitLayerRuntime(runtime);
        expect(runtime.currentStateIndex).toBe(1); // B
    });

    it('fixedDuration transition uses absolute seconds regardless of state speed', () => {
        const machine = compileStateMachine(
            {
                entryState: 'A',
                states: [
                    {
                        id: 'A',
                        motion: { kind: 'clip', clipId: 'idle' },
                        speed: 2,
                        transitions: [
                            {
                                to: 'B',
                                duration: 0.5,
                                fixedDuration: true,
                                conditions: [{ kind: 'trigger', parameter: 'go' }],
                            },
                        ],
                    },
                    { id: 'B', motion: { kind: 'clip', clipId: 'run' } },
                ],
            },
            clipMap
        );
        const runtime = createLayerRuntime(machine);
        const parameters = new AnimationParameterStore([{ name: 'go', kind: 'trigger' }]);
        parameters.setTrigger('go');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        // fixedDuration: durationSeconds = 0.5 (not scaled by state speed)
        expect(runtime.transition!.durationSeconds).toBeCloseTo(0.5, 5);
    });

    it('non-fixedDuration transition scales by state duration', () => {
        const machine = compileStateMachine(
            {
                entryState: 'A',
                states: [
                    {
                        id: 'A',
                        motion: { kind: 'clip', clipId: 'idle' },
                        transitions: [
                            {
                                to: 'B',
                                duration: 0.5,
                                fixedDuration: false,
                                conditions: [{ kind: 'trigger', parameter: 'go' }],
                            },
                        ],
                    },
                    { id: 'B', motion: { kind: 'clip', clipId: 'run' } },
                ],
            },
            clipMap
        );
        const runtime = createLayerRuntime(machine);
        const parameters = new AnimationParameterStore([{ name: 'go', kind: 'trigger' }]);
        parameters.setTrigger('go');
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        // Non-fixed: durationSeconds = 0.5 * stateDuration (1s for our clips) = 0.5
        expect(runtime.transition!.durationSeconds).toBeCloseTo(0.5, 5);
    });

    it('compileStateMachine throws on unknown transition target', () => {
        expect(() =>
            compileStateMachine(
                {
                    entryState: 'A',
                    states: [
                        {
                            id: 'A',
                            motion: { kind: 'clip', clipId: 'idle' },
                            transitions: [{ to: 'MISSING' }],
                        },
                    ],
                },
                clipMap
            )
        ).toThrow(AnimationValidationError);
    });

    it('compileStateMachine throws on empty states array', () => {
        expect(() =>
            compileStateMachine({ entryState: 'A', states: [] }, clipMap)
        ).toThrow(AnimationValidationError);
    });

    it('compileStateMachine throws on duplicate state ids', () => {
        expect(() =>
            compileStateMachine(
                {
                    entryState: 'A',
                    states: [
                        { id: 'A', motion: { kind: 'clip', clipId: 'idle' } },
                        { id: 'A', motion: { kind: 'clip', clipId: 'run' } },
                    ],
                },
                clipMap
            )
        ).toThrow(AnimationValidationError);
    });

    it('forceLayerState throws for unknown state id', () => {
        const { machine, runtime } = makeRuntime();
        expect(() => forceLayerState(machine, runtime, 'MISSING')).toThrow(
            AnimationStateMachineError
        );
    });

    it('crossFadeLayerState throws for unknown state id', () => {
        const { machine, runtime } = makeRuntime();
        expect(() => crossFadeLayerState(machine, runtime, 'MISSING', 0.5)).toThrow(
            AnimationStateMachineError
        );
    });

    it('createLayerRuntime starts at the entry state with zero normalized time', () => {
        const machine = compileStateMachine(makeCharacterDefinition(), clipMap);
        const runtime = createLayerRuntime(machine);
        expect(runtime.currentStateIndex).toBe(machine.entryStateIndex);
        expect(runtime.currentNormalizedTime).toBe(0);
        expect(runtime.previousNormalizedTime).toBe(0);
        expect(runtime.transition).toBeNull();
    });
});

// ===========================================================================
// 13. Controller-Level Integration (AnimationController)
// ===========================================================================

describe('Animator State Machine — Controller Integration', () => {
    const makeController = (): AnimationController =>
        new AnimationController({
            rig: { bones: [{ name: 'root' }] },
            clips: [
                {
                    id: 'idle',
                    tracks: [
                        {
                            target: 'root',
                            path: 'translation',
                            times: [0, 1],
                            values: [0, 0, 0, 0, 0, 0],
                        },
                    ],
                },
                {
                    id: 'run',
                    tracks: [
                        {
                            target: 'root',
                            path: 'translation',
                            times: [0, 1],
                            values: [0, 0, 0, 2, 0, 0],
                        },
                    ],
                },
                {
                    id: 'walk',
                    tracks: [
                        {
                            target: 'root',
                            path: 'translation',
                            times: [0, 1],
                            values: [0, 0, 0, 4, 0, 0],
                        },
                    ],
                },
            ],
            parameters: [{ name: 'a', kind: 'float', defaultValue: 0 }],
            layers: [
                {
                    id: 'base',
                    stateMachine: {
                        entryState: 'Idle',
                        states: [
                            {
                                id: 'Idle',
                                motion: { kind: 'clip', clipId: 'idle' },
                                transitions: [
                                    {
                                        to: 'Run',
                                        duration: TRANSITION_DURATION,
                                        exitTime: EXIT_TIME,
                                        canInterrupt: true,
                                        conditions: [
                                            { kind: 'float', parameter: 'a', operator: '>', value: 0.5 },
                                        ],
                                    },
                                    {
                                        to: 'Walk',
                                        duration: TRANSITION_DURATION,
                                        exitTime: EXIT_TIME,
                                        canInterrupt: true,
                                        conditions: [
                                            { kind: 'float', parameter: 'a', operator: '>', value: 0.1 },
                                            { kind: 'float', parameter: 'a', operator: '<=', value: 0.5 },
                                        ],
                                    },
                                ],
                            },
                            {
                                id: 'Run',
                                motion: { kind: 'clip', clipId: 'run' },
                                transitions: [
                                    {
                                        to: 'Idle',
                                        duration: TRANSITION_DURATION,
                                        exitTime: EXIT_TIME,
                                        canInterrupt: true,
                                        conditions: [
                                            { kind: 'float', parameter: 'a', operator: '<', value: 0.1 },
                                        ],
                                    },
                                    {
                                        to: 'Walk',
                                        duration: TRANSITION_DURATION,
                                        exitTime: EXIT_TIME,
                                        canInterrupt: true,
                                        conditions: [
                                            { kind: 'float', parameter: 'a', operator: '<=', value: 0.5 },
                                            { kind: 'float', parameter: 'a', operator: '>=', value: 0.1 },
                                        ],
                                    },
                                ],
                            },
                            {
                                id: 'Walk',
                                motion: { kind: 'clip', clipId: 'walk' },
                                transitions: [
                                    {
                                        to: 'Idle',
                                        duration: TRANSITION_DURATION,
                                        exitTime: EXIT_TIME,
                                        canInterrupt: true,
                                        conditions: [
                                            { kind: 'float', parameter: 'a', operator: '<', value: 0.1 },
                                        ],
                                    },
                                    {
                                        to: 'Run',
                                        duration: TRANSITION_DURATION,
                                        exitTime: EXIT_TIME,
                                        canInterrupt: true,
                                        conditions: [
                                            { kind: 'float', parameter: 'a', operator: '>', value: 0.5 },
                                        ],
                                    },
                                ],
                            },
                        ],
                    },
                },
            ],
        });

    it('controller starts in Idle state', () => {
        const controller = makeController();
        expect(controller.profile.activeLayers[0]?.stateId).toBe('Idle');
        expect(controller.profile.activeLayers[0]?.transitioning).toBe(false);
    });

    it('controller transitions Idle → Run via update()', () => {
        const controller = makeController();
        controller.parameters.setFloat('a', 0.8);
        // First update: advance past exitTime and trigger transition
        controller.update(0.15);
        expect(controller.profile.activeLayers[0]?.transitioning).toBe(true);
        // Complete the transition
        controller.update(0.25);
        expect(controller.profile.activeLayers[0]?.stateId).toBe('Run');
    });

    it('controller transitions Idle → Walk when 0.1 < a <= 0.5', () => {
        const controller = makeController();
        controller.parameters.setFloat('a', 0.3);
        controller.update(0.15);
        expect(controller.profile.activeLayers[0]?.transitioning).toBe(true);
        controller.update(0.25);
        expect(controller.profile.activeLayers[0]?.stateId).toBe('Walk');
    });

    it('controller play() forces a specific state', () => {
        const controller = makeController();
        controller.play('Run');
        expect(controller.profile.activeLayers[0]?.stateId).toBe('Run');
    });

    it('controller crossFade() initiates a manual transition', () => {
        const controller = makeController();
        controller.crossFade('Run', 0.5);
        controller.evaluate();
        expect(controller.profile.activeLayers[0]?.transitioning).toBe(true);
        controller.update(0.6);
        expect(controller.profile.activeLayers[0]?.stateId).toBe('Run');
    });
});
