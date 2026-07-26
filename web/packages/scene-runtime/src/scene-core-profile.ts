import type { ComponentRegistry } from '@axrone/ecs-runtime';
import { createCoreSceneRegistry } from './scene-core-registry';
import {
    createSceneRuntimeProfile,
    type SceneRuntimeProfile,
} from './scene-profile-contract';

export type { SceneRuntimeProfile, SceneRuntimeProfileContext } from './scene-profile-contract';

export const CORE_SCENE_RUNTIME_PROFILE_ID = 'scene/core-default';

const CORE_SCENE_RUNTIME_PROFILE: SceneRuntimeProfile<any> = Object.freeze(
    createSceneRuntimeProfile({
        id: CORE_SCENE_RUNTIME_PROFILE_ID,
        resolveRegistry: ({ registry }) => createCoreSceneRegistry({ registry }),
    })
);

export const getCoreSceneRuntimeProfile = <
    R extends ComponentRegistry = Record<string, never>,
>(): SceneRuntimeProfile<R> => CORE_SCENE_RUNTIME_PROFILE as SceneRuntimeProfile<R>;