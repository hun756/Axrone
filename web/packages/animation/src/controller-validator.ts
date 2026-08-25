import { validateAnimationMotionDefinition } from './blend-graph';
import {
    buildAnimationControllerDefinition,
    buildAnimationLayerDefinition,
    buildAnimationStateMachineDefinition,
    type AnimationControllerInput,
    type AnimationLayerInput,
    type AnimationStateMachineInput,
} from './controller-builder';
import { isFiniteNumber } from './internal';
import type {
    AnimationConditionDefinition,
    AnimationIkLayerDefinition,
    AnimationLayerBlendMode,
    AnimationStateDefinition,
    AnimationTransitionDefinition,
} from './types';

export interface AnimationControllerGraphDiagnostic {
    readonly code: string;
    readonly message: string;
    readonly path: string;
}

export interface AnimationControllerGraphValidationOptions {
    readonly knownClipIds?: readonly string[];
    readonly knownParameters?: readonly string[];
    readonly knownBones?: readonly string[];
}

const VALID_LAYER_MODES: readonly AnimationLayerBlendMode[] = ['override', 'additive'] as const;

const pushDiagnostic = (
    diagnostics: AnimationControllerGraphDiagnostic[],
    code: string,
    message: string,
    path: string
): void => {
    diagnostics.push(Object.freeze({ code, message, path }));
};

const validateCondition = (
    condition: AnimationConditionDefinition,
    diagnostics: AnimationControllerGraphDiagnostic[],
    options: AnimationControllerGraphValidationOptions,
    path: string
): void => {
    if (
        options.knownParameters &&
        options.knownParameters.includes(String(condition.parameter)) === false
    ) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.parameter.unknown',
            `Unknown parameter '${condition.parameter}'`,
            `${path}.parameter`
        );
    }
    switch (condition.kind) {
        case 'float':
        case 'int':
            if (!isFiniteNumber(condition.value)) {
                pushDiagnostic(
                    diagnostics,
                    'animation.controller.condition.value.invalid',
                    'Numeric transition conditions require a finite value',
                    `${path}.value`
                );
            }
            break;
        case 'bool':
        case 'trigger':
            break;
        default:
            pushDiagnostic(
                diagnostics,
                'animation.controller.condition.kind.unsupported',
                `Unsupported condition kind '${String((condition as { kind?: unknown }).kind)}'`,
                path
            );
            break;
    }
};

const validateTransition = (
    transition: AnimationTransitionDefinition,
    diagnostics: AnimationControllerGraphDiagnostic[],
    options: AnimationControllerGraphValidationOptions,
    knownStates: ReadonlySet<string>,
    path: string
): void => {
    if (knownStates.has(String(transition.to)) === false) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.state.unknown',
            `Unknown transition target '${transition.to}'`,
            `${path}.to`
        );
    }
    if (transition.duration !== undefined && !isFiniteNumber(transition.duration)) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.transition.duration.invalid',
            'Transition duration must be finite',
            `${path}.duration`
        );
    }
    if (transition.offset !== undefined && !isFiniteNumber(transition.offset)) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.transition.offset.invalid',
            'Transition offset must be finite',
            `${path}.offset`
        );
    }
    if (transition.exitTime !== undefined && !isFiniteNumber(transition.exitTime)) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.transition.exitTime.invalid',
            'Transition exitTime must be finite',
            `${path}.exitTime`
        );
    }
    if (transition.priority !== undefined && !isFiniteNumber(transition.priority)) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.transition.priority.invalid',
            'Transition priority must be finite',
            `${path}.priority`
        );
    }
    for (let index = 0; index < (transition.conditions?.length ?? 0); index += 1) {
        validateCondition(
            transition.conditions![index]!,
            diagnostics,
            options,
            `${path}.conditions[${index}]`
        );
    }
};

const validateState = (
    state: AnimationStateDefinition,
    diagnostics: AnimationControllerGraphDiagnostic[],
    options: AnimationControllerGraphValidationOptions,
    knownStates: ReadonlySet<string>,
    path: string
): void => {
    if (!String(state.id)) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.state.id.empty',
            'States require a non-empty id',
            `${path}.id`
        );
    }
    if (state.speed !== undefined && !isFiniteNumber(state.speed)) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.state.speed.invalid',
            'State speed must be finite',
            `${path}.speed`
        );
    }
    const motionDiagnostics = validateAnimationMotionDefinition(state.motion, {
        knownClipIds: options.knownClipIds,
        knownParameters: options.knownParameters,
    });
    for (let index = 0; index < motionDiagnostics.length; index += 1) {
        const diagnostic = motionDiagnostics[index]!;
        pushDiagnostic(
            diagnostics,
            diagnostic.code.replace('animation.blendGraph', 'animation.controller.motion'),
            diagnostic.message,
            `${path}.motion${diagnostic.path === 'motion' ? '' : diagnostic.path.slice('motion'.length)}`
        );
    }
    for (let index = 0; index < (state.transitions?.length ?? 0); index += 1) {
        validateTransition(
            state.transitions![index]!,
            diagnostics,
            options,
            knownStates,
            `${path}.transitions[${index}]`
        );
    }
};

