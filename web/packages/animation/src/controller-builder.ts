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

export class AnimationTransitionBuilder {
    private _duration?: number;
    private _offset?: number;
    private _exitTime?: number;
    private _fixedDuration?: boolean;
    private _canInterrupt?: boolean;
    private _priority?: number;
    private readonly _conditions: AnimationConditionDefinition[] = [];

    constructor(public readonly to: string) {}

    withDuration(duration: number): this {
        this._duration = duration;
        return this;
    }

    withOffset(offset: number): this {
        this._offset = offset;
        return this;
    }

    withExitTime(exitTime: number): this {
        this._exitTime = exitTime;
        return this;
    }

    withFixedDuration(fixedDuration = true): this {
        this._fixedDuration = fixedDuration;
        return this;
    }

    withInterruptible(canInterrupt = true): this {
        this._canInterrupt = canInterrupt;
        return this;
    }

    withPriority(priority: number): this {
        this._priority = priority;
        return this;
    }

    addCondition(condition: AnimationConditionDefinition): this {
        this._conditions.push(cloneCondition(condition));
        return this;
    }

    whenFloat(parameter: string, operator: AnimationTransitionOperator, value: number): this {
        return this.addCondition({ kind: 'float', parameter, operator, value });
    }

    whenInt(parameter: string, operator: AnimationTransitionOperator, value: number): this {
        return this.addCondition({ kind: 'int', parameter, operator, value });
    }

    whenBool(parameter: string, value: boolean): this {
        return this.addCondition({ kind: 'bool', parameter, value });
    }

    whenTriggered(parameter: string): this {
        return this.addCondition({ kind: 'trigger', parameter });
    }

    build(): AnimationTransitionDefinition {
        return buildAnimationTransitionDefinition({
            to: this.to,
            ...spreadIfFinite('duration', this._duration),
            ...spreadIfFinite('offset', this._offset),
            ...spreadIfFinite('exitTime', this._exitTime),
            ...(typeof this._fixedDuration === 'boolean' ? { fixedDuration: this._fixedDuration } : {}),
            ...(typeof this._canInterrupt === 'boolean' ? { canInterrupt: this._canInterrupt } : {}),
            ...spreadIfFinite('priority', this._priority),
            ...(this._conditions.length > 0 ? { conditions: Object.freeze([...this._conditions]) } : {}),
        });
    }
}

export class AnimationStateBuilder {
    private _speed?: number;
    private _loop?: boolean;
    private readonly _transitions: AnimationTransitionInput[] = [];

    constructor(
        public readonly id: string,
        private _motion: AnimationMotionInput
    ) {}

    withMotion(motion: AnimationMotionInput): this {
        this._motion = motion;
        return this;
    }

    withSpeed(speed: number): this {
        this._speed = speed;
        return this;
    }

    withLoop(loop: boolean): this {
        this._loop = loop;
        return this;
    }

    addTransition(transition: AnimationTransitionInput): this {
        this._transitions.push(transition);
        return this;
    }

    transitionTo(
        to: string,
        configure?: (transition: AnimationTransitionBuilder) => void
    ): this {
        const transition = new AnimationTransitionBuilder(to);
        configure?.(transition);
        return this.addTransition(transition);
    }

    build(): AnimationStateDefinition {
        return Object.freeze({
            id: this.id,
            motion: buildAnimationMotionDefinition(this._motion),
            ...spreadIfFinite('speed', this._speed),
            ...(typeof this._loop === 'boolean' ? { loop: this._loop } : {}),
            ...(this._transitions.length > 0
                ? { transitions: Object.freeze(this._transitions.map(buildAnimationTransitionDefinition)) }
                : {}),
        });
    }
}

export class AnimationStateMachineBuilder {
    private _entryState?: string;
    private readonly _states: AnimationStateInput[] = [];
    private readonly _anyStateTransitions: AnimationTransitionInput[] = [];

    constructor(entryState?: string) {
        this._entryState = entryState;
    }

    withEntryState(entryState: string): this {
        this._entryState = entryState;
        return this;
    }

    addState(state: AnimationStateInput): this {
        this._states.push(state);
        if (!this._entryState) {
            this._entryState = 'build' in state ? state.id : state.id;
        }
        return this;
    }

    state(
        id: string,
        motion: AnimationMotionInput,
        configure?: (state: AnimationStateBuilder) => void
    ): this {
        const state = new AnimationStateBuilder(id, motion);
        configure?.(state);
        return this.addState(state);
    }

    addAnyStateTransition(transition: AnimationTransitionInput): this {
        this._anyStateTransitions.push(transition);
        return this;
    }

    anyState(
        to: string,
        configure?: (transition: AnimationTransitionBuilder) => void
    ): this {
        const transition = new AnimationTransitionBuilder(to);
        configure?.(transition);
        return this.addAnyStateTransition(transition);
    }

    build(): AnimationStateMachineDefinition {
        return buildAnimationStateMachineDefinition({
            entryState: this._entryState ?? this._states[0]?.id ?? '',
            states: Object.freeze(this._states.map(buildAnimationStateDefinition)),
            ...(this._anyStateTransitions.length > 0
                ? {
                      anyStateTransitions: Object.freeze(
                          this._anyStateTransitions.map(buildAnimationTransitionDefinition)
                      ),
                  }
                : {}),
        });
    }
}

