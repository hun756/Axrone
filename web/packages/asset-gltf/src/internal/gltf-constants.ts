export const EMPTY_ARRAY = Object.freeze([]) as readonly never[];
export const DEFAULT_SAMPLER_ID = 'gltf/sampler/default';
export const DEFAULT_MATERIAL_KEY_SUFFIX = 'material/default';
export const DEFAULT_MATERIAL_NAME = 'Default Material';
export const DEFAULT_DOCUMENT_NAME = 'glTF Document';
export const MAX_SCENE_LOCAL_LIGHTS = 4;
export const RADIANS_TO_DEGREES = 180 / Math.PI;

export const VALID_ANIMATION_PARAMETER_KINDS = new Set(['float', 'int', 'bool', 'trigger']);
export const VALID_ANIMATION_LAYER_MODES = new Set(['override', 'additive']);
export const VALID_ANIMATION_IK_SOLVERS = new Set(['ccd', 'fabrik']);
export const VALID_ANIMATION_CONDITION_KINDS = new Set(['float', 'int', 'bool', 'trigger']);
export const VALID_ANIMATION_CONDITION_OPERATORS = new Set(['<', '<=', '>', '>=', '==', '!=']);

export const ANIMATION_MANIFEST_RESOURCE_NAMES = Object.freeze([
    'animation-manifest.json',
    'animations.manifest.json',
    'animation-controller.json',
    'animations.json',
]);

export const SUPPORTED_GLTF_EXTENSIONS = new Set<string>([
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

export interface PrefabBuildResult {
    readonly prefab: import('../asset-ir').GltfPrefabDefinition;
    readonly rootNodeIds: readonly string[];
    readonly nodeIds: readonly string[];
    readonly meshKeys: readonly string[];
    readonly skinKeys: readonly string[];
    readonly animationKeys: readonly string[];
    readonly materialKeys: readonly string[];
    readonly animationController?: import('../types').GltfAnimationControllerMetadata;
    readonly diagnostics: readonly import('../asset-contract').AssetImportDiagnostic[];
}

export interface GltfSkinBinding {
    readonly jointNodeIds: readonly string[];
    readonly skeletonNodeId?: string;
    readonly inverseBindMatrices?: readonly number[] | Float32Array;
}

export interface PortableAnimationManifestSceneEntry {
    readonly scene?: number;
    readonly sceneName?: string;
    readonly controller?: Record<string, unknown>;
    readonly clips?: readonly Record<string, unknown>[];
}

export interface PortableAnimationManifest {
    readonly controller?: Record<string, unknown>;
    readonly scenes?: readonly PortableAnimationManifestSceneEntry[];
    readonly clips?: readonly Record<string, unknown>[];
}

export interface PortableAnimationFeatureExportDefinition {
    readonly rootNodeId?: string;
    readonly rootNodeIndex?: number;
    readonly sampleInterval?: number;
    readonly sampleTimes?: readonly number[];
    readonly forwardAxis?: readonly [number, number, number];
    readonly tags?: readonly string[];
    readonly costBias?: number;
}

export interface GltfAnimationClipMetadataSource extends Omit<import('../types').GltfAnimationClipMetadata, 'id'> {
    readonly featureExport?: PortableAnimationFeatureExportDefinition;
}

export interface GltfAnimationClipMetadataSourceIndex {
    readonly byId: ReadonlyMap<string, GltfAnimationClipMetadataSource>;
    readonly byAnimationIndex: ReadonlyMap<number, GltfAnimationClipMetadataSource>;
}