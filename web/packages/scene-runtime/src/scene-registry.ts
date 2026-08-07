import type { ComponentRegistry } from '@axrone/ecs-runtime';
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
    DEFAULT_SCENE_2D_BUILT_IN_MANIFESTS,
    SCENE_2D_BUILT_IN_MANIFEST,
    get2DSceneBuiltInRegistrySource,
} from './scene-2d-registry';
import {
    DEFAULT_SCENE_3D_BUILT_IN_MANIFESTS,
    SCENE_3D_BUILT_IN_MANIFEST,
    get3DSceneBuiltInRegistrySource,
} from './scene-3d-registry';
import {
    SCENE_UI_BUILT_IN_MANIFEST,
    UI_SCENE_BUILT_IN_MANIFESTS,
    getUISceneBuiltInRegistrySource,
} from './scene-ui-registry';
import {
    createSceneBuiltInManifest,
    createSceneRegistryFromBuiltInManifestsWithSource,
    createSceneRegistryWithSource,
    resolveSceneBuiltInComponents,
    type SceneBuiltInComponentName,
    type SceneBuiltInManifest,
    type SceneManifestRegistryBuilderOptions,
    type SceneRegistryBuilderOptions,
    type SceneRegistryForBuiltIns,
} from './scene-built-in-support';
import type { SceneBuiltInRegistry, SceneRegistry } from './types';

const DEFAULT_SCENE_BUILT_IN_REGISTRY: SceneBuiltInRegistry = Object.freeze({
    ...getCoreSceneBuiltInRegistrySource(),
    ...getAnimationSceneBuiltInRegistrySource(),
    ...getUISceneBuiltInRegistrySource(),
    ...get2DSceneBuiltInRegistrySource(),
    ...get3DSceneBuiltInRegistrySource(),
}) as SceneBuiltInRegistry;

export type {
    SceneBuiltInComponentName,
    SceneBuiltInManifest,
    SceneManifestRegistryBuilderOptions,
    SceneRegistryBuilderOptions,
    SceneRegistryForBuiltIns,
};
export { createSceneBuiltInManifest, resolveSceneBuiltInComponents };

export {
    SCENE_ANIMATION_BUILT_IN_MANIFEST,
    SCENE_CORE_BUILT_IN_MANIFEST,
    SCENE_UI_BUILT_IN_MANIFEST,
    SCENE_2D_BUILT_IN_MANIFEST,
    SCENE_3D_BUILT_IN_MANIFEST,
};

export const DEFAULT_SCENE_BUILT_IN_MANIFESTS = Object.freeze([
    ...CORE_SCENE_BUILT_IN_MANIFESTS,
    ...ANIMATION_SCENE_BUILT_IN_MANIFESTS,
    ...UI_SCENE_BUILT_IN_MANIFESTS,
    SCENE_3D_BUILT_IN_MANIFEST,
]) as readonly SceneBuiltInManifest[];

export const DEFAULT_SCENE_2D_BUILT_IN_COMPONENTS = resolveSceneBuiltInComponents(
    DEFAULT_SCENE_2D_BUILT_IN_MANIFESTS
);
export const DEFAULT_SCENE_3D_BUILT_IN_COMPONENTS = resolveSceneBuiltInComponents(
    DEFAULT_SCENE_3D_BUILT_IN_MANIFESTS
);
export const DEFAULT_SCENE_BUILT_IN_COMPONENTS = resolveSceneBuiltInComponents(
    DEFAULT_SCENE_BUILT_IN_MANIFESTS
);

export const getDefaultSceneBuiltInRegistry = (): SceneBuiltInRegistry => ({
    ...DEFAULT_SCENE_BUILT_IN_REGISTRY,
});

export const createSceneRegistryFromBuiltInManifests = <
    R extends ComponentRegistry = Record<string, never>,
>(
    options: SceneManifestRegistryBuilderOptions<R> = {}
): SceneRegistry<R> =>
    createSceneRegistryFromBuiltInManifestsWithSource(
        DEFAULT_SCENE_BUILT_IN_REGISTRY,
        {
            registry: options.registry,
            manifests: options.manifests ?? DEFAULT_SCENE_BUILT_IN_MANIFESTS,
        },
        DEFAULT_SCENE_BUILT_IN_COMPONENTS
    );

export function createSceneRegistry<R extends ComponentRegistry = Record<string, never>>(
    options?: SceneRegistryBuilderOptions<R>
): SceneRegistry<R>;
export function createSceneRegistry<
    R extends ComponentRegistry,
    const TBuiltIns extends readonly SceneBuiltInComponentName[],
>(options: SceneRegistryBuilderOptions<R, TBuiltIns>): SceneRegistryForBuiltIns<R, TBuiltIns>;
export function createSceneRegistry<
    R extends ComponentRegistry = Record<string, never>,
    TBuiltIns extends readonly SceneBuiltInComponentName[] | undefined = undefined,
>(options: SceneRegistryBuilderOptions<R, TBuiltIns> = {}): SceneRegistry<R> {
    return createSceneRegistryWithSource(DEFAULT_SCENE_BUILT_IN_REGISTRY, {
        registry: options.registry,
        builtIns: options.builtIns ?? DEFAULT_SCENE_BUILT_IN_COMPONENTS,
    }) as SceneRegistry<R>;
}