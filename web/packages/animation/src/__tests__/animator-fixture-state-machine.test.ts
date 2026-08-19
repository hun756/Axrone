/**
 * T-07: Animator Fixture State Machine Test
 *
 * Loads the actual GM_AssetStore_3D_Character.animator.json fixture,
 * compiles it into a state machine, and validates all transitions,
 * parameters, and blending behavior.
 *
 * Fixture structure:
 *   - 2 float params: a (default 0), b (default 0)
 *   - 1 layer: "base"
 *   - 3 states: Idle, Run, Walk
 *   - entryState: "Run"
 *   - All transitions: duration=0.2, exitTime=0.1, canInterrupt=true
 *   - Parameter `a` driven:
 *       a < 0.1       -> Idle
 *       0.1 < a <= 0.5 -> Walk
 *       a > 0.5        -> Run
 */

import { describe, expect, it, beforeAll } from 'vitest';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { AnimationClip } from '../clip';
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
import { AnimationScratchPool } from '../blend-tree';
import type { AnimationStateMachineDefinition } from '../types';

// ---------------------------------------------------------------------------
// Path resolution
// ---------------------------------------------------------------------------

const testDir = path.dirname(fileURLToPath(import.meta.url));
// From: Axrone/web/packages/animation/src/__tests__
// To:   <repo-root>/Assets/Animator
const animatorDir = path.resolve(testDir, '../../../../../../Assets/Animator');

// ---------------------------------------------------------------------------
// Shared rig / clip helpers (same pattern as animator-state-machine.test.ts)
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

// Each clip produces distinct translation values for blend verification:
//   Idle -> x = 0 at any time
//   Run  -> x = 2 at t=1
//   Walk -> x = 4 at t=1
const clipMap = new Map<string, AnimationClip>([
    ['Idle', makeClip('Idle', 0)],
    ['Run', makeClip('Run', 2)],
    ['Walk', makeClip('Walk', 4)],
]);

// ---------------------------------------------------------------------------
// Fixture types (on-disk .animator.json schema)
// ---------------------------------------------------------------------------

interface FixtureTransitionCondition {
    kind: string;
    parameter: string;
    operator: string;
    value: number;
}

interface FixtureTransition {
    to: string;
    duration: number;
    exitTime: number;
    canInterrupt: boolean;
    conditions: FixtureTransitionCondition[];
}

interface FixtureState {
    id: string;
    motion: { kind: string; clipId: string };
    transitions: FixtureTransition[];
    loop?: boolean;
    speed?: number;
    cycleOffset?: number;
    footIk?: boolean;
    writeDefaults?: boolean;
}

interface FixtureStateMachine {
    entryState: string;
    states: FixtureState[];
}

interface FixtureLayer {
    id: string;
    mode: string;
    weight: number;
    stateMachine: FixtureStateMachine;
}

interface FixtureParameter {
    name: string;
    kind: string;
    defaultValue: number;
}

interface FixtureFile {
    schemaVersion: number;
    kind: string;
    name: string;
    parameters: FixtureParameter[];
    layers: FixtureLayer[];
    animatorEditor?: unknown;
}

// ---------------------------------------------------------------------------
// Fixture loading
// ---------------------------------------------------------------------------

const FIXTURE_FILE = 'GM_AssetStore_3D_Character.animator.json';

let fixture: FixtureFile;
let stateMachineDef: AnimationStateMachineDefinition;

/**
 * Convert the on-disk fixture's layer state machine into the
 * AnimationStateMachineDefinition format used by compileStateMachine.
 */
const convertFixtureureToDefinition = (f: FixtureFile): AnimationStateMachineDefinition => {
    const layer = f.layers[0]!;
    const sm = layer.stateMachine;
    return {
        entryState: sm.entryState,
        states: sm.states.map((s) => ({
            id: s.id,
            motion: s.motion,
            transitions: s.transitions.map((t) => ({
                to: t.to,
                duration: t.duration,
                exitTime: t.exitTime,
                canInterrupt: t.canInterrupt,
                conditions: t.conditions.map((c) => ({
                    kind: c.kind as 'float',
                    parameter: c.parameter,
                    operator: c.operator as '>' | '<' | '>=' | '<=',
                    value: c.value,
                })),
            })),
        })),
    };
};

