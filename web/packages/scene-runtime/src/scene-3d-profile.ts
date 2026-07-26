import type { ComponentRegistry } from '@axrone/ecs-runtime';
import { create3DSceneRegistry } from './scene-3d-registry';
import {
    createSceneRuntimeProfile,
    type SceneRuntimeProfile,
} from './scene-profile-contract';

export type { SceneRuntimeProfile, SceneRuntimeProfileContext } from './scene-profile-contract';

export const SCENE_3D_RUNTIME_PROFILE_ID = 'scene/3d-default';

const SCENE_3D_RUNTIME_PROFILE: SceneRuntimeProfile<any> = Object.freeze(
    createSceneRuntimeProfile({
        id: SCENE_3D_RUNTIME_PROFILE_ID,
        resolveRegistry: ({ registry }) => create3DSceneRegistry({ registry }),
    })
);

export const get3DSceneRuntimeProfile = <
    R extends ComponentRegistry = Record<string, never>,
>(): SceneRuntimeProfile<R> => SCENE_3D_RUNTIME_PROFILE as SceneRuntimeProfile<R>;