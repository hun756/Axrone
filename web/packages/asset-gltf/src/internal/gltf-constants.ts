export const EMPTY_ARRAY: readonly never[] = Object.freeze([]) as readonly never[];
export const DEFAULT_SAMPLER_ID = 'gltf/sampler/default';
export const DEFAULT_MATERIAL_KEY_SUFFIX = 'material/default';
export const DEFAULT_MATERIAL_NAME = 'Default Material';
export const DEFAULT_DOCUMENT_NAME = 'glTF Document';
export const MAX_SCENE_LOCAL_LIGHTS = 4;
export const RADIANS_TO_DEGREES = 180 / Math.PI;

export const VALID_ANIMATION_PARAMETER_KINDS = new Set(['float', 'int', 'bool', 'trigger'] as const);
export const VALID_ANIMATION_LAYER_MODES = new Set(['override', 'additive'] as const);
export const VALID_ANIMATION_IK_SOLVERS = new Set(['ccd', 'fabrik'] as const);
export const VALID_ANIMATION_CONDITION_KINDS = new Set(['float', 'int', 'bool', 'trigger'] as const);
export const VALID_ANIMATION_CONDITION_OPERATORS = new Set(['<', '<=', '>', '>=', '==', '!='] as const);

export const ANIMATION_MANIFEST_RESOURCE_NAMES: readonly string[] = Object.freeze([
    'animation-manifest.json',
    'animations.manifest.json',
    'animation-controller.json',
    'animations.json',
]);

export type SupportedGltfExtension =
    | 'EXT_meshopt_compression'
    | 'KHR_draco_mesh_compression'
    | 'KHR_lights_punctual'
    | 'KHR_materials_clearcoat'
    | 'KHR_materials_emissive_strength'
    | 'KHR_materials_unlit'
    | 'KHR_mesh_quantization'
    | 'KHR_texture_basisu'
    | 'KHR_texture_transform';

export const SUPPORTED_GLTF_EXTENSIONS: ReadonlySet<SupportedGltfExtension> = new Set<SupportedGltfExtension>([
    'EXT_meshopt_compression',
    'KHR_draco_mesh_compression',
    'KHR_lights_punctual',
    'KHR_materials_clearcoat',
    'KHR_materials_emissive_strength',
    'KHR_materials_unlit',
    'KHR_mesh_quantization',
    'KHR_texture_basisu',
    'KHR_texture_transform',
]);

export type AnimationParameterKind = 'float' | 'int' | 'bool' | 'trigger';
export type AnimationLayerMode = 'override' | 'additive';
export type AnimationIkSolver = 'ccd' | 'fabrik';
export type AnimationConditionOperator = '<' | '<=' | '>' | '>=' | '==' | '!=';

export const isSupportedExtension = (extension: string): extension is SupportedGltfExtension =>
    SUPPORTED_GLTF_EXTENSIONS.has(extension as SupportedGltfExtension);

export const isAnimationParameterKind = (kind: string): kind is AnimationParameterKind =>
    VALID_ANIMATION_PARAMETER_KINDS.has(kind as AnimationParameterKind);

export const isAnimationLayerMode = (mode: string): mode is AnimationLayerMode =>
    VALID_ANIMATION_LAYER_MODES.has(mode as AnimationLayerMode);

export const isAnimationIkSolver = (solver: string): solver is AnimationIkSolver =>
    VALID_ANIMATION_IK_SOLVERS.has(solver as AnimationIkSolver);

export const isAnimationConditionOperator = (op: string): op is AnimationConditionOperator =>
    VALID_ANIMATION_CONDITION_OPERATORS.has(op as AnimationConditionOperator);
