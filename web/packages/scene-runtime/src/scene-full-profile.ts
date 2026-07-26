import type { ComponentRegistry } from '@axrone/ecs-runtime';
import { DEFAULT_SCENE_BUILT_IN_MANIFESTS } from './scene-registry';
import {
    createSceneManifestRuntimeProfile,
    type SceneManifestRuntimeProfileOptions,
} from './scene-manifest-profile';
import {
    createSceneRuntimeProfile,
    resolveSceneRegistryFromProfileWithFallback,
    type SceneRuntimeProfile,
    type SceneRuntimeProfileContext,
} from './scene-profile-contract';

export type {
    SceneManifestRuntimeProfileOptions,
    SceneRuntimeProfile,
    SceneRuntimeProfileContext,
};
export {
    createSceneManifestRuntimeProfile,
    createSceneRuntimeProfile,
};

export const DEFAULT_SCENE_RUNTIME_PROFILE_ID = 'scene/full-3d-default';

const DEFAULT_SCENE_RUNTIME_PROFILE: SceneRuntimeProfile<any> = Object.freeze(
    createSceneManifestRuntimeProfile({
        id: DEFAULT_SCENE_RUNTIME_PROFILE_ID,
        manifests: DEFAULT_SCENE_BUILT_IN_MANIFESTS,
    })
);

export const getDefaultSceneRuntimeProfile = <
    R extends ComponentRegistry = Record<string, never>,
>(): SceneRuntimeProfile<R> => DEFAULT_SCENE_RUNTIME_PROFILE as SceneRuntimeProfile<R>;

export const resolveSceneRegistryFromProfile = <
    R extends ComponentRegistry = Record<string, never>,
>(
    profile: SceneRuntimeProfile<R> | undefined,
    context: SceneRuntimeProfileContext<R> = {}
): ReturnType<SceneRuntimeProfile<R>['resolveRegistry']> =>
    resolveSceneRegistryFromProfileWithFallback(
        profile,
        getDefaultSceneRuntimeProfile<R>(),
        context
    );