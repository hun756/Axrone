import type { ComponentRegistry } from '@axrone/ecs-runtime';
import { Hierarchy, Transform } from '@axrone/ecs-runtime';
import { PrefabNodeBinding } from './components/prefab-node-binding';
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

const CORE_SCENE_BUILT_IN_REGISTRY_SOURCE: SceneBuiltInRegistrySource = Object.freeze({
    Hierarchy,
    Transform,
    PrefabNodeBinding,
});

export const SCENE_CORE_BUILT_IN_MANIFEST = createSceneBuiltInManifest({
    id: 'scene/core',
    builtIns: ['Hierarchy', 'Transform', 'PrefabNodeBinding'] as const,
});

export const CORE_SCENE_BUILT_IN_MANIFESTS = Object.freeze([
    SCENE_CORE_BUILT_IN_MANIFEST,
]) as readonly SceneBuiltInManifest[];

export const getCoreSceneBuiltInRegistrySource = (): SceneBuiltInRegistrySource => ({
    ...CORE_SCENE_BUILT_IN_REGISTRY_SOURCE,
});

export const createCoreSceneRegistry = <R extends ComponentRegistry = Record<string, never>>(
    options: SceneRegistryBuilderOptions<R> = {}
): SceneRegistry<R> =>
    createSceneRegistryWithSource(CORE_SCENE_BUILT_IN_REGISTRY_SOURCE, {
        registry: options.registry,
        builtIns: options.builtIns ?? SCENE_CORE_BUILT_IN_MANIFEST.builtIns,
    });

export const createCoreSceneRegistryFromBuiltInManifests = <
    R extends ComponentRegistry = Record<string, never>,
>(
    options: SceneManifestRegistryBuilderOptions<R> = {}
): SceneRegistry<R> =>
    createSceneRegistryFromBuiltInManifestsWithSource(
        CORE_SCENE_BUILT_IN_REGISTRY_SOURCE,
        {
            registry: options.registry,
            manifests: options.manifests ?? CORE_SCENE_BUILT_IN_MANIFESTS,
        },
        SCENE_CORE_BUILT_IN_MANIFEST.builtIns
    );