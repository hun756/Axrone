import type { ComponentRegistry } from '@axrone/ecs-runtime';
import { Camera } from './components/camera';
import { DirectionalLight } from './components/directional-light';
import { FollowCameraController } from './components/follow-camera-controller';
import { LineRenderer } from './components/line-renderer';
import { MeshRenderer } from './components/mesh-renderer';
import { OrbitCameraController } from './components/orbit-camera-controller';
import { PointLight } from './components/point-light';
import { SpotLight } from './components/spot-light';
import { Terrain } from './components/terrain';
import { TrailRenderer } from './components/trail-renderer';
import {
    CORE_SCENE_BUILT_IN_MANIFESTS,
    SCENE_CORE_BUILT_IN_MANIFEST,
    getCoreSceneBuiltInRegistrySource,
} from './scene-core-registry';
import {
    SCENE_UI_BUILT_IN_MANIFEST,
    UI_SCENE_BUILT_IN_MANIFESTS,
    getUISceneBuiltInRegistrySource,
} from './scene-ui-registry';
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

const SCENE_3D_ONLY_BUILT_IN_REGISTRY_SOURCE: SceneBuiltInRegistrySource = Object.freeze({
    Camera,
    MeshRenderer,
    DirectionalLight,
    PointLight,
    SpotLight,
    Terrain,
    OrbitCameraController,
    FollowCameraController,
    LineRenderer,
    TrailRenderer,
});

const SCENE_3D_BUILT_IN_REGISTRY_SOURCE: SceneBuiltInRegistrySource = Object.freeze({
    ...getCoreSceneBuiltInRegistrySource(),
    ...getUISceneBuiltInRegistrySource(),
    ...SCENE_3D_ONLY_BUILT_IN_REGISTRY_SOURCE,
});

export const SCENE_3D_BUILT_IN_MANIFEST = createSceneBuiltInManifest({
    id: 'scene/3d',
    builtIns: [
        'Camera',
        'MeshRenderer',
        'DirectionalLight',
        'PointLight',
        'SpotLight',
        'Terrain',
        'OrbitCameraController',
        'FollowCameraController',
        'LineRenderer',
        'TrailRenderer',
    ] as const,
});

export const DEFAULT_SCENE_3D_BUILT_IN_MANIFESTS = Object.freeze([
    ...CORE_SCENE_BUILT_IN_MANIFESTS,
    ...UI_SCENE_BUILT_IN_MANIFESTS,
    SCENE_3D_BUILT_IN_MANIFEST,
]) as readonly SceneBuiltInManifest[];

export const get3DSceneBuiltInRegistrySource = (): SceneBuiltInRegistrySource => ({
    ...SCENE_3D_ONLY_BUILT_IN_REGISTRY_SOURCE,
});

export const create3DSceneRegistry = <R extends ComponentRegistry = Record<string, never>>(
    options: SceneRegistryBuilderOptions<R> = {}
): SceneRegistry<R> =>
    createSceneRegistryWithSource(SCENE_3D_BUILT_IN_REGISTRY_SOURCE, options) as SceneRegistry<R>;

export const create3DSceneRegistryFromBuiltInManifests = <
    R extends ComponentRegistry = Record<string, never>,
>(
    options: SceneManifestRegistryBuilderOptions<R> = {}
): SceneRegistry<R> =>
    createSceneRegistryFromBuiltInManifestsWithSource(
        SCENE_3D_BUILT_IN_REGISTRY_SOURCE,
        {
            registry: options.registry,
            manifests: options.manifests ?? DEFAULT_SCENE_3D_BUILT_IN_MANIFESTS,
        }
    );

export { SCENE_CORE_BUILT_IN_MANIFEST, SCENE_UI_BUILT_IN_MANIFEST };