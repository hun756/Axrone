import { isPlainObject } from '@axrone/utility';
import type {
    AnimationLayerDefinition,
    AnimationParameterDefinition,
    AnimationRootMotionDefinition,
} from '@axrone/animation/types';
import type { AssetImportDiagnostic } from '../asset-contract';
import type {
    GltfAnimationClipAsset,
    GltfAnimationClipMetadata,
    GltfAnimationControllerMetadata,
    GltfSceneJson,
} from '../types';
import {
    EMPTY_ARRAY,
    VALID_ANIMATION_PARAMETER_KINDS,
    VALID_ANIMATION_LAYER_MODES,
    VALID_ANIMATION_IK_SOLVERS,
    VALID_ANIMATION_CONDITION_KINDS,
    VALID_ANIMATION_CONDITION_OPERATORS,
    type PortableAnimationManifest,
} from './gltf-constants';
import {
    isFiniteNumber,
    isBooleanTuple3,
    isNumberTuple3,
    isNumberTuple4,
    maybeFreeze,
    cloneSerializableMetadata,
} from './gltf-utils';
import {
    createAnimationMetadataDiagnostic,
    resolvePortableSceneAnimationMetadataSource,
    resolveSceneAnimationMetadataSource,
    mergeAnimationMetadataSources,
    resolveScenePortableAnimationClipMetadataSources,
    resolveClipMetadataSourceForAnimation,
    toClipMetadata,
    mergeClipMetadata,
} from './gltf-animation-metadata';

const validateAnimationParameterReference = (
    parameter: unknown,
    parameterNames: ReadonlySet<string>
): boolean => typeof parameter === 'string' && parameterNames.has(parameter);

const validateAnimationConditionMetadata = (
    condition: unknown,
    parameterNames: ReadonlySet<string>
): boolean => {
    if (!isPlainObject(condition) || typeof condition.kind !== 'string') {
        return false;
    }

    if (VALID_ANIMATION_CONDITION_KINDS.has(condition.kind) === false) {
        return false;
    }

    if (!validateAnimationParameterReference(condition.parameter, parameterNames)) {
        return false;
    }

    switch (condition.kind) {
        case 'float':
        case 'int':
            return (
                typeof condition.operator === 'string' &&
                VALID_ANIMATION_CONDITION_OPERATORS.has(condition.operator) &&
                isFiniteNumber(condition.value)
            );
        case 'bool':
            return typeof condition.value === 'boolean';
        case 'trigger':
            return true;
        default:
            return false;
    }
};

const validateAnimationMotionMetadata = (
    motion: unknown,
    clipIds: ReadonlySet<string>,
    parameterNames: ReadonlySet<string>
): boolean => {
    if (!isPlainObject(motion) || typeof motion.kind !== 'string') {
        return false;
    }

    switch (motion.kind) {
        case 'clip':
            return typeof motion.clipId === 'string' && clipIds.has(motion.clipId);
        case 'blend1d':
            return (
                validateAnimationParameterReference(motion.parameter, parameterNames) &&
                Array.isArray(motion.children) &&
                motion.children.length > 0 &&
                motion.children.every(
                    (child) =>
                        isPlainObject(child) &&
                        isFiniteNumber(child.threshold) &&
                        validateAnimationMotionMetadata(child.motion, clipIds, parameterNames)
                )
            );
        case 'blend2d':
            return (
                validateAnimationParameterReference(motion.parameterX, parameterNames) &&
                validateAnimationParameterReference(motion.parameterY, parameterNames) &&
                Array.isArray(motion.children) &&
                motion.children.length > 0 &&
                motion.children.every(
                    (child) =>
                        isPlainObject(child) &&
                        Array.isArray(child.position) &&
                        child.position.length === 2 &&
                        child.position.every((entry) => isFiniteNumber(entry)) &&
                        validateAnimationMotionMetadata(child.motion, clipIds, parameterNames)
                )
            );
        case 'direct':
            return (
                Array.isArray(motion.children) &&
                motion.children.length > 0 &&
                motion.children.every(
                    (child) =>
                        isPlainObject(child) &&
                        (child.parameter === undefined ||
                            validateAnimationParameterReference(child.parameter, parameterNames)) &&
                        (child.weight === undefined || isFiniteNumber(child.weight)) &&
                        validateAnimationMotionMetadata(child.motion, clipIds, parameterNames)
                )
            );
        case 'additive':
            return (
                validateAnimationMotionMetadata(motion.base, clipIds, parameterNames) &&
                validateAnimationMotionMetadata(motion.additive, clipIds, parameterNames) &&
                (motion.parameter === undefined ||
                    validateAnimationParameterReference(motion.parameter, parameterNames)) &&
                (motion.weight === undefined || isFiniteNumber(motion.weight))
            );
        default:
            return false;
    }
};