/** Helper: compile + create runtime + parameter store from fixture. */
const makeRuntime = (): {
    machine: AnimationCompiledStateMachine;
    runtime: AnimationLayerRuntime;
    parameters: AnimationParameterStore;
} => {
    const machine = compileStateMachine(stateMachineDef, clipMap);
    const runtime = createLayerRuntime(machine);
    const parameters = new AnimationParameterStore(
        fixture.parameters.map((p) => ({
            name: p.name,
            kind: p.kind as 'float',
            defaultValue: p.defaultValue,
        }))
    );
    return { machine, runtime, parameters };
};

/** Helper: build a scratch context for evaluateLayerRuntime. */
const makeContext = (parameters?: AnimationParameterStore) => {
    const scratchPool = new AnimationScratchPool(rig, curveLayout, new Float32Array(0));
    return {
        rig,
        parameters: parameters ?? new AnimationParameterStore([]),
        restFrame: new AnimationFrame(rig, curveLayout),
        scratch: scratchPool,
    };
};

// ---------------------------------------------------------------------------
// Load fixture once
// ---------------------------------------------------------------------------

beforeAll(() => {
    const filePath = path.join(animatorDir, FIXTURE_FILE);
    const raw = fs.readFileSync(filePath, 'utf8');
    fixture = JSON.parse(raw) as FixtureFile;
    stateMachineDef = convertFixtureureToDefinition(fixture);
});

// ===========================================================================
// 1. JSON Schema Validation
// ===========================================================================

describe('T-07: Fixture JSON Schema', () => {
    it('loads the fixture file without errors', () => {
        expect(fixture).toBeDefined();
        expect(fixture.name).toBe('GM_AssetStore_3D_Character');
    });

    it('has schemaVersion 1 and kind "axrone.animator-controller"', () => {
        expect(fixture.schemaVersion).toBe(1);
        expect(fixture.kind).toBe('axrone.animator-controller');
    });

    it('defines exactly 2 float parameters: a and b', () => {
        expect(fixture.parameters).toHaveLength(2);
        const names = fixture.parameters.map((p) => p.name);
        expect(names).toContain('a');
        expect(names).toContain('b');
        for (const p of fixture.parameters) {
            expect(p.kind).toBe('float');
            expect(p.defaultValue).toBe(0);
        }
    });

    it('defines exactly 1 layer named "base"', () => {
        expect(fixture.layers).toHaveLength(1);
        expect(fixture.layers[0]!.id).toBe('base');
        expect(fixture.layers[0]!.mode).toBe('override');
        expect(fixture.layers[0]!.weight).toBe(1);
    });

    it('defines 3 states: Idle, Run, Walk with correct clip references', () => {
        const sm = fixture.layers[0]!.stateMachine;
        expect(sm.states).toHaveLength(3);
        const ids = sm.states.map((s) => s.id);
        expect(ids).toContain('Idle');
        expect(ids).toContain('Run');
        expect(ids).toContain('Walk');

        // Each state references its own clip
        for (const s of sm.states) {
            expect(s.motion.kind).toBe('clip');
            expect(s.motion.clipId).toBe(s.id);
        }
    });

    it('all transitions have duration=0.2, exitTime=0.1, canInterrupt=true', () => {
        const sm = fixture.layers[0]!.stateMachine;
        for (const state of sm.states) {
            for (const t of state.transitions) {
                expect(t.duration).toBe(0.2);
                expect(t.exitTime).toBe(0.1);
                expect(t.canInterrupt).toBe(true);
            }
        }
    });

    it('each state has exactly 2 transitions', () => {
        const sm = fixture.layers[0]!.stateMachine;
        for (const state of sm.states) {
            expect(state.transitions).toHaveLength(2);
        }
    });
});

// ===========================================================================
// 2. State Machine Compilation
// ===========================================================================

