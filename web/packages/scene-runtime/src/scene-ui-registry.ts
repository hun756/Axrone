import type { ComponentRegistry } from '@axrone/ecs-runtime';
import { UIHost } from './components/ui-host';
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

const UI_SCENE_BUILT_IN_REGISTRY_SOURCE: SceneBuiltInRegistrySource = Object.freeze({
    UIHost,
});

export const SCENE_UI_BUILT_IN_MANIFEST = createSceneBuiltInManifest({
    id: 'scene/ui',
    builtIns: ['UIHost'] as const,
});

export const UI_SCENE_BUILT_IN_MANIFESTS = Object.freeze([
    SCENE_UI_BUILT_IN_MANIFEST,
]) as readonly SceneBuiltInManifest[];

export const getUISceneBuiltInRegistrySource = (): SceneBuiltInRegistrySource => ({
    ...UI_SCENE_BUILT_IN_REGISTRY_SOURCE,
});

export const createUISceneRegistry = <R extends ComponentRegistry = Record<string, never>>(
    options: SceneRegistryBuilderOptions<R> = {}
): SceneRegistry<R> =>
    createSceneRegistryWithSource(UI_SCENE_BUILT_IN_REGISTRY_SOURCE, {
        registry: options.registry,
        builtIns: options.builtIns ?? SCENE_UI_BUILT_IN_MANIFEST.builtIns,
    }) as SceneRegistry<R>;

export const createUISceneRegistryFromBuiltInManifests = <
    R extends ComponentRegistry = Record<string, never>,
>(
    options: SceneManifestRegistryBuilderOptions<R> = {}
): SceneRegistry<R> =>
    createSceneRegistryFromBuiltInManifestsWithSource(
        UI_SCENE_BUILT_IN_REGISTRY_SOURCE,
        {
            registry: options.registry,
            manifests: options.manifests ?? UI_SCENE_BUILT_IN_MANIFESTS,
        },
        SCENE_UI_BUILT_IN_MANIFEST.builtIns
    );