const validateAnimationStateMachineMetadata = (
    stateMachine: unknown,
    clipIds: ReadonlySet<string>,
    parameterNames: ReadonlySet<string>
): boolean => {
    if (
        !isPlainObject(stateMachine) ||
        typeof stateMachine.entryState !== 'string' ||
        !Array.isArray(stateMachine.states)
    ) {
        return false;
    }

    const stateIds = new Set<string>();
    for (let index = 0; index < stateMachine.states.length; index += 1) {
        const state = stateMachine.states[index];
        if (!isPlainObject(state) || typeof state.id !== 'string') {
            return false;
        }
        stateIds.add(state.id);
    }

    if (!stateIds.has(stateMachine.entryState)) {
        return false;
    }

    const validateTransitions = (transitions: unknown): boolean =>
        transitions === undefined ||
        (Array.isArray(transitions) &&
            transitions.every(
                (transition) =>
                    isPlainObject(transition) &&
                    typeof transition.to === 'string' &&
                    stateIds.has(transition.to) &&
                    (transition.duration === undefined || isFiniteNumber(transition.duration)) &&
                    (transition.offset === undefined || isFiniteNumber(transition.offset)) &&
                    (transition.exitTime === undefined || isFiniteNumber(transition.exitTime)) &&
                    (transition.fixedDuration === undefined || typeof transition.fixedDuration === 'boolean') &&
                    (transition.canInterrupt === undefined || typeof transition.canInterrupt === 'boolean') &&
                    (transition.priority === undefined || isFiniteNumber(transition.priority)) &&
                    (transition.conditions === undefined ||
                        (Array.isArray(transition.conditions) &&
                            transition.conditions.every((condition) =>
                                validateAnimationConditionMetadata(condition, parameterNames)
                            )))
            ));

    return (
        validateTransitions(stateMachine.anyStateTransitions) &&
        stateMachine.states.every(
            (state) =>
                isPlainObject(state) &&
                validateAnimationMotionMetadata(state.motion, clipIds, parameterNames) &&
                (state.speed === undefined || isFiniteNumber(state.speed)) &&
                (state.loop === undefined || typeof state.loop === 'boolean') &&
                validateTransitions(state.transitions)
        )
    );
};

const validateAnimationIkLayerMetadata = (
    value: unknown,
    boneIds: ReadonlySet<string>
): boolean => {
    if (!isPlainObject(value) || typeof value.id !== 'string' || !Array.isArray(value.jobs)) {
        return false;
    }

    return value.jobs.every(
        (job) =>
            isPlainObject(job) &&
            typeof job.id === 'string' &&
            typeof job.solver === 'string' &&
            VALID_ANIMATION_IK_SOLVERS.has(job.solver) &&
            typeof job.rootBone === 'string' &&
            boneIds.has(job.rootBone) &&
            typeof job.tipBone === 'string' &&
            boneIds.has(job.tipBone) &&
            (job.targetBone === undefined ||
                (typeof job.targetBone === 'string' && boneIds.has(job.targetBone))) &&
            (job.targetPosition === undefined || isNumberTuple3(job.targetPosition)) &&
            (job.targetRotation === undefined || isNumberTuple4(job.targetRotation)) &&
            (job.precision === undefined || isFiniteNumber(job.precision)) &&
            (job.maxIterations === undefined || isFiniteNumber(job.maxIterations)) &&
            (job.weight === undefined || isFiniteNumber(job.weight)) &&
            (job.preserveTipRotation === undefined || typeof job.preserveTipRotation === 'boolean')
    );
};

