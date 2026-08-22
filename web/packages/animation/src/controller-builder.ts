import { Builder } from '@axrone/utility';
import {
    buildAnimationMotionDefinition,
    type AnimationMotionBuilder,
} from './blend-graph';
import {
    cloneClipDefinition,
    cloneCondition,
    cloneParameterDefinition,
    cloneRigDefinition,
    cloneRootMotionDefinition,
} from './controller-clone';
import type {
    AnimationConditionDefinition,
    AnimationControllerDefinition,
    AnimationIkJobDefinition,
    AnimationIkLayerDefinition,
    AnimationLayerBlendMode,
    AnimationLayerDefinition,
    AnimationMotionDefinition,
    AnimationParameterDefinition,
    AnimationParameterKind,
    AnimationParameterValue,
    AnimationRigDefinition,
    AnimationRootMotionDefinition,
    AnimationStateDefinition,
    AnimationStateMachineDefinition,
    AnimationTransitionDefinition,
    AnimationTransitionOperator,
} from './types';

export type AnimationMotionInput = AnimationMotionDefinition | AnimationMotionBuilder;
export type AnimationTransitionInput = AnimationTransitionDefinition | AnimationTransitionBuilder;
export type AnimationStateInput = AnimationStateDefinition | AnimationStateBuilder;
export type AnimationStateMachineInput = AnimationStateMachineDefinition | AnimationStateMachineBuilder;
export type AnimationIkLayerInput = AnimationIkLayerDefinition | AnimationIkLayerBuilder;
export type AnimationLayerInput = AnimationLayerDefinition | AnimationLayerBuilder;
export type AnimationControllerInput =
    | AnimationControllerDefinition<readonly AnimationParameterDefinition[]>
    | AnimationControllerBuilder;

// ─── AnimationTransitionBuilder ──────────────────────────────────────────────

export class AnimationTransitionBuilder<
    TSupplied extends keyof AnimationTransitionDefinition = 'to',
> extends Builder<AnimationTransitionDefinition, TSupplied> {
    constructor(to: string) {
        super({ to, conditions: Object.freeze([]) as readonly AnimationConditionDefinition[] });
    }

    withDuration(duration: number): AnimationTransitionBuilder<'to' | 'duration'> {
        return this.set('duration', duration) as unknown as AnimationTransitionBuilder<'to' | 'duration'>;
    }

    withOffset(offset: number): AnimationTransitionBuilder<'to' | 'offset'> {
        return this.set('offset', offset) as unknown as AnimationTransitionBuilder<'to' | 'offset'>;
    }

    withExitTime(exitTime: number): AnimationTransitionBuilder<'to' | 'exitTime'> {
        return this.set('exitTime', exitTime) as unknown as AnimationTransitionBuilder<'to' | 'exitTime'>;
    }

    withFixedDuration(fixedDuration = true): AnimationTransitionBuilder<'to' | 'fixedDuration'> {
        return this.set('fixedDuration', fixedDuration) as unknown as AnimationTransitionBuilder<'to' | 'fixedDuration'>;
    }

    withInterruptible(canInterrupt = true): AnimationTransitionBuilder<'to' | 'canInterrupt'> {
        return this.set('canInterrupt', canInterrupt) as unknown as AnimationTransitionBuilder<'to' | 'canInterrupt'>;
    }

    withPriority(priority: number): AnimationTransitionBuilder<'to' | 'priority'> {
        return this.set('priority', priority) as unknown as AnimationTransitionBuilder<'to' | 'priority'>;
    }

    addCondition(condition: AnimationConditionDefinition): AnimationTransitionBuilder<'to' | 'conditions'> {
        const current = (this.peek().conditions ?? []) as readonly AnimationConditionDefinition[];
        return this.set('conditions', Object.freeze([...current, cloneCondition(condition)])) as unknown as AnimationTransitionBuilder<'to' | 'conditions'>;
    }

    whenFloat(parameter: string, operator: AnimationTransitionOperator, value: number): AnimationTransitionBuilder<'to' | 'conditions'> {
        return this.addCondition({ kind: 'float', parameter, operator, value });
    }

    whenInt(parameter: string, operator: AnimationTransitionOperator, value: number): AnimationTransitionBuilder<'to' | 'conditions'> {
        return this.addCondition({ kind: 'int', parameter, operator, value });
    }

    whenBool(parameter: string, value: boolean): AnimationTransitionBuilder<'to' | 'conditions'> {
        return this.addCondition({ kind: 'bool', parameter, value });
    }

    whenTriggered(parameter: string): AnimationTransitionBuilder<'to' | 'conditions'> {
        return this.addCondition({ kind: 'trigger', parameter });
    }
}