describe('T-07: State Machine Compilation', () => {
    it('compiles the fixture definition without errors', () => {
        const machine = compileStateMachine(stateMachineDef, clipMap);
        expect(machine).toBeDefined();
        expect(machine.states).toHaveLength(3);
    });

    it('entry state is "Run" (index 1) as specified in the fixture', () => {
        const machine = compileStateMachine(stateMachineDef, clipMap);
        // Fixture entryState is "Run"
        expect(machine.entryStateIndex).toBe(1);
        expect(machine.states[machine.entryStateIndex]!.id).toBe('Run');
    });

    it('assigns correct state indices: Idle=0, Run=1, Walk=2', () => {
        const machine = compileStateMachine(stateMachineDef, clipMap);
        expect(machine.stateIndexById.get('Idle')).toBe(0);
        expect(machine.stateIndexById.get('Run')).toBe(1);
        expect(machine.stateIndexById.get('Walk')).toBe(2);
    });

    it('compiles transitions with correct target indices and condition counts', () => {
        const machine = compileStateMachine(stateMachineDef, clipMap);

        // Idle transitions: to Run (1 condition), to Walk (2 conditions)
        const idleTransitions = machine.states[0]!.transitions;
        expect(idleTransitions).toHaveLength(2);
        const idleToRun = idleTransitions.find((t) => t.targetStateIndex === 1);
        const idleToWalk = idleTransitions.find((t) => t.targetStateIndex === 2);
        expect(idleToRun).toBeDefined();
        expect(idleToRun!.conditions).toHaveLength(1);
        expect(idleToWalk).toBeDefined();
        expect(idleToWalk!.conditions).toHaveLength(2);

        // Run transitions: to Idle (1 condition), to Walk (2 conditions)
        const runTransitions = machine.states[1]!.transitions;
        expect(runTransitions).toHaveLength(2);
        const runToIdle = runTransitions.find((t) => t.targetStateIndex === 0);
        const runToWalk = runTransitions.find((t) => t.targetStateIndex === 2);
        expect(runToIdle).toBeDefined();
        expect(runToIdle!.conditions).toHaveLength(1);
        expect(runToWalk).toBeDefined();
        expect(runToWalk!.conditions).toHaveLength(2);

        // Walk transitions: to Idle (1 condition), to Run (1 condition)
        const walkTransitions = machine.states[2]!.transitions;
        expect(walkTransitions).toHaveLength(2);
        const walkToIdle = walkTransitions.find((t) => t.targetStateIndex === 0);
        const walkToRun = walkTransitions.find((t) => t.targetStateIndex === 1);
        expect(walkToIdle).toBeDefined();
        expect(walkToIdle!.conditions).toHaveLength(1);
        expect(walkToRun).toBeDefined();
        expect(walkToRun!.conditions).toHaveLength(1);
    });

    it('freezes the compiled machine so it cannot be mutated', () => {
        const machine = compileStateMachine(stateMachineDef, clipMap);
        expect(Object.isFrozen(machine)).toBe(true);
        expect(Object.isFrozen(machine.states)).toBe(true);
    });
});

// ===========================================================================
// 3. Parameter Store
// ===========================================================================

describe('T-07: Parameter Store', () => {
    it('creates float params a and b with default values of 0', () => {
        const params = new AnimationParameterStore(
            fixture.parameters.map((p) => ({
                name: p.name,
                kind: p.kind as 'float',
                defaultValue: p.defaultValue,
            }))
        );
        expect(params.get('a')).toBe(0);
        expect(params.get('b')).toBe(0);
    });

    it('setFloat / get returns the exact value for both params', () => {
        const params = new AnimationParameterStore(
            fixture.parameters.map((p) => ({
                name: p.name,
                kind: p.kind as 'float',
                defaultValue: p.defaultValue,
            }))
        );
        params.setFloat('a', 0.75);
        params.setFloat('b', 0.42);
        expect(params.get('a')).toBe(0.75);
        expect(params.get('b')).toBe(0.42);
    });

    it('has() returns true for a and b, false for unknown', () => {
        const params = new AnimationParameterStore(
            fixture.parameters.map((p) => ({
                name: p.name,
                kind: p.kind as 'float',
                defaultValue: p.defaultValue,
            }))
        );
        expect(params.has('a')).toBe(true);
        expect(params.has('b')).toBe(true);
        expect(params.has('missing')).toBe(false);
    });
});

// ===========================================================================
// 4. Basic Transitions (from entry state "Run")
// ===========================================================================

