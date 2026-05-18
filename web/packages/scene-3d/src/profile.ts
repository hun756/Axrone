import type { ComponentRegistry } from '@axrone/ecs-runtime';
import { getDefaultSceneRuntimeProfile } from '@axrone/scene-runtime/scene-full-profile';
import {
    resolveSceneRegistryFromProfileWithFallback,
    type SceneRuntimeProfile,
    type SceneRuntimeProfileContext,
} from '@axrone/scene-runtime/scene-profile-contract';

export type { SceneManifestRuntimeProfileOptions } from '@axrone/scene-runtime/scene-manifest-profile';
export type { SceneRuntimeProfile, SceneRuntimeProfileContext } from '@axrone/scene-runtime/scene-profile-contract';
export {
    DEFAULT_SCENE_RUNTIME_PROFILE_ID,
    getDefaultSceneRuntimeProfile,
} from '@axrone/scene-runtime/scene-full-profile';
export {
    SCENE_3D_RUNTIME_PROFILE_ID,
    get3DSceneRuntimeProfile,
} from '@axrone/scene-runtime/scene-3d-profile';
export { createSceneManifestRuntimeProfile } from '@axrone/scene-runtime/scene-manifest-profile';
export { createSceneRuntimeProfile } from '@axrone/scene-runtime/scene-profile-contract';

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