export class AnimationIkLayerBuilder {
    private _weight?: number;
    private readonly _jobs: AnimationIkJobDefinition[] = [];

    constructor(public readonly id: string) {}

    withWeight(weight: number): this {
        this._weight = weight;
        return this;
    }

    addJob(job: AnimationIkJobDefinition): this {
        this._jobs.push(Object.freeze({ ...job }));
        return this;
    }

    build(): AnimationIkLayerDefinition {
        return buildAnimationIkLayerDefinition({
            id: this.id,
            ...spreadIfFinite('weight', this._weight),
            jobs: Object.freeze(this._jobs.map((job) => Object.freeze({ ...job }))),
        });
    }
}

export class AnimationLayerBuilder {
    private _weight?: number;
    private _mode?: AnimationLayerBlendMode;
    private _boneMask?: string[];
    private _stateMachine: AnimationStateMachineInput;
    private readonly _ikLayers: AnimationIkLayerInput[] = [];

    constructor(
        public readonly id: string,
        stateMachine: AnimationStateMachineInput
    ) {
        this._stateMachine = stateMachine;
    }

    withWeight(weight: number): this {
        this._weight = weight;
        return this;
    }

    withMode(mode: AnimationLayerBlendMode): this {
        this._mode = mode;
        return this;
    }

    withBoneMask(bones: readonly string[]): this {
        this._boneMask = [...bones];
        return this;
    }

    withStateMachine(stateMachine: AnimationStateMachineInput): this {
        this._stateMachine = stateMachine;
        return this;
    }

    addIkLayer(layer: AnimationIkLayerInput): this {
        this._ikLayers.push(layer);
        return this;
    }

    build(): AnimationLayerDefinition {
        return Object.freeze({
            id: this.id,
            ...spreadIfFinite('weight', this._weight),
            ...(this._mode ? { mode: this._mode } : {}),
            ...(this._boneMask ? { boneMask: Object.freeze([...this._boneMask]) } : {}),
            stateMachine: buildAnimationStateMachineDefinition(this._stateMachine),
            ...(this._ikLayers.length > 0
                ? { ikLayers: Object.freeze(this._ikLayers.map(buildAnimationIkLayerDefinition)) }
                : {}),
        });
    }
}

export class AnimationControllerBuilder {
    private readonly _clips: AnimationControllerDefinition<readonly AnimationParameterDefinition[]>['clips'][number][] = [];
    private readonly _parameters: AnimationParameterDefinition[] = [];
    private readonly _layers: AnimationLayerInput[] = [];
    private _rootMotion?: AnimationRootMotionDefinition | null;

    constructor(private readonly _rig: AnimationRigDefinition) {}

    addClip(clip: AnimationControllerDefinition<readonly AnimationParameterDefinition[]>['clips'][number]): this {
        this._clips.push(clip);
        return this;
    }

    addParameter(parameter: AnimationParameterDefinition): this {
        this._parameters.push(parameter);
        return this;
    }

    parameter<TKind extends AnimationParameterKind>(
        name: string,
        kind: TKind,
        defaultValue?: AnimationParameterValue<TKind>
    ): this {
        return this.addParameter({
            name,
            kind,
            ...(defaultValue !== undefined ? { defaultValue } : {}),
        });
    }

    addLayer(layer: AnimationLayerInput): this {
        this._layers.push(layer);
        return this;
    }

    layer(
        id: string,
        stateMachine: AnimationStateMachineInput,
        configure?: (layer: AnimationLayerBuilder) => void
    ): this {
        const layer = new AnimationLayerBuilder(id, stateMachine);
        configure?.(layer);
        return this.addLayer(layer);
    }

    withRootMotion(rootMotion: AnimationRootMotionDefinition | null): this {
        this._rootMotion = rootMotion;
        return this;
    }

    build(): AnimationControllerDefinition<readonly AnimationParameterDefinition[]> {
        return buildAnimationControllerDefinition({
            rig: this._rig,
            clips: Object.freeze(this._clips.map(cloneClipDefinition)),
            layers: Object.freeze(this._layers.map(buildAnimationLayerDefinition)),
            ...(this._parameters.length > 0
                ? { parameters: Object.freeze(this._parameters.map(cloneParameterDefinition)) }
                : {}),
            ...(this._rootMotion !== undefined
                ? { rootMotion: this._rootMotion ? cloneRootMotionDefinition(this._rootMotion) : null }
                : {}),
        });
    }
}

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

export const buildAnimationTransitionDefinition = (
    transition: AnimationTransitionInput
): AnimationTransitionDefinition =>
    transition instanceof AnimationTransitionBuilder
        ? transition.build()
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
        ? state.build()
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
        ? stateMachine.build()
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
        ? layer.build()
        : Object.freeze({
              id: layer.id,
              ...spreadIfFinite('weight', layer.weight),
              jobs: Object.freeze(layer.jobs.map((job) => Object.freeze({ ...job }))),
          });

export const buildAnimationLayerDefinition = (layer: AnimationLayerInput): AnimationLayerDefinition =>
    layer instanceof AnimationLayerBuilder
        ? layer.build()
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
        ? controller.build()
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