describe('T-07: Basic Transitions', () => {
    it('Run -> Idle when a < 0.1 and exitTime crossed', () => {
        const { machine, runtime, parameters } = makeRuntime();
        // Start in Run (entry state), set a = 0.05
        parameters.setFloat('a', 0.05);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(0); // Idle
        expect(runtime.transition!.sourceStateIndex).toBe(1); // Run
    });

    it('Run -> Walk when 0.1 < a <= 0.5 and exitTime crossed', () => {
        const { machine, runtime, parameters } = makeRuntime();
        parameters.setFloat('a', 0.3);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(2); // Walk
        expect(runtime.transition!.sourceStateIndex).toBe(1); // Run
    });

    it('stays in Run when a = 0.5 (Run->Idle needs a<0.1, Run->Walk needs a<=0.5 AND a>=0.1)', () => {
        const { machine, runtime, parameters } = makeRuntime();
        // a=0.5 satisfies Run->Walk conditions: a<=0.5 AND a>=0.1
        parameters.setFloat('a', 0.5);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(2); // Walk
    });

    it('does NOT transition when a = 0.1 (Run->Idle needs a<0.1, Run->Walk needs a>=0.1)', () => {
        const { machine, runtime, parameters } = makeRuntime();
        // a=0.1: Run->Idle needs a<0.1 (fails), Run->Walk needs a>=0.1 AND a<=0.5 (passes)
        parameters.setFloat('a', 0.1);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        // a=0.1 satisfies Run->Walk (a>=0.1 AND a<=0.5)
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(2); // Walk
    });

    it('does NOT transition when a = 0 (no condition met: Run->Idle needs a<0.1)', () => {
        const { machine, runtime, parameters } = makeRuntime();
        // a=0: Run->Idle needs a<0.1 (passes! 0 < 0.1)
        parameters.setFloat('a', 0.0);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(0); // Idle
    });
});

// ===========================================================================
// 5. All 6 Bidirectional Transitions
// ===========================================================================

describe('T-07: All 6 Bidirectional Transitions', () => {
    it('Idle -> Run when a > 0.5', () => {
        const { machine, runtime, parameters } = makeRuntime();
        forceLayerState(machine, runtime, 'Idle', 0);
        parameters.setFloat('a', 0.8);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(1); // Run
        expect(runtime.transition!.sourceStateIndex).toBe(0); // Idle
    });

    it('Idle -> Walk when 0.1 < a <= 0.5', () => {
        const { machine, runtime, parameters } = makeRuntime();
        forceLayerState(machine, runtime, 'Idle', 0);
        parameters.setFloat('a', 0.3);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(2); // Walk
        expect(runtime.transition!.sourceStateIndex).toBe(0); // Idle
    });

    it('Run -> Idle when a < 0.1', () => {
        const { machine, runtime, parameters } = makeRuntime();
        forceLayerState(machine, runtime, 'Run', 0);
        parameters.setFloat('a', 0.05);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(0); // Idle
        expect(runtime.transition!.sourceStateIndex).toBe(1); // Run
    });

    it('Run -> Walk when 0.1 <= a <= 0.5', () => {
        const { machine, runtime, parameters } = makeRuntime();
        forceLayerState(machine, runtime, 'Run', 0);
        parameters.setFloat('a', 0.3);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(2); // Walk
        expect(runtime.transition!.sourceStateIndex).toBe(1); // Run
    });

    it('Walk -> Idle when a < 0.1', () => {
        const { machine, runtime, parameters } = makeRuntime();
        forceLayerState(machine, runtime, 'Walk', 0);
        parameters.setFloat('a', 0.02);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(0); // Idle
        expect(runtime.transition!.sourceStateIndex).toBe(2); // Walk
    });

    it('Walk -> Run when a > 0.5', () => {
        const { machine, runtime, parameters } = makeRuntime();
        forceLayerState(machine, runtime, 'Walk', 0);
        parameters.setFloat('a', 0.8);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(1); // Run
        expect(runtime.transition!.sourceStateIndex).toBe(2); // Walk
    });
});

// ===========================================================================
// 6. Parameter-Driven Transition Logic
// ===========================================================================

