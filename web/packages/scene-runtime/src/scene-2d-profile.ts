import type { ComponentRegistry } from '@axrone/ecs-runtime';
import { create2DSceneRegistry } from './scene-2d-registry';
import {
    createSceneRuntimeProfile,
    type SceneRuntimeProfile,
} from './scene-profile-contract';

export type { SceneRuntimeProfile, SceneRuntimeProfileContext } from './scene-profile-contract';

export const SCENE_2D_RUNTIME_PROFILE_ID = 'scene/2d-default';

const SCENE_2D_RUNTIME_PROFILE: SceneRuntimeProfile<any> = Object.freeze(
    createSceneRuntimeProfile({
        id: SCENE_2D_RUNTIME_PROFILE_ID,
        resolveRegistry: ({ registry }) => create2DSceneRegistry({ registry }),
    })
);

export const get2DSceneRuntimeProfile = <
    R extends ComponentRegistry = Record<string, never>,
>(): SceneRuntimeProfile<R> => SCENE_2D_RUNTIME_PROFILE as SceneRuntimeProfile<R>;