// ─── AnimationStateBuilder ───────────────────────────────────────────────────

export class AnimationStateBuilder<
    TSupplied extends keyof AnimationStateDefinition = 'id' | 'motion',
> extends Builder<AnimationStateDefinition, TSupplied> {
    constructor(id: string, motion: AnimationMotionInput) {
        super({
            id,
            motion: buildAnimationMotionDefinition(motion),
            transitions: Object.freeze([]) as readonly AnimationTransitionDefinition[],
        });
    }

    withMotion(motion: AnimationMotionInput): AnimationStateBuilder<'id' | 'motion'> {
        return this.set('motion', buildAnimationMotionDefinition(motion)) as unknown as AnimationStateBuilder<'id' | 'motion'>;
    }

    withSpeed(speed: number): AnimationStateBuilder<'id' | 'motion' | 'speed'> {
        return this.set('speed', speed) as unknown as AnimationStateBuilder<'id' | 'motion' | 'speed'>;
    }

    withLoop(loop: boolean): AnimationStateBuilder<'id' | 'motion' | 'loop'> {
        return this.set('loop', loop) as unknown as AnimationStateBuilder<'id' | 'motion' | 'loop'>;
    }

    addTransition(transition: AnimationTransitionInput): AnimationStateBuilder<'id' | 'motion' | 'transitions'> {
        const current = (this.peek().transitions ?? []) as readonly AnimationTransitionDefinition[];
        const built = buildAnimationTransitionDefinition(transition);
        return this.set('transitions', Object.freeze([...current, built])) as unknown as AnimationStateBuilder<'id' | 'motion' | 'transitions'>;
    }

    transitionTo(
        to: string,
        configure?: (transition: AnimationTransitionBuilder) => AnimationTransitionBuilder
    ): AnimationStateBuilder<'id' | 'motion' | 'transitions'> {
        let transition = new AnimationTransitionBuilder(to);
        if (configure) {
            const configured = configure(transition);
            // `configure` may return a new immutable instance
            transition = configured as AnimationTransitionBuilder;
        }
        return this.addTransition(transition);
    }
}

// ─── AnimationStateMachineBuilder ────────────────────────────────────────────

export class AnimationStateMachineBuilder<
    TSupplied extends keyof AnimationStateMachineDefinition = 'entryState' | 'states',
> extends Builder<AnimationStateMachineDefinition, TSupplied> {
    constructor(entryState?: string) {
        super({
            entryState: entryState ?? '',
            states: Object.freeze([]) as readonly AnimationStateDefinition[],
            anyStateTransitions: Object.freeze([]) as readonly AnimationTransitionDefinition[],
        });
    }

    withEntryState(entryState: string): AnimationStateMachineBuilder<'entryState' | 'states'> {
        return this.set('entryState', entryState) as unknown as AnimationStateMachineBuilder<'entryState' | 'states'>;
    }

    addState(state: AnimationStateInput): AnimationStateMachineBuilder<'entryState' | 'states'> {
        const current = (this.peek().states ?? []) as readonly AnimationStateDefinition[];
        const built = buildAnimationStateDefinition(state);
        const next = this.set('states', Object.freeze([...current, built])) as unknown as AnimationStateMachineBuilder<'entryState' | 'states'>;
        const currentEntry = next.peek().entryState;
        if (!currentEntry) {
            return next.set('entryState', built.id) as unknown as AnimationStateMachineBuilder<'entryState' | 'states'>;
        }
        return next;
    }

    state(
        id: string,
        motion: AnimationMotionInput,
        configure?: (state: AnimationStateBuilder) => AnimationStateBuilder
    ): AnimationStateMachineBuilder<'entryState' | 'states'> {
        let stateBuilder = new AnimationStateBuilder(id, motion);
        if (configure) {
            const configured = configure(stateBuilder);
            stateBuilder = configured as AnimationStateBuilder;
        }
        return this.addState(stateBuilder);
    }

    addAnyStateTransition(transition: AnimationTransitionInput): AnimationStateMachineBuilder<'entryState' | 'states' | 'anyStateTransitions'> {
        const current = (this.peek().anyStateTransitions ?? []) as readonly AnimationTransitionDefinition[];
        const built = buildAnimationTransitionDefinition(transition);
        return this.set('anyStateTransitions', Object.freeze([...current, built])) as unknown as AnimationStateMachineBuilder<'entryState' | 'states' | 'anyStateTransitions'>;
    }

    anyState(
        to: string,
        configure?: (transition: AnimationTransitionBuilder) => AnimationTransitionBuilder
    ): AnimationStateMachineBuilder<'entryState' | 'states' | 'anyStateTransitions'> {
        let transition = new AnimationTransitionBuilder(to);
        if (configure) {
            const configured = configure(transition);
            transition = configured as AnimationTransitionBuilder;
        }
        return this.addAnyStateTransition(transition);
    }
}

