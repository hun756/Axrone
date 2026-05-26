import type {
    AnimationClipCompressionDefinition,
    AnimationClipEventDefinition,
    AnimationClipStreamingDefinition,
    AnimationFootContactDefinition,
    AnimationLayerDefinition,
    AnimationMotionFeatureDefinition,
    AnimationParameterDefinition,
    AnimationRootMotionDefinition,
} from '@axrone/animation/types';
import type { PortableAnimationFeatureExportDefinition } from './gltf-constants';

export interface AnimationControllerSource {
    readonly parameters?: readonly AnimationParameterDefinition[];
    readonly layers?: readonly AnimationLayerDefinition[];
    readonly rootMotion?: AnimationRootMotionDefinition | null;
}

export interface AnimationClipEntrySource extends AnimationControllerSource {
    readonly id?: string;
    readonly clipId?: string;
    readonly animationIndex?: number;
    readonly events?: readonly AnimationClipEventDefinition[];
    readonly footContacts?: readonly AnimationFootContactDefinition[];
    readonly tags?: readonly string[];
    readonly features?: readonly AnimationMotionFeatureDefinition[];
    readonly compression?: AnimationClipCompressionDefinition;
    readonly streaming?: AnimationClipStreamingDefinition;
    readonly featureExport?: PortableAnimationFeatureExportDefinition;
}

export interface AnimationManifestSceneEntry {
    readonly scene?: number;
    readonly sceneName?: string;
    readonly controller?: AnimationControllerSource;
    readonly clips?: readonly AnimationClipEntrySource[];
}

export interface AnimationManifest {
    readonly controller?: AnimationControllerSource;
    readonly scenes?: readonly AnimationManifestSceneEntry[];
    readonly clips?: readonly AnimationClipEntrySource[];
}

export type AnimationMetadataMergeStrategy = 'override' | 'merge' | 'fallback';

export interface AnimationMetadataMergeOptions {
    readonly parameterStrategy?: AnimationMetadataMergeStrategy;
    readonly layerStrategy?: AnimationMetadataMergeStrategy;
    readonly rootMotionStrategy?: AnimationMetadataMergeStrategy;
}

export const DEFAULT_MERGE_OPTIONS: Readonly<AnimationMetadataMergeOptions> = Object.freeze({
    parameterStrategy: 'override',
    layerStrategy: 'override',
    rootMotionStrategy: 'override',
});

export interface AnimationClipMetadata {
    readonly events?: readonly AnimationClipEventDefinition[];
    readonly footContacts?: readonly AnimationFootContactDefinition[];
    readonly tags?: readonly string[];
    readonly features?: readonly AnimationMotionFeatureDefinition[];
    readonly compression?: AnimationClipCompressionDefinition;
    readonly streaming?: AnimationClipStreamingDefinition;
    readonly featureExport?: PortableAnimationFeatureExportDefinition;
}

export interface AnimationClipMetadataIndex {
    readonly byId: ReadonlyMap<string, AnimationClipMetadata>;
    readonly byAnimationIndex: ReadonlyMap<number, AnimationClipMetadata>;
}

export interface AnimationSampleResult {
    readonly values: readonly number[];
    readonly frameIndex: number;
    readonly alpha: number;
}

export interface MotionFeatureExportContext {
    readonly clipId: string;
    readonly tracks: readonly import('../types').GltfAnimationTrackAsset[];
    readonly duration: number;
    readonly config: PortableAnimationFeatureExportDefinition;
}

export interface ResolvedExportTarget {
    readonly targetNodeId: string;
    readonly targetNodeIndex: number;
}

export const isAnimationControllerSource = (value: unknown): value is AnimationControllerSource =>
    typeof value === 'object' && value !== null && !Array.isArray(value);

export const isAnimationClipEntrySource = (value: unknown): value is AnimationClipEntrySource =>
    isAnimationControllerSource(value) && ('id' in value || 'clipId' in value || 'animationIndex' in value);