export const sanitizeAnimationParameters = (
    value: unknown,
    sceneIndex: number,
    diagnostics: AssetImportDiagnostic[],
    freeze: boolean
): readonly AnimationParameterDefinition[] | undefined => {
    if (!Array.isArray(value)) {
        return undefined;
    }

    const parameters: AnimationParameterDefinition[] = [];
    const seenNames = new Set<string>();
    for (let index = 0; index < value.length; index += 1) {
        const entry = value[index];
        if (!isPlainObject(entry)) {
            diagnostics.push(
                createAnimationMetadataDiagnostic(sceneIndex, `parameter ${index} is not an object`)
            );
            continue;
        }

        const name = typeof entry.name === 'string' ? entry.name : null;
        const kind =
            typeof entry.kind === 'string' && VALID_ANIMATION_PARAMETER_KINDS.has(entry.kind)
                ? (entry.kind as AnimationParameterDefinition['kind'])
                : null;
        if (!name || !kind || VALID_ANIMATION_PARAMETER_KINDS.has(kind) === false) {
            diagnostics.push(
                createAnimationMetadataDiagnostic(
                    sceneIndex,
                    `parameter ${index} must provide a valid name and kind`
                )
            );
            continue;
        }

        if (seenNames.has(name)) {
            diagnostics.push(
                createAnimationMetadataDiagnostic(sceneIndex, `parameter '${name}' is duplicated`)
            );
            continue;
        }

        const defaultValue = entry.defaultValue;
        if (
            defaultValue !== undefined &&
            ((kind === 'float' || kind === 'int')
                ? isFiniteNumber(defaultValue) === false
                : typeof defaultValue !== 'boolean')
        ) {
            diagnostics.push(
                createAnimationMetadataDiagnostic(
                    sceneIndex,
                    `parameter '${name}' has an invalid defaultValue`
                )
            );
            continue;
        }

        seenNames.add(name);
        switch (kind) {
            case 'float':
            case 'int':
                parameters.push(
                    maybeFreeze(
                        {
                            name,
                            kind,
                            ...(typeof defaultValue === 'number' ? { defaultValue } : {}),
                        } satisfies AnimationParameterDefinition<string, 'float' | 'int'>,
                        freeze
                    )
                );
                break;
            case 'bool':
            case 'trigger':
                parameters.push(
                    maybeFreeze(
                        {
                            name,
                            kind,
                            ...(typeof defaultValue === 'boolean' ? { defaultValue } : {}),
                        } satisfies AnimationParameterDefinition<string, 'bool' | 'trigger'>,
                        freeze
                    )
                );
                break;
        }
    }

    return parameters.length > 0 ? Object.freeze(parameters) : undefined;
};

export const sanitizeAnimationLayers = (
    value: unknown,
    sceneIndex: number,
    diagnostics: AssetImportDiagnostic[],
    clipIds: ReadonlySet<string>,
    parameterNames: ReadonlySet<string>,
    boneIds: ReadonlySet<string>,
    freeze: boolean
): readonly AnimationLayerDefinition[] | undefined => {
    if (!Array.isArray(value)) {
        return undefined;
    }

    const layers: AnimationLayerDefinition[] = [];
    const seenLayerIds = new Set<string>();
    for (let index = 0; index < value.length; index += 1) {
        const entry = value[index];
        if (!isPlainObject(entry) || typeof entry.id !== 'string') {
            diagnostics.push(
                createAnimationMetadataDiagnostic(sceneIndex, `layer ${index} must provide an id`)
            );
            continue;
        }

        if (seenLayerIds.has(entry.id)) {
            diagnostics.push(
                createAnimationMetadataDiagnostic(sceneIndex, `layer '${entry.id}' is duplicated`)
            );
            continue;
        }

        if (
            entry.mode !== undefined &&
            (typeof entry.mode !== 'string' || VALID_ANIMATION_LAYER_MODES.has(entry.mode) === false)
        ) {
            diagnostics.push(
                createAnimationMetadataDiagnostic(sceneIndex, `layer '${entry.id}' has an unsupported mode`)
            );
            continue;
        }

        if (entry.weight !== undefined && isFiniteNumber(entry.weight) === false) {
            diagnostics.push(
                createAnimationMetadataDiagnostic(sceneIndex, `layer '${entry.id}' has an invalid weight`)
            );
            continue;
        }

        if (
            entry.boneMask !== undefined &&
            (!Array.isArray(entry.boneMask) ||
                entry.boneMask.some(
                    (boneId) => typeof boneId !== 'string' || boneIds.has(boneId) === false
                ))
        ) {
            diagnostics.push(
                createAnimationMetadataDiagnostic(
                    sceneIndex,
                    `layer '${entry.id}' references unknown bone ids in boneMask`
                )
            );
            continue;
        }

        if (!validateAnimationStateMachineMetadata(entry.stateMachine, clipIds, parameterNames)) {
            diagnostics.push(
                createAnimationMetadataDiagnostic(
                    sceneIndex,
                    `layer '${entry.id}' has an invalid state machine`
                )
            );
            continue;
        }

        if (
            entry.ikLayers !== undefined &&
            (!Array.isArray(entry.ikLayers) ||
                entry.ikLayers.some((ikLayer) => validateAnimationIkLayerMetadata(ikLayer, boneIds) === false))
        ) {
            diagnostics.push(
                createAnimationMetadataDiagnostic(sceneIndex, `layer '${entry.id}' has invalid IK metadata`)
            );
            continue;
        }

        const cloned = cloneSerializableMetadata(entry);
        if (!isPlainObject(cloned)) {
            diagnostics.push(
                createAnimationMetadataDiagnostic(sceneIndex, `layer '${entry.id}' could not be cloned`)
            );
            continue;
        }

        seenLayerIds.add(entry.id);
        layers.push(maybeFreeze(cloned as unknown as AnimationLayerDefinition, freeze));
    }

    return layers.length > 0 ? Object.freeze(layers) : undefined;
};

