import type { ComponentRegistry } from '@axrone/ecs-runtime';
import { Camera } from './components/camera';
import { SpriteAnimator } from './components/sprite-animator';
import { SpriteMask } from './components/sprite-mask';
import { SpriteRenderer } from './components/sprite-renderer';
import {
    ANIMATION_SCENE_BUILT_IN_MANIFESTS,
    SCENE_ANIMATION_BUILT_IN_MANIFEST,
    getAnimationSceneBuiltInRegistrySource,
} from './scene-animation-registry';
import {
    CORE_SCENE_BUILT_IN_MANIFESTS,
    SCENE_CORE_BUILT_IN_MANIFEST,
    getCoreSceneBuiltInRegistrySource,
} from './scene-core-registry';
import {
    createSceneBuiltInManifest,
    createSceneRegistryFromBuiltInManifestsWithSource,
    createSceneRegistryWithSource,
    type SceneBuiltInManifest,
    type SceneBuiltInRegistrySource,
    type SceneManifestRegistryBuilderOptions,
    type SceneRegistryBuilderOptions,
} from './scene-built-in-support';
import type { SceneRegistry } from './types';

const SCENE_2D_ONLY_BUILT_IN_REGISTRY_SOURCE: SceneBuiltInRegistrySource = Object.freeze({
    Camera,
    SpriteRenderer,
    SpriteAnimator,
    SpriteMask,
});

const SCENE_2D_BUILT_IN_REGISTRY_SOURCE: SceneBuiltInRegistrySource = Object.freeze({
    ...getCoreSceneBuiltInRegistrySource(),
    ...getAnimationSceneBuiltInRegistrySource(),
    ...SCENE_2D_ONLY_BUILT_IN_REGISTRY_SOURCE,
});

export const SCENE_2D_BUILT_IN_MANIFEST = createSceneBuiltInManifest({
    id: 'scene/2d',
    builtIns: ['Camera', 'SpriteRenderer', 'SpriteAnimator', 'SpriteMask'] as const,
});

export const DEFAULT_SCENE_2D_BUILT_IN_MANIFESTS = Object.freeze([
    ...CORE_SCENE_BUILT_IN_MANIFESTS,
    ...ANIMATION_SCENE_BUILT_IN_MANIFESTS,
    SCENE_2D_BUILT_IN_MANIFEST,
]) as readonly SceneBuiltInManifest[];

export const get2DSceneBuiltInRegistrySource = (): SceneBuiltInRegistrySource => ({
    ...SCENE_2D_ONLY_BUILT_IN_REGISTRY_SOURCE,
});

export const create2DSceneRegistry = <R extends ComponentRegistry = Record<string, never>>(
    options: SceneRegistryBuilderOptions<R> = {}
): SceneRegistry<R> =>
    createSceneRegistryWithSource(SCENE_2D_BUILT_IN_REGISTRY_SOURCE, options) as SceneRegistry<R>;

export const create2DSceneRegistryFromBuiltInManifests = <
    R extends ComponentRegistry = Record<string, never>,
>(
    options: SceneManifestRegistryBuilderOptions<R> = {}
): SceneRegistry<R> =>
    createSceneRegistryFromBuiltInManifestsWithSource(
        SCENE_2D_BUILT_IN_REGISTRY_SOURCE,
        {
            registry: options.registry,
            manifests: options.manifests ?? DEFAULT_SCENE_2D_BUILT_IN_MANIFESTS,
        }
    );

export { SCENE_ANIMATION_BUILT_IN_MANIFEST, SCENE_CORE_BUILT_IN_MANIFEST };