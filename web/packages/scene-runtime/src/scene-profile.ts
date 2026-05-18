import type { ComponentRegistry } from '@axrone/ecs-runtime';
import {
    DEFAULT_SCENE_BUILT_IN_MANIFESTS,
    type SceneBuiltInManifest,
    createSceneRegistryFromBuiltInManifests,
} from './scene-registry';
import {
    CORE_SCENE_RUNTIME_PROFILE_ID,
    getCoreSceneRuntimeProfile,
} from './scene-core-profile';
import {
    SCENE_2D_RUNTIME_PROFILE_ID,
    get2DSceneRuntimeProfile,
} from './scene-2d-profile';
import {
    SCENE_3D_RUNTIME_PROFILE_ID,
    get3DSceneRuntimeProfile,
} from './scene-3d-profile';
import {
    createSceneRuntimeProfile,
    resolveSceneRegistryFromProfile as resolveSceneRegistryFromProfileWithFallback,
    type SceneRuntimeProfile,
    type SceneRuntimeProfileContext,
} from './scene-profile-contract';

export interface SceneManifestRuntimeProfileOptions<
    R extends ComponentRegistry = Record<string, never>,
> {
    readonly id: string;
    readonly manifests: readonly SceneBuiltInManifest[];
}

export type { SceneRuntimeProfile, SceneRuntimeProfileContext };
export { createSceneRuntimeProfile };

export const createSceneManifestRuntimeProfile = <
    R extends ComponentRegistry = Record<string, never>,
>(
    options: SceneManifestRuntimeProfileOptions<R>
): SceneRuntimeProfile<R> =>
    createSceneRuntimeProfile({
        id: options.id,
        resolveRegistry: ({ registry }) =>
            createSceneRegistryFromBuiltInManifests({
                registry,
                manifests: options.manifests,
            }),
    });

export const DEFAULT_SCENE_RUNTIME_PROFILE_ID = 'scene/full-3d-default';

const DEFAULT_SCENE_RUNTIME_PROFILE: SceneRuntimeProfile<any> = Object.freeze(
    createSceneManifestRuntimeProfile({
        id: DEFAULT_SCENE_RUNTIME_PROFILE_ID,
        manifests: DEFAULT_SCENE_BUILT_IN_MANIFESTS,
    })
);

export {
    CORE_SCENE_RUNTIME_PROFILE_ID,
    SCENE_2D_RUNTIME_PROFILE_ID,
    SCENE_3D_RUNTIME_PROFILE_ID,
    getCoreSceneRuntimeProfile,
    get2DSceneRuntimeProfile,
    get3DSceneRuntimeProfile,
};

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