// ─── AnimationIkLayerBuilder ─────────────────────────────────────────────────

export class AnimationIkLayerBuilder<
    TSupplied extends keyof AnimationIkLayerDefinition = 'id' | 'jobs',
> extends Builder<AnimationIkLayerDefinition, TSupplied> {
    constructor(id: string) {
        super({ id, jobs: Object.freeze([]) as readonly AnimationIkJobDefinition[] });
    }

    withWeight(weight: number): AnimationIkLayerBuilder<'id' | 'jobs' | 'weight'> {
        return this.set('weight', weight) as unknown as AnimationIkLayerBuilder<'id' | 'jobs' | 'weight'>;
    }

    addJob(job: AnimationIkJobDefinition): AnimationIkLayerBuilder<'id' | 'jobs'> {
        const current = (this.peek().jobs ?? []) as readonly AnimationIkJobDefinition[];
        return this.set('jobs', Object.freeze([...current, Object.freeze({ ...job })])) as unknown as AnimationIkLayerBuilder<'id' | 'jobs'>;
    }
}

// ─── AnimationLayerBuilder ───────────────────────────────────────────────────

export class AnimationLayerBuilder<
    TSupplied extends keyof AnimationLayerDefinition = 'id' | 'stateMachine',
> extends Builder<AnimationLayerDefinition, TSupplied> {
    constructor(id: string, stateMachine: AnimationStateMachineInput) {
        super({
            id,
            stateMachine: buildAnimationStateMachineDefinition(stateMachine),
        });
    }

    withWeight(weight: number): AnimationLayerBuilder<'id' | 'stateMachine' | 'weight'> {
        return this.set('weight', weight) as unknown as AnimationLayerBuilder<'id' | 'stateMachine' | 'weight'>;
    }

    withMode(mode: AnimationLayerBlendMode): AnimationLayerBuilder<'id' | 'stateMachine' | 'mode'> {
        return this.set('mode', mode) as unknown as AnimationLayerBuilder<'id' | 'stateMachine' | 'mode'>;
    }

    withBoneMask(bones: readonly string[]): AnimationLayerBuilder<'id' | 'stateMachine' | 'boneMask'> {
        return this.set('boneMask', Object.freeze([...bones])) as unknown as AnimationLayerBuilder<'id' | 'stateMachine' | 'boneMask'>;
    }

    withStateMachine(stateMachine: AnimationStateMachineInput): AnimationLayerBuilder<'id' | 'stateMachine'> {
        return this.set('stateMachine', buildAnimationStateMachineDefinition(stateMachine)) as unknown as AnimationLayerBuilder<'id' | 'stateMachine'>;
    }

    addIkLayer(layer: AnimationIkLayerInput): AnimationLayerBuilder<'id' | 'stateMachine' | 'ikLayers'> {
        const current = ((this.peek() as any).ikLayers ?? []) as readonly AnimationIkLayerDefinition[];
        const built = buildAnimationIkLayerDefinition(layer);
        return this.set('ikLayers' as keyof AnimationLayerDefinition, Object.freeze([...current, built]) as any) as unknown as AnimationLayerBuilder<'id' | 'stateMachine' | 'ikLayers'>;
    }
}

// ─── AnimationControllerBuilder ──────────────────────────────────────────────

type ControllerClips = AnimationControllerDefinition<readonly AnimationParameterDefinition[]>['clips'];

export class AnimationControllerBuilder<
    TSupplied extends keyof AnimationControllerDefinition<readonly AnimationParameterDefinition[]> = 'rig' | 'clips' | 'layers',