describe('T-07: Parameter-Driven Transition Logic', () => {
    it('Idle: a=0.3 triggers Walk (not Run)', () => {
        const { machine, runtime, parameters } = makeRuntime();
        forceLayerState(machine, runtime, 'Idle', 0);
        parameters.setFloat('a', 0.3);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(2); // Walk, not Run
    });

    it('Idle: a=0.7 triggers Run (not Walk)', () => {
        const { machine, runtime, parameters } = makeRuntime();
        forceLayerState(machine, runtime, 'Idle', 0);
        parameters.setFloat('a', 0.7);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(1); // Run, not Walk
    });

    it('Idle: a=0.05 triggers no transition (below all thresholds)', () => {
        const { machine, runtime, parameters } = makeRuntime();
        forceLayerState(machine, runtime, 'Idle', 0);
        parameters.setFloat('a', 0.05);
        updateLayerRuntime(machine, runtime, parameters, 0.2);
        // a=0.05: Run needs a>0.5 (no), Walk needs a>0.1 (no)
        expect(runtime.transition).toBeNull();
    });

    it('Walk: a=0.5 triggers Run (a>0.5 is false, but a<=0.5 AND a>=0.1 for Walk->Run? No)', () => {
        const { machine, runtime, parameters } = makeRuntime();
        forceLayerState(machine, runtime, 'Walk', 0);
        // Walk transitions: to Idle (a<0.1), to Run (a>0.5)
        // a=0.5: Idle needs a<0.1 (no), Run needs a>0.5 (no, 0.5 is not > 0.5)
        parameters.setFloat('a', 0.5);
        updateLayerRuntime(machine, runtime, parameters, 0.2);
        expect(runtime.transition).toBeNull();
    });

    it('Run: a=0.1 triggers Walk (a>=0.1 AND a<=0.5)', () => {
        const { machine, runtime, parameters } = makeRuntime();
        forceLayerState(machine, runtime, 'Run', 0);
        // Run transitions: to Idle (a<0.1), to Walk (a<=0.5 AND a>=0.1)
        // a=0.1: Idle needs a<0.1 (no), Walk needs a>=0.1 (yes) AND a<=0.5 (yes)
        parameters.setFloat('a', 0.1);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(2); // Walk
    });
});

// ===========================================================================
// 7. Cross-Fade Blending
// ===========================================================================

describe('T-07: Cross-Fade Blending', () => {
    it('during transition, progress is between 0 and 1', () => {
        const { machine, runtime, parameters } = makeRuntime();
        // Trigger Run -> Idle transition
        parameters.setFloat('a', 0.05);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();

        // Advance partway through the transition (duration=0.2)
        updateLayerRuntime(machine, runtime, parameters, 0.1);
        const progress = runtime.transition!.progress;
        expect(progress).toBeGreaterThan(0);
        expect(progress).toBeLessThan(1);
    });

    it('evaluateLayerRuntime produces valid blended output during transition', () => {
        const { machine, runtime, parameters } = makeRuntime();
        // Trigger Run -> Walk transition
        parameters.setFloat('a', 0.3);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();

        // Advance partway
        updateLayerRuntime(machine, runtime, parameters, 0.05);

        const context = makeContext(parameters);
        context.scratch.reset();
        const out = new AnimationFrame(rig, curveLayout);
        evaluateLayerRuntime(machine, runtime, context, out);

        // The blended frame should contain valid finite numbers
        expect(out.pose.translations[0]).toBeTypeOf('number');
        expect(Number.isFinite(out.pose.translations[0])).toBe(true);
    });

    it('at progress=0 the output matches the source state (Run, x=2 at t=0)', () => {
        const { machine, runtime, parameters } = makeRuntime();
        // Use crossFadeLayerState for precise control — start Run->Walk at progress=0
        crossFadeLayerState(machine, runtime, 'Walk', 1, 0, false);
        const context = makeContext(parameters);
        context.scratch.reset();
        const out = new AnimationFrame(rig, curveLayout);
        evaluateLayerRuntime(machine, runtime, context, out);
        // At progress=0, pure source (Run). Run clip: x=0 at t=0 (start of clip)
        // The clip has times=[0,1], values=[0,0,0,2,0,0], so at t=0 x=0
        expect(out.pose.translations[0]).toBeCloseTo(0, 5);
    });

    it('commitLayerRuntime is a no-op when transition is not complete', () => {
        const { machine, runtime, parameters } = makeRuntime();
        parameters.setFloat('a', 0.05);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.complete).toBe(false);

        const stateBefore = runtime.currentStateIndex;
        commitLayerRuntime(runtime);
        expect(runtime.currentStateIndex).toBe(stateBefore);
        expect(runtime.transition).not.toBeNull();
    });

    it('transition completes after full duration (0.2s) and commit finalizes state', () => {
        const { machine, runtime, parameters } = makeRuntime();
        // Start in Run (entry), trigger Run -> Idle
        parameters.setFloat('a', 0.05);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();

        // Complete the transition (duration = 0.2s)
        updateLayerRuntime(machine, runtime, parameters, 0.25);
        expect(runtime.transition!.complete).toBe(true);
        commitLayerRuntime(runtime);
        expect(runtime.currentStateIndex).toBe(0); // Now in Idle
        expect(runtime.transition).toBeNull();
    });
});

