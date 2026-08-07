import type { ComponentRegistry } from '@axrone/ecs-runtime';
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
    DEFAULT_SCENE_RUNTIME_PROFILE_ID,
    getDefaultSceneRuntimeProfile,
} from './scene-full-profile';
import {
    resolveSceneRegistryFromProfileWithFallback,
    type SceneRuntimeProfile,
    type SceneRuntimeProfileContext,
} from './scene-profile-contract';

export type { SceneManifestRuntimeProfileOptions } from './scene-manifest-profile';
export { createSceneManifestRuntimeProfile } from './scene-manifest-profile';
export { createSceneRuntimeProfile } from './scene-profile-contract';

export type { SceneRuntimeProfile, SceneRuntimeProfileContext };

export {
    CORE_SCENE_RUNTIME_PROFILE_ID,
    DEFAULT_SCENE_RUNTIME_PROFILE_ID,
    SCENE_2D_RUNTIME_PROFILE_ID,
    SCENE_3D_RUNTIME_PROFILE_ID,
    getCoreSceneRuntimeProfile,
    get2DSceneRuntimeProfile,
    get3DSceneRuntimeProfile,
    getDefaultSceneRuntimeProfile,
};

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