export const validateAnimationStateMachineDefinition = (
    stateMachine: AnimationStateMachineInput,
    options: AnimationControllerGraphValidationOptions = {}
): readonly AnimationControllerGraphDiagnostic[] => {
    const diagnostics: AnimationControllerGraphDiagnostic[] = [];
    const resolved = buildAnimationStateMachineDefinition(stateMachine);

    if (resolved.states.length === 0) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.states.empty',
            'State machines require at least one state',
            'stateMachine.states'
        );
    }

    const knownStates = new Set<string>();
    for (let index = 0; index < resolved.states.length; index += 1) {
        const state = resolved.states[index]!;
        if (knownStates.has(String(state.id))) {
            pushDiagnostic(
                diagnostics,
                'animation.controller.state.duplicate',
                `Duplicate state '${state.id}'`,
                `stateMachine.states[${index}].id`
            );
        }
        knownStates.add(String(state.id));
    }

    if (knownStates.has(String(resolved.entryState)) === false) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.state.entry.unknown',
            `Unknown entry state '${resolved.entryState}'`,
            'stateMachine.entryState'
        );
    }

    for (let index = 0; index < resolved.states.length; index += 1) {
        validateState(
            resolved.states[index]!,
            diagnostics,
            options,
            knownStates,
            `stateMachine.states[${index}]`
        );
    }

    for (let index = 0; index < (resolved.anyStateTransitions?.length ?? 0); index += 1) {
        validateTransition(
            resolved.anyStateTransitions![index]!,
            diagnostics,
            options,
            knownStates,
            `stateMachine.anyStateTransitions[${index}]`
        );
    }

    return Object.freeze(diagnostics);
};

const validateIkLayer = (
    layer: AnimationIkLayerDefinition,
    diagnostics: AnimationControllerGraphDiagnostic[],
    options: AnimationControllerGraphValidationOptions,
    path: string
): void => {
    if (!String(layer.id)) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.ikLayer.id.empty',
            'IK layers require a non-empty id',
            `${path}.id`
        );
    }
    if (layer.weight !== undefined && !isFiniteNumber(layer.weight)) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.ikLayer.weight.invalid',
            'IK layer weight must be finite',
            `${path}.weight`
        );
    }
    if (layer.jobs.length === 0) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.ikLayer.jobs.empty',
            'IK layers require at least one job',
            `${path}.jobs`
        );
    }
    for (let index = 0; index < layer.jobs.length; index += 1) {
        const job = layer.jobs[index]!;
        if (
            options.knownBones &&
            options.knownBones.includes(job.rootBone) === false
        ) {
            pushDiagnostic(
                diagnostics,
                'animation.controller.bone.unknown',
                `Unknown bone '${job.rootBone}'`,
                `${path}.jobs[${index}].rootBone`
            );
        }
        if (
            options.knownBones &&
            options.knownBones.includes(job.tipBone) === false
        ) {
            pushDiagnostic(
                diagnostics,
                'animation.controller.bone.unknown',
                `Unknown bone '${job.tipBone}'`,
                `${path}.jobs[${index}].tipBone`
            );
        }
        if (
            typeof job.targetBone === 'string' &&
            options.knownBones &&
            options.knownBones.includes(job.targetBone) === false
        ) {
            pushDiagnostic(
                diagnostics,
                'animation.controller.bone.unknown',
                `Unknown bone '${job.targetBone}'`,
                `${path}.jobs[${index}].targetBone`
            );
        }
    }
};

export const validateAnimationLayerDefinition = (
    layer: AnimationLayerInput,
    options: AnimationControllerGraphValidationOptions = {}
): readonly AnimationControllerGraphDiagnostic[] => {
    const diagnostics: AnimationControllerGraphDiagnostic[] = [];
    const resolved = buildAnimationLayerDefinition(layer);

    if (!String(resolved.id)) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.layer.id.empty',
            'Layers require a non-empty id',
            'layer.id'
        );
    }
    if (resolved.weight !== undefined && !isFiniteNumber(resolved.weight)) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.layer.weight.invalid',
            'Layer weight must be finite',
            'layer.weight'
        );
    }
    if (resolved.mode !== undefined && VALID_LAYER_MODES.includes(resolved.mode) === false) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.layer.mode.invalid',
            `Unsupported layer mode '${String(resolved.mode)}'`,
            'layer.mode'
        );
    }
    for (let index = 0; index < (resolved.boneMask?.length ?? 0); index += 1) {
        const bone = resolved.boneMask![index]!;
        if (options.knownBones && options.knownBones.includes(bone) === false) {
            pushDiagnostic(
                diagnostics,
                'animation.controller.bone.unknown',
                `Unknown bone '${bone}'`,
                `layer.boneMask[${index}]`
            );
        }
    }

    diagnostics.push(
        ...validateAnimationStateMachineDefinition(resolved.stateMachine, options).map((diagnostic) =>
            Object.freeze({
                code: diagnostic.code,
                message: diagnostic.message,
                path: `layer.${diagnostic.path}`,
            })
        )
    );

    for (let index = 0; index < (resolved.ikLayers?.length ?? 0); index += 1) {
        validateIkLayer(resolved.ikLayers![index]!, diagnostics, options, `layer.ikLayers[${index}]`);
    }

    return Object.freeze(diagnostics);
};