> extends Builder<AnimationControllerDefinition<readonly AnimationParameterDefinition[]>, TSupplied> {
    constructor(rig: AnimationRigDefinition) {
        super({
            rig: cloneRigDefinition(rig),
            clips: Object.freeze([]) as unknown as ControllerClips,
            layers: Object.freeze([]) as readonly AnimationLayerDefinition[],
        });
    }

    addClip(clip: ControllerClips[number]): AnimationControllerBuilder<'rig' | 'clips' | 'layers' | 'clips'> {
        const current = (this.peek().clips ?? []) as unknown as readonly ControllerClips[number][];
        return this.set('clips', Object.freeze([...current, clip]) as unknown as ControllerClips) as unknown as AnimationControllerBuilder<'rig' | 'clips' | 'layers' | 'clips'>;
    }

    addParameter(parameter: AnimationParameterDefinition): AnimationControllerBuilder<'rig' | 'clips' | 'layers' | 'parameters'> {
        const current = (this.peek().parameters ?? []) as readonly AnimationParameterDefinition[];
        return this.set('parameters', Object.freeze([...current, cloneParameterDefinition(parameter)])) as unknown as AnimationControllerBuilder<'rig' | 'clips' | 'layers' | 'parameters'>;
    }

    parameter<TKind extends AnimationParameterKind>(
        name: string,
        kind: TKind,
        defaultValue?: AnimationParameterValue<TKind>
    ): AnimationControllerBuilder<'rig' | 'clips' | 'layers' | 'parameters'> {
        return this.addParameter({
            name,
            kind,
            ...(defaultValue !== undefined ? { defaultValue } : {}),
        });
    }

    addLayer(layer: AnimationLayerInput): AnimationControllerBuilder<'rig' | 'clips' | 'layers'> {
        const current = (this.peek().layers ?? []) as readonly AnimationLayerDefinition[];
        const built = buildAnimationLayerDefinition(layer);
        return this.set('layers', Object.freeze([...current, built])) as unknown as AnimationControllerBuilder<'rig' | 'clips' | 'layers'>;
    }

    layer(
        id: string,
        stateMachine: AnimationStateMachineInput,
        configure?: (layer: AnimationLayerBuilder) => AnimationLayerBuilder
    ): AnimationControllerBuilder<'rig' | 'clips' | 'layers'> {
        let layerBuilder = new AnimationLayerBuilder(id, stateMachine);
        if (configure) {
            const configured = configure(layerBuilder);
            layerBuilder = configured as AnimationLayerBuilder;
        }
        return this.addLayer(layerBuilder);
    }

    withRootMotion(rootMotion: AnimationRootMotionDefinition | null): AnimationControllerBuilder<'rig' | 'clips' | 'layers' | 'rootMotion'> {
        const cloned = rootMotion ? cloneRootMotionDefinition(rootMotion) : null;
        return this.set('rootMotion', cloned) as unknown as AnimationControllerBuilder<'rig' | 'clips' | 'layers' | 'rootMotion'>;
    }
}

// ─── Factory Functions ───────────────────────────────────────────────────────

export const createAnimationTransition = (to: string): AnimationTransitionBuilder =>
    new AnimationTransitionBuilder(to);

export const createAnimationState = (
    id: string,
    motion: AnimationMotionInput
): AnimationStateBuilder => new AnimationStateBuilder(id, motion);

export const createAnimationStateMachine = (
    entryState?: string
): AnimationStateMachineBuilder => new AnimationStateMachineBuilder(entryState);

export const createAnimationIkLayer = (id: string): AnimationIkLayerBuilder =>
    new AnimationIkLayerBuilder(id);

export const createAnimationLayer = (
    id: string,
    stateMachine: AnimationStateMachineInput
): AnimationLayerBuilder => new AnimationLayerBuilder(id, stateMachine);

export const createAnimationController = (
    rig: AnimationRigDefinition
): AnimationControllerBuilder => new AnimationControllerBuilder(rig);

// ─── buildAnimation*Definition (backward compat for raw definitions) ─────────

