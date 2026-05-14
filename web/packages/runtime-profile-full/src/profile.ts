import { getDefaultSceneRuntimeProfile } from '@axrone/scene-runtime/scene-profile';

export type { SceneRuntimeProfile } from '@axrone/scene-runtime/scene-profile';
export {
    DEFAULT_SCENE_RUNTIME_PROFILE_ID,
    getDefaultSceneRuntimeProfile,
} from '@axrone/scene-runtime/scene-profile';

export const fullSceneRuntimeProfile = getDefaultSceneRuntimeProfile;