export const sanitizeAnimationRootMotion = (
    value: unknown,
    sceneIndex: number,
    diagnostics: AssetImportDiagnostic[],
    boneIds: ReadonlySet<string>,
    freeze: boolean
): AnimationRootMotionDefinition | null | undefined => {
    if (value === null) {
        return null;
    }

    if (!isPlainObject(value)) {
        return undefined;
    }

    if (typeof value.bone !== 'string' || boneIds.has(value.bone) === false) {
        diagnostics.push(
            createAnimationMetadataDiagnostic(
                sceneIndex,
                'rootMotion must reference an imported node id'
            )
        );
        return undefined;
    }

    if (
        value.projectTranslationAxes !== undefined &&
        isBooleanTuple3(value.projectTranslationAxes) === false
    ) {
        diagnostics.push(
            createAnimationMetadataDiagnostic(
                sceneIndex,
                'rootMotion.projectTranslationAxes must be a boolean tuple of length 3'
            )
        );
        return undefined;
    }

    if (
        (value.consume !== undefined && typeof value.consume !== 'boolean') ||
        (value.extractRotation !== undefined && typeof value.extractRotation !== 'boolean')
    ) {
        diagnostics.push(
            createAnimationMetadataDiagnostic(sceneIndex, 'rootMotion flags must be boolean values')
        );
        return undefined;
    }

    return maybeFreeze(
        {
            bone: value.bone,
            ...(value.consume !== undefined ? { consume: value.consume } : {}),
            ...(value.projectTranslationAxes !== undefined
                ? {
                      projectTranslationAxes: Object.freeze([
                          ...value.projectTranslationAxes,
                      ]) as readonly [boolean, boolean, boolean],
                  }
                : {}),
            ...(value.extractRotation !== undefined
                ? { extractRotation: value.extractRotation }
                : {}),
        } satisfies AnimationRootMotionDefinition,
        freeze
    );
};

export const resolveSceneAnimationControllerMetadata = (
    scene: GltfSceneJson | undefined,
    sceneIndex: number,
    clipIds: ReadonlySet<string>,
    boneIds: ReadonlySet<string>,
    animations: readonly GltfAnimationClipAsset[],
    manifest: PortableAnimationManifest | undefined,
    diagnostics: AssetImportDiagnostic[],
    freeze: boolean
): GltfAnimationControllerMetadata | undefined => {
    const source = mergeAnimationMetadataSources(
        resolvePortableSceneAnimationMetadataSource(manifest, scene, sceneIndex),
        resolveSceneAnimationMetadataSource(scene)
    );
    const parameters = source
        ? sanitizeAnimationParameters(source.parameters, sceneIndex, diagnostics, freeze)
        : undefined;
    const parameterNames = new Set(parameters?.map((parameter) => parameter.name) ?? EMPTY_ARRAY);
    const layers = source
        ? sanitizeAnimationLayers(
              source.layers,
              sceneIndex,
              diagnostics,
              clipIds,
              parameterNames,
              boneIds,
              freeze
          )
        : undefined;
    const rootMotion = source
        ? sanitizeAnimationRootMotion(
              source.rootMotion,
              sceneIndex,
              diagnostics,
              boneIds,
              freeze
          )
        : undefined;
    const sceneClipMetadataSources = resolveScenePortableAnimationClipMetadataSources(
        manifest,
        scene,
        sceneIndex,
        diagnostics,
        freeze
    );
    const clips = Object.freeze(
        animations
            .map((animation) =>
                mergeClipMetadata(
                    animation.id,
                    toClipMetadata(animation, freeze),
                    resolveClipMetadataSourceForAnimation(sceneClipMetadataSources, animation),
                    freeze
                )
            )
            .filter((clip): clip is GltfAnimationClipMetadata => Boolean(clip))
    );

    if (!parameters && !layers && rootMotion === undefined && clips.length === 0) {
        return undefined;
    }

    return maybeFreeze(
        {
            ...(parameters ? { parameters } : {}),
            ...(layers ? { layers } : {}),
            ...(rootMotion !== undefined ? { rootMotion } : {}),
            ...(clips.length > 0 ? { clips } : {}),
        } satisfies GltfAnimationControllerMetadata,
        freeze
    );
};