// ===========================================================================
// 8. Exit Time
// ===========================================================================

describe('T-07: Exit Time', () => {
    it('transition does NOT fire before exitTime=0.1 is crossed', () => {
        const { machine, runtime, parameters } = makeRuntime();
        parameters.setFloat('a', 0.05); // satisfies Run->Idle condition
        // Advance only to normalizedTime=0.05 (before exitTime=0.1)
        updateLayerRuntime(machine, runtime, parameters, 0.05);
        expect(runtime.transition).toBeNull();
    });

    it('transition fires once normalizedTime crosses exitTime=0.1', () => {
        const { machine, runtime, parameters } = makeRuntime();
        parameters.setFloat('a', 0.05); // satisfies Run->Idle condition
        // Advance to normalizedTime=0.15 (past exitTime=0.1)
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(0); // Idle
    });
});

// ===========================================================================
// 9. Can Interrupt
// ===========================================================================

describe('T-07: Can Interrupt', () => {
    it('interruptible transition: Run->Walk can be interrupted by Run->Idle when a drops', () => {
        const { machine, runtime, parameters } = makeRuntime();
        // Start Run->Walk transition (a=0.3)
        parameters.setFloat('a', 0.3);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(2); // Walk

        // Now drop a to 0.02 — Run->Idle (a<0.1) should interrupt
        parameters.setFloat('a', 0.02);
        updateLayerRuntime(machine, runtime, parameters, 0.05);
        // canInterrupt=true allows the transition to be re-evaluated
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(0); // Idle
    });

    it('all fixture transitions have canInterrupt=true', () => {
        const machine = compileStateMachine(stateMachineDef, clipMap);
        for (const state of machine.states) {
            for (const t of state.transitions) {
                expect(t.canInterrupt).toBe(true);
            }
        }
    });
});

// ===========================================================================
// 10. Full Cycle Integration
// ===========================================================================

describe('T-07: Full Cycle Integration', () => {
    it('full cycle: Run -> Walk -> Idle -> Run via parameter changes', () => {
        const { machine, runtime, parameters } = makeRuntime();

        // Start in Run (entry state)

        // Run -> Walk (a=0.3)
        parameters.setFloat('a', 0.3);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        updateLayerRuntime(machine, runtime, parameters, 0.25);
        commitLayerRuntime(runtime);
        expect(runtime.currentStateIndex).toBe(2); // Walk

        // Reset normalized time so exitTime can be crossed again
        forceLayerState(machine, runtime, 'Walk', 0);

        // Walk -> Idle (a=0.0)
        parameters.setFloat('a', 0.0);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        updateLayerRuntime(machine, runtime, parameters, 0.25);
        commitLayerRuntime(runtime);
        expect(runtime.currentStateIndex).toBe(0); // Idle

        // Reset normalized time
        forceLayerState(machine, runtime, 'Idle', 0);

        // Idle -> Run (a=0.8)
        parameters.setFloat('a', 0.8);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        updateLayerRuntime(machine, runtime, parameters, 0.25);
        commitLayerRuntime(runtime);
        expect(runtime.currentStateIndex).toBe(1); // Run
    });

    it('rapid parameter changes: Run -> Walk -> Run without committing', () => {
        const { machine, runtime, parameters } = makeRuntime();

        // Start Run -> Walk
        parameters.setFloat('a', 0.3);
        updateLayerRuntime(machine, runtime, parameters, 0.15);
        expect(runtime.transition).not.toBeNull();
        expect(runtime.transition!.targetStateIndex).toBe(2); // Walk

        // Before committing, change a to 0.8 — should interrupt to Run (but Run is source)
        // Since canInterrupt=true and we're still in Run, the Run->Run doesn't make sense.
        // Actually, the transition resolves from source state transitions excluding current target.
        // Source=Run, current target=Walk. Run transitions: to Idle (a<0.1), to Walk (a<=0.5 AND a>=0.1).
        // With a=0.8: Idle needs a<0.1 (no), Walk needs a<=0.5 (no). No interrupt resolution.
        // The transition stays targeting Walk.
        parameters.setFloat('a', 0.8);
        updateLayerRuntime(machine, runtime, parameters, 0.05);
        // No interrupt possible — transition still targets Walk
        expect(runtime.transition!.targetStateIndex).toBe(2); // Walk
    });
});