export const buildAnimationTransitionDefinition = (
    transition: AnimationTransitionInput
): AnimationTransitionDefinition =>
    transition instanceof AnimationTransitionBuilder
        ? transition.buildUnsafe() as AnimationTransitionDefinition
        : Object.freeze({
              to: transition.to,
              ...spreadIfFinite('duration', transition.duration),
              ...spreadIfFinite('offset', transition.offset),
              ...spreadIfFinite('exitTime', transition.exitTime),
              ...(typeof transition.fixedDuration === 'boolean'
                  ? { fixedDuration: transition.fixedDuration }
                  : {}),
              ...(typeof transition.canInterrupt === 'boolean'
                  ? { canInterrupt: transition.canInterrupt }
                  : {}),
              ...spreadIfFinite('priority', transition.priority),
              ...(transition.conditions
                  ? { conditions: Object.freeze(transition.conditions.map(cloneCondition)) }
                  : {}),
          });

export const buildAnimationStateDefinition = (state: AnimationStateInput): AnimationStateDefinition =>
    state instanceof AnimationStateBuilder
        ? state.buildUnsafe() as AnimationStateDefinition
        : Object.freeze({
              id: state.id,
              motion: buildAnimationMotionDefinition(state.motion),
              ...spreadIfFinite('speed', state.speed),
              ...(typeof state.loop === 'boolean' ? { loop: state.loop } : {}),
              ...(state.transitions
                  ? {
                        transitions: Object.freeze(
                            state.transitions.map(buildAnimationTransitionDefinition)
                        ),
                    }
                  : {}),
          });

export const buildAnimationStateMachineDefinition = (
    stateMachine: AnimationStateMachineInput
): AnimationStateMachineDefinition =>
    stateMachine instanceof AnimationStateMachineBuilder
        ? stateMachine.buildUnsafe() as AnimationStateMachineDefinition
        : Object.freeze({
              entryState: stateMachine.entryState,
              states: Object.freeze(stateMachine.states.map(buildAnimationStateDefinition)),
              ...(stateMachine.anyStateTransitions
                  ? {
                      anyStateTransitions: Object.freeze(
                          stateMachine.anyStateTransitions.map(buildAnimationTransitionDefinition)
                      ),
                  }
                  : {}),
          });

export const buildAnimationIkLayerDefinition = (
    layer: AnimationIkLayerInput
): AnimationIkLayerDefinition =>
    layer instanceof AnimationIkLayerBuilder
        ? layer.buildUnsafe() as AnimationIkLayerDefinition
        : Object.freeze({
              id: layer.id,
              ...spreadIfFinite('weight', layer.weight),
              jobs: Object.freeze(layer.jobs.map((job) => Object.freeze({ ...job }))),
          });

export const buildAnimationLayerDefinition = (layer: AnimationLayerInput): AnimationLayerDefinition =>
    layer instanceof AnimationLayerBuilder
        ? layer.buildUnsafe() as AnimationLayerDefinition
        : Object.freeze({
              id: layer.id,
              ...spreadIfFinite('weight', layer.weight),
              ...(layer.mode ? { mode: layer.mode } : {}),
              ...(layer.boneMask ? { boneMask: Object.freeze([...layer.boneMask]) } : {}),
              stateMachine: buildAnimationStateMachineDefinition(layer.stateMachine),
              ...(layer.ikLayers
                  ? { ikLayers: Object.freeze(layer.ikLayers.map(buildAnimationIkLayerDefinition)) }
                  : {}),
          });

export const buildAnimationControllerDefinition = (
    controller: AnimationControllerInput
): AnimationControllerDefinition<readonly AnimationParameterDefinition[]> =>
    controller instanceof AnimationControllerBuilder
        ? controller.buildUnsafe() as AnimationControllerDefinition<readonly AnimationParameterDefinition[]>
        : Object.freeze({
              rig: cloneRigDefinition(controller.rig),
              clips: Object.freeze(controller.clips.map(cloneClipDefinition)),
              layers: Object.freeze(controller.layers.map(buildAnimationLayerDefinition)),
              ...(controller.parameters
                  ? { parameters: Object.freeze(controller.parameters.map(cloneParameterDefinition)) }
                  : {}),
              ...(controller.rootMotion !== undefined
                  ? {
                        rootMotion: controller.rootMotion
                            ? cloneRootMotionDefinition(controller.rootMotion)
                            : null,
                    }
                  : {}),
          });

const spreadIfFinite = (key: string, value: unknown): Record<string, unknown> =>
    typeof value === 'number' && Number.isFinite(value) ? { [key]: value } : {};
