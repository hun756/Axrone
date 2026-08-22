import { Builder, $state, $node } from '@axrone/utility';
import type { DeepReadonly, RequiredKeys } from '@axrone/utility';
import { StateNode } from '@axrone/utility';
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
import { spreadIfFinite } from './internal';
import type {
    AnimationConditionDefinition,
    AnimationControllerDefinition,
    AnimationIkJobDefinition,
    AnimationIkLayerDefinition,
    AnimationLayerBlendMode,
    AnimationLayerDefinition,
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

export class AnimationTransitionBuilder extends Builder<
    AnimationTransitionDefinition,
    TSupplied extends keyof AnimationTransitionDefinition = 'to'
> {
    constructor(to: string) {
        super({ to, conditions: Object.freeze([]) as readonly AnimationConditionDefinition[] });
    }

    withDuration(duration: number): AnimationTransitionBuilder<TSupplied | 'to'> {
        return this._wrap(this.set('duration', duration));
    }

    withOffset(offset: number): AnimationTransitionBuilder<TSupplied | 'to'> {
        return this._wrap(this.set('offset', offset));
    }

    withExitTime(exitTime: number): AnimationTransitionBuilder<TSupplied | 'to'> {
        return this._wrap(this.set('exitTime', exitTime));
    }

    withFixedDuration(fixedDuration = true): AnimationTransitionBuilder<TSupplied | 'to'> {
        return this._wrap(this.set('fixedDuration', fixedDuration));
    }

    withInterruptible(canInterrupt = true): AnimationTransitionBuilder<TSupplied | 'to'> {
        return this._wrap(this.set('canInterrupt', canInterrupt));
    }

    withPriority(priority: number): AnimationTransitionBuilder<TSupplied | 'to'> {
        return this._wrap(this.set('priority', priority));
    }

    addCondition(condition: AnimationConditionDefinition): AnimationTransitionBuilder<TSupplied | 'to'> {
        const current = (this.peek().conditions ?? []) as readonly AnimationConditionDefinition[];
        return this._wrap(this.set('conditions', Object.freeze([...current, cloneCondition(condition)])));
    }

    whenFloat(parameter: string, operator: AnimationTransitionOperator, value: number): AnimationTransitionBuilder<TSupplied | 'to'> {
        return this.addCondition({ kind: 'float', parameter, operator, value });
    }

    whenInt(parameter: string, operator: AnimationTransitionOperator, value: number): AnimationTransitionBuilder<TSupplied | 'to'> {
        return this.addCondition({ kind: 'int', parameter, operator, value });
    }

    whenBool(parameter: string, value: boolean): AnimationTransitionBuilder<TSupplied | 'to'> {
        return this.addCondition({ kind: 'bool', parameter, value });
    }

    whenTriggered(parameter: string): AnimationTransitionBuilder<TSupplied | 'to'> {
        return this.addCondition({ kind: 'trigger', parameter });
    }

    public override build(this: AnimationTransitionBuilder<RequiredKeys<AnimationTransitionDefinition>>): DeepReadonly<AnimationTransitionDefinition> {
        return super.build();
    }

    private _wrap<TNext extends keyof AnimationTransitionDefinition>(
        builder: Builder<AnimationTransitionDefinition, TNext>
    ): AnimationTransitionBuilder<TNext> {
        return AnimationTransitionBuilder._reconstruct<TNext>(
            (builder as any)[$state],
            (builder as any)[$node],
            (builder as any).validators,
            (builder as any).beforeHooks,
            (builder as any).afterHooks,
            (builder as any).shouldFreeze
        );
    }

    /** @internal */
    static _reconstruct<S extends keyof AnimationTransitionDefinition>(
        seed: Record<string, unknown>,
        node: StateNode,
        validators: readonly unknown[] = [],
        beforeHooks: readonly unknown[] = [],
        afterHooks: readonly unknown[] = [],
        shouldFreeze = false
    ): AnimationTransitionBuilder<S> {
        const instance = Object.create(AnimationTransitionBuilder.prototype) as AnimationTransitionBuilder<S>;
        (instance as any)[$state] = seed;
        (instance as any)[$node] = node;
        (instance as any).validators = validators;
        (instance as any).beforeHooks = beforeHooks;
        (instance as any).afterHooks = afterHooks;
        (instance as any).shouldFreeze = shouldFreeze;
        return instance;
    }
}

// ─── AnimationStateBuilder ───────────────────────────────────────────────────

export class AnimationStateBuilder extends Builder<
    AnimationStateDefinition,
    TSupplied extends keyof AnimationStateDefinition = 'id' | 'motion'
> {
    constructor(id: string, motion: AnimationMotionInput) {
        super({
            id,
            motion: buildAnimationMotionDefinition(motion),
            transitions: Object.freeze([]) as readonly AnimationTransitionDefinition[],
        });
    }

    withMotion(motion: AnimationMotionInput): AnimationStateBuilder<TSupplied | 'id' | 'motion'> {
        return this._wrap(this.set('motion', buildAnimationMotionDefinition(motion)));
    }

    withSpeed(speed: number): AnimationStateBuilder<TSupplied | 'id' | 'motion'> {
        return this._wrap(this.set('speed', speed));
    }

    withLoop(loop: boolean): AnimationStateBuilder<TSupplied | 'id' | 'motion'> {
        return this._wrap(this.set('loop', loop));
    }

    addTransition(transition: AnimationTransitionInput): AnimationStateBuilder<TSupplied | 'id' | 'motion'> {
        const current = (this.peek().transitions ?? []) as readonly AnimationTransitionDefinition[];
        const built = buildAnimationTransitionDefinition(transition);
        return this._wrap(this.set('transitions', Object.freeze([...current, built])));
    }

    transitionTo(
        to: string,
        configure?: (transition: AnimationTransitionBuilder) => AnimationTransitionBuilder
    ): AnimationStateBuilder<TSupplied | 'id' | 'motion'> {
        let transition = new AnimationTransitionBuilder(to);
        if (configure) {
            transition = configure(transition);
        }
        return this.addTransition(transition);
    }

    public override build(this: AnimationStateBuilder<RequiredKeys<AnimationStateDefinition>>): DeepReadonly<AnimationStateDefinition> {
        return super.build();
    }

    private _wrap<TNext extends keyof AnimationStateDefinition>(
        builder: Builder<AnimationStateDefinition, TNext>
    ): AnimationStateBuilder<TNext> {
        return AnimationStateBuilder._reconstruct<TNext>(
            (builder as any)[$state],
            (builder as any)[$node],
            (builder as any).validators,
            (builder as any).beforeHooks,
            (builder as any).afterHooks,
            (builder as any).shouldFreeze
        );
    }

    /** @internal */
    static _reconstruct<S extends keyof AnimationStateDefinition>(
        seed: Record<string, unknown>,
        node: StateNode,
        validators: readonly unknown[] = [],
        beforeHooks: readonly unknown[] = [],
        afterHooks: readonly unknown[] = [],
        shouldFreeze = false
    ): AnimationStateBuilder<S> {
        const instance = Object.create(AnimationStateBuilder.prototype) as AnimationStateBuilder<S>;
        (instance as any)[$state] = seed;
        (instance as any)[$node] = node;
        (instance as any).validators = validators;
        (instance as any).beforeHooks = beforeHooks;
        (instance as any).afterHooks = afterHooks;
        (instance as any).shouldFreeze = shouldFreeze;
        return instance;
    }
}

// ─── AnimationStateMachineBuilder ────────────────────────────────────────────

export class AnimationStateMachineBuilder extends Builder<
    AnimationStateMachineDefinition,
    TSupplied extends keyof AnimationStateMachineDefinition = 'entryState' | 'states'
> {
    constructor(entryState?: string) {
        super({
            entryState: entryState ?? '',
            states: Object.freeze([]) as readonly AnimationStateDefinition[],
            anyStateTransitions: Object.freeze([]) as readonly AnimationTransitionDefinition[],
        });
    }

    withEntryState(entryState: string): AnimationStateMachineBuilder<TSupplied | 'entryState' | 'states'> {
        return this._wrap(this.set('entryState', entryState));
    }

    addState(state: AnimationStateInput): AnimationStateMachineBuilder<TSupplied | 'entryState' | 'states'> {
        const current = (this.peek().states ?? []) as readonly AnimationStateDefinition[];
        const built = buildAnimationStateDefinition(state);
        const next = this._wrap(this.set('states', Object.freeze([...current, built])));
        const currentEntry = next.peek().entryState;
        if (!currentEntry) {
            return next._wrap(next.set('entryState', built.id));
        }
        return next;
    }

    state(
        id: string,
        motion: AnimationMotionInput,
        configure?: (state: AnimationStateBuilder) => AnimationStateBuilder
    ): AnimationStateMachineBuilder<TSupplied | 'entryState' | 'states'> {
        let stateBuilder = new AnimationStateBuilder(id, motion);
        if (configure) {
            stateBuilder = configure(stateBuilder);
        }
        return this.addState(stateBuilder);
    }

    addAnyStateTransition(transition: AnimationTransitionInput): AnimationStateMachineBuilder<TSupplied | 'entryState' | 'states'> {
        const current = (this.peek().anyStateTransitions ?? []) as readonly AnimationTransitionDefinition[];
        const built = buildAnimationTransitionDefinition(transition);
        return this._wrap(this.set('anyStateTransitions', Object.freeze([...current, built])));
    }

    anyState(
        to: string,
        configure?: (transition: AnimationTransitionBuilder) => AnimationTransitionBuilder
    ): AnimationStateMachineBuilder<TSupplied | 'entryState' | 'states'> {
        let transition = new AnimationTransitionBuilder(to);
        if (configure) {
            transition = configure(transition);
        }
        return this.addAnyStateTransition(transition);
    }

    public override build(this: AnimationStateMachineBuilder<RequiredKeys<AnimationStateMachineDefinition>>): DeepReadonly<AnimationStateMachineDefinition> {
        return super.build();
    }

    private _wrap<TNext extends keyof AnimationStateMachineDefinition>(
        builder: Builder<AnimationStateMachineDefinition, TNext>
    ): AnimationStateMachineBuilder<TNext> {
        return AnimationStateMachineBuilder._reconstruct<TNext>(
            (builder as any)[$state],
            (builder as any)[$node],
            (builder as any).validators,
            (builder as any).beforeHooks,
            (builder as any).afterHooks,
            (builder as any).shouldFreeze
        );
    }

    /** @internal */
    static _reconstruct<S extends keyof AnimationStateMachineDefinition>(
        seed: Record<string, unknown>,
        node: StateNode,
        validators: readonly unknown[] = [],
        beforeHooks: readonly unknown[] = [],
        afterHooks: readonly unknown[] = [],
        shouldFreeze = false
    ): AnimationStateMachineBuilder<S> {
        const instance = Object.create(AnimationStateMachineBuilder.prototype) as AnimationStateMachineBuilder<S>;
        (instance as any)[$state] = seed;
        (instance as any)[$node] = node;
        (instance as any).validators = validators;
        (instance as any).beforeHooks = beforeHooks;
        (instance as any).afterHooks = afterHooks;
        (instance as any).shouldFreeze = shouldFreeze;
        return instance;
    }
}

// ─── AnimationIkLayerBuilder ─────────────────────────────────────────────────

export class AnimationIkLayerBuilder extends Builder<
    AnimationIkLayerDefinition,
    TSupplied extends keyof AnimationIkLayerDefinition = 'id' | 'jobs'
> {
    constructor(id: string) {
        super({ id, jobs: Object.freeze([]) as readonly AnimationIkJobDefinition[] });
    }

    withWeight(weight: number): AnimationIkLayerBuilder<TSupplied | 'id' | 'jobs'> {
        return this._wrap(this.set('weight', weight));
    }

    addJob(job: AnimationIkJobDefinition): AnimationIkLayerBuilder<TSupplied | 'id' | 'jobs'> {
        const current = (this.peek().jobs ?? []) as readonly AnimationIkJobDefinition[];
        return this._wrap(this.set('jobs', Object.freeze([...current, Object.freeze({ ...job })])));
    }

    public override build(this: AnimationIkLayerBuilder<RequiredKeys<AnimationIkLayerDefinition>>): DeepReadonly<AnimationIkLayerDefinition> {
        return super.build();
    }

    private _wrap<TNext extends keyof AnimationIkLayerDefinition>(
        builder: Builder<AnimationIkLayerDefinition, TNext>
    ): AnimationIkLayerBuilder<TNext> {
        return AnimationIkLayerBuilder._reconstruct<TNext>(
            (builder as any)[$state],
            (builder as any)[$node],
            (builder as any).validators,
            (builder as any).beforeHooks,
            (builder as any).afterHooks,
            (builder as any).shouldFreeze
        );
    }

    /** @internal */
    static _reconstruct<S extends keyof AnimationIkLayerDefinition>(
        seed: Record<string, unknown>,
        node: StateNode,
        validators: readonly unknown[] = [],
        beforeHooks: readonly unknown[] = [],
        afterHooks: readonly unknown[] = [],
        shouldFreeze = false
    ): AnimationIkLayerBuilder<S> {
        const instance = Object.create(AnimationIkLayerBuilder.prototype) as AnimationIkLayerBuilder<S>;
        (instance as any)[$state] = seed;
        (instance as any)[$node] = node;
        (instance as any).validators = validators;
        (instance as any).beforeHooks = beforeHooks;
        (instance as any).afterHooks = afterHooks;
        (instance as any).shouldFreeze = shouldFreeze;
        return instance;
    }
}

// ─── AnimationLayerBuilder ───────────────────────────────────────────────────

export class AnimationLayerBuilder extends Builder<
    AnimationLayerDefinition,
    TSupplied extends keyof AnimationLayerDefinition = 'id' | 'stateMachine'
> {
    constructor(id: string, stateMachine: AnimationStateMachineInput) {
        super({
            id,
            stateMachine: buildAnimationStateMachineDefinition(stateMachine),
        });
    }

    withWeight(weight: number): AnimationLayerBuilder<TSupplied | 'id' | 'stateMachine'> {
        return this._wrap(this.set('weight', weight));
    }

    withMode(mode: AnimationLayerBlendMode): AnimationLayerBuilder<TSupplied | 'id' | 'stateMachine'> {
        return this._wrap(this.set('mode', mode));
    }

    withBoneMask(bones: readonly string[]): AnimationLayerBuilder<TSupplied | 'id' | 'stateMachine'> {
        return this._wrap(this.set('boneMask', Object.freeze([...bones])));
    }

    withStateMachine(stateMachine: AnimationStateMachineInput): AnimationLayerBuilder<TSupplied | 'id' | 'stateMachine'> {
        return this._wrap(this.set('stateMachine', buildAnimationStateMachineDefinition(stateMachine)));
    }

    addIkLayer(layer: AnimationIkLayerInput): AnimationLayerBuilder<TSupplied | 'id' | 'stateMachine'> {
        const current = ((this.peek() as any).ikLayers ?? []) as readonly AnimationIkLayerDefinition[];
        const built = buildAnimationIkLayerDefinition(layer);
        return this._wrap(this.set('ikLayers' as keyof AnimationLayerDefinition, Object.freeze([...current, built]) as any));
    }

    public override build(this: AnimationLayerBuilder<RequiredKeys<AnimationLayerDefinition>>): DeepReadonly<AnimationLayerDefinition> {
        return super.build();
    }

    private _wrap<TNext extends keyof AnimationLayerDefinition>(
        builder: Builder<AnimationLayerDefinition, TNext>
    ): AnimationLayerBuilder<TNext> {
        return AnimationLayerBuilder._reconstruct<TNext>(
            (builder as any)[$state],
            (builder as any)[$node],
            (builder as any).validators,
            (builder as any).beforeHooks,
            (builder as any).afterHooks,
            (builder as any).shouldFreeze
        );
    }

    /** @internal */
    static _reconstruct<S extends keyof AnimationLayerDefinition>(
        seed: Record<string, unknown>,
        node: StateNode,
        validators: readonly unknown[] = [],
        beforeHooks: readonly unknown[] = [],
        afterHooks: readonly unknown[] = [],
        shouldFreeze = false
    ): AnimationLayerBuilder<S> {
        const instance = Object.create(AnimationLayerBuilder.prototype) as AnimationLayerBuilder<S>;
        (instance as any)[$state] = seed;
        (instance as any)[$node] = node;
        (instance as any).validators = validators;
        (instance as any).beforeHooks = beforeHooks;
        (instance as any).afterHooks = afterHooks;
        (instance as any).shouldFreeze = shouldFreeze;
        return instance;
    }
}

// ─── AnimationControllerBuilder ──────────────────────────────────────────────

type ControllerClips = AnimationControllerDefinition<readonly AnimationParameterDefinition[]>['clips'];

export class AnimationControllerBuilder extends Builder<
    AnimationControllerDefinition<readonly AnimationParameterDefinition[]>,
    TSupplied extends keyof AnimationControllerDefinition<readonly AnimationParameterDefinition[]> = 'rig' | 'clips' | 'layers'
> {
    constructor(rig: AnimationRigDefinition) {
        super({
            rig: cloneRigDefinition(rig),
            clips: Object.freeze([]) as unknown as ControllerClips,
            layers: Object.freeze([]) as readonly AnimationLayerDefinition[],
        });
    }

    addClip(clip: ControllerClips[number]): AnimationControllerBuilder<TSupplied | 'rig' | 'clips' | 'layers'> {
        const current = (this.peek().clips ?? []) as unknown as readonly ControllerClips[number][];
        return this._wrap(this.set('clips', Object.freeze([...current, clip]) as unknown as ControllerClips));
    }

    addParameter(parameter: AnimationParameterDefinition): AnimationControllerBuilder<TSupplied | 'rig' | 'clips' | 'layers'> {
        const current = (this.peek().parameters ?? []) as readonly AnimationParameterDefinition[];
        return this._wrap(this.set('parameters', Object.freeze([...current, cloneParameterDefinition(parameter)])));
    }

    parameter<TKind extends AnimationParameterKind>(
        name: string,
        kind: TKind,
        defaultValue?: AnimationParameterValue<TKind>
    ): AnimationControllerBuilder<TSupplied | 'rig' | 'clips' | 'layers'> {
        return this.addParameter({
            name,
            kind,
            ...(defaultValue !== undefined ? { defaultValue } : {}),
        });
    }

    addLayer(layer: AnimationLayerInput): AnimationControllerBuilder<TSupplied | 'rig' | 'clips' | 'layers'> {
        const current = (this.peek().layers ?? []) as readonly AnimationLayerDefinition[];
        const built = buildAnimationLayerDefinition(layer);
        return this._wrap(this.set('layers', Object.freeze([...current, built])));
    }

    layer(
        id: string,
        stateMachine: AnimationStateMachineInput,
        configure?: (layer: AnimationLayerBuilder) => AnimationLayerBuilder
    ): AnimationControllerBuilder<TSupplied | 'rig' | 'clips' | 'layers'> {
        let layerBuilder = new AnimationLayerBuilder(id, stateMachine);
        if (configure) {
            layerBuilder = configure(layerBuilder);
        }
        return this.addLayer(layerBuilder);
    }

    withRootMotion(rootMotion: AnimationRootMotionDefinition | null): AnimationControllerBuilder<TSupplied | 'rig' | 'clips' | 'layers'> {
        const cloned = rootMotion ? cloneRootMotionDefinition(rootMotion) : null;
        return this._wrap(this.set('rootMotion', cloned));
    }

    public override build(
        this: AnimationControllerBuilder<RequiredKeys<AnimationControllerDefinition<readonly AnimationParameterDefinition[]>>>
    ): DeepReadonly<AnimationControllerDefinition<readonly AnimationParameterDefinition[]>> {
        return super.build();
    }

    private _wrap<TNext extends keyof AnimationControllerDefinition<readonly AnimationParameterDefinition[]>>(
        builder: Builder<AnimationControllerDefinition<readonly AnimationParameterDefinition[]>, TNext>
    ): AnimationControllerBuilder<TNext> {
        return AnimationControllerBuilder._reconstruct<TNext>(
            (builder as any)[$state],
            (builder as any)[$node],
            (builder as any).validators,
            (builder as any).beforeHooks,
            (builder as any).afterHooks,
            (builder as any).shouldFreeze
        );
    }

    /** @internal */
    static _reconstruct<S extends keyof AnimationControllerDefinition<readonly AnimationParameterDefinition[]>>(
        seed: Record<string, unknown>,
        node: StateNode,
        validators: readonly unknown[] = [],
        beforeHooks: readonly unknown[] = [],
        afterHooks: readonly unknown[] = [],
        shouldFreeze = false
    ): AnimationControllerBuilder<S> {
        const instance = Object.create(AnimationControllerBuilder.prototype) as AnimationControllerBuilder<S>;
        (instance as any)[$state] = seed;
        (instance as any)[$node] = node;
        (instance as any).validators = validators;
        (instance as any).beforeHooks = beforeHooks;
        (instance as any).afterHooks = afterHooks;
        (instance as any).shouldFreeze = shouldFreeze;
        return instance;
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