export const validateAnimationControllerDefinition = (
    controller: AnimationControllerInput,
    options: AnimationControllerGraphValidationOptions = {}
): readonly AnimationControllerGraphDiagnostic[] => {
    const diagnostics: AnimationControllerGraphDiagnostic[] = [];
    const resolved = buildAnimationControllerDefinition(controller);
    const knownClipIds = options.knownClipIds ?? resolved.clips.map((clip) => String(clip.id));
    const knownParameters =
        options.knownParameters ?? resolved.parameters?.map((parameter) => parameter.name) ?? [];
    const knownBones = options.knownBones ?? resolved.rig.bones.map((bone) => bone.name);

    if (resolved.rig.bones.length === 0) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.rig.bones.empty',
            'Controllers require at least one rig bone',
            'controller.rig.bones'
        );
    }
    const seenBones = new Set<string>();
    for (let index = 0; index < resolved.rig.bones.length; index += 1) {
        const bone = resolved.rig.bones[index]!;
        if (!bone.name) {
            pushDiagnostic(
                diagnostics,
                'animation.controller.bone.name.empty',
                'Rig bones require a non-empty name',
                `controller.rig.bones[${index}].name`
            );
        }
        if (seenBones.has(bone.name)) {
            pushDiagnostic(
                diagnostics,
                'animation.controller.bone.duplicate',
                `Duplicate rig bone '${bone.name}'`,
                `controller.rig.bones[${index}].name`
            );
        }
        seenBones.add(bone.name);
    }

    if (resolved.clips.length === 0) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.clips.empty',
            'Controllers require at least one clip',
            'controller.clips'
        );
    }
    const seenClips = new Set<string>();
    for (let index = 0; index < resolved.clips.length; index += 1) {
        const clipId = String(resolved.clips[index]!.id);
        if (seenClips.has(clipId)) {
            pushDiagnostic(
                diagnostics,
                'animation.controller.clip.duplicate',
                `Duplicate clip '${clipId}'`,
                `controller.clips[${index}].id`
            );
        }
        seenClips.add(clipId);
    }

    const seenParameters = new Set<string>();
    for (let index = 0; index < (resolved.parameters?.length ?? 0); index += 1) {
        const parameter = resolved.parameters![index]!;
        if (seenParameters.has(parameter.name)) {
            pushDiagnostic(
                diagnostics,
                'animation.controller.parameter.duplicate',
                `Duplicate parameter '${parameter.name}'`,
                `controller.parameters[${index}].name`
            );
        }
        seenParameters.add(parameter.name);
    }

    if (resolved.layers.length === 0) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.layers.empty',
            'Controllers require at least one layer',
            'controller.layers'
        );
    }
    const seenLayers = new Set<string>();
    for (let index = 0; index < resolved.layers.length; index += 1) {
        const layer = resolved.layers[index]!;
        if (seenLayers.has(String(layer.id))) {
            pushDiagnostic(
                diagnostics,
                'animation.controller.layer.duplicate',
                `Duplicate layer '${layer.id}'`,
                `controller.layers[${index}].id`
            );
        }
        seenLayers.add(String(layer.id));

        diagnostics.push(
            ...validateAnimationLayerDefinition(layer, {
                knownClipIds,
                knownParameters,
                knownBones,
            }).map((diagnostic) =>
                Object.freeze({
                    code: diagnostic.code,
                    message: diagnostic.message,
                    path: diagnostic.path.replace(/^layer\./, `controller.layers[${index}].`),
                })
            )
        );
    }

    if (
        resolved.rootMotion &&
        knownBones.includes(resolved.rootMotion.bone) === false
    ) {
        pushDiagnostic(
            diagnostics,
            'animation.controller.rootMotion.bone.unknown',
            `Unknown root motion bone '${resolved.rootMotion.bone}'`,
            'controller.rootMotion.bone'
        );
    }

    return Object.freeze(diagnostics);
};
