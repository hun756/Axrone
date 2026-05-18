import type { ComponentRegistry } from '@axrone/ecs-runtime';
import { get2DSceneRuntimeProfile } from '@axrone/scene-runtime/scene-2d-profile';
import {
    resolveSceneRegistryFromProfileWithFallback,
    type SceneRuntimeProfile,
    type SceneRuntimeProfileContext,
} from '@axrone/scene-runtime/scene-profile-contract';

export type { SceneManifestRuntimeProfileOptions } from '@axrone/scene-runtime/scene-manifest-profile';
export type { SceneRuntimeProfile, SceneRuntimeProfileContext } from '@axrone/scene-runtime/scene-profile-contract';
export {
    CORE_SCENE_RUNTIME_PROFILE_ID,
    getCoreSceneRuntimeProfile,
} from '@axrone/scene-runtime/scene-core-profile';
export {
    SCENE_2D_RUNTIME_PROFILE_ID,
    get2DSceneRuntimeProfile,
} from '@axrone/scene-runtime/scene-2d-profile';
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
        get2DSceneRuntimeProfile<R>(),
        context
    );