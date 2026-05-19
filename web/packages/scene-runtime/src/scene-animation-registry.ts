import type { ComponentRegistry } from '@axrone/ecs-runtime';
import { Animator } from './components/animator';
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

const ANIMATION_SCENE_BUILT_IN_REGISTRY_SOURCE: SceneBuiltInRegistrySource = Object.freeze({
    Animator,
});

export const SCENE_ANIMATION_BUILT_IN_MANIFEST = createSceneBuiltInManifest({
    id: 'scene/animation',
    builtIns: ['Animator'] as const,
});

export const ANIMATION_SCENE_BUILT_IN_MANIFESTS = Object.freeze([
    SCENE_ANIMATION_BUILT_IN_MANIFEST,
]) as readonly SceneBuiltInManifest[];

export const getAnimationSceneBuiltInRegistrySource = (): SceneBuiltInRegistrySource => ({
    ...ANIMATION_SCENE_BUILT_IN_REGISTRY_SOURCE,
});

export const createAnimationSceneRegistry = <R extends ComponentRegistry = Record<string, never>>(
    options: SceneRegistryBuilderOptions<R> = {}
): SceneRegistry<R> =>
    createSceneRegistryWithSource(ANIMATION_SCENE_BUILT_IN_REGISTRY_SOURCE, {
        registry: options.registry,
        builtIns: options.builtIns ?? SCENE_ANIMATION_BUILT_IN_MANIFEST.builtIns,
    }) as SceneRegistry<R>;

export const createAnimationSceneRegistryFromBuiltInManifests = <
    R extends ComponentRegistry = Record<string, never>,
>(
    options: SceneManifestRegistryBuilderOptions<R> = {}
): SceneRegistry<R> =>
    createSceneRegistryFromBuiltInManifestsWithSource(
        ANIMATION_SCENE_BUILT_IN_REGISTRY_SOURCE,
        {
            registry: options.registry,
            manifests: options.manifests ?? ANIMATION_SCENE_BUILT_IN_MANIFESTS,
        },
        SCENE_ANIMATION_BUILT_IN_MANIFEST.builtIns
    );