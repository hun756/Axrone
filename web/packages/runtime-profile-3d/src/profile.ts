import { get3DSceneRuntimeProfile } from '@axrone/scene-runtime/scene-profile';

export type { SceneRuntimeProfile } from '@axrone/scene-runtime/scene-profile';
export {
    SCENE_3D_RUNTIME_PROFILE_ID,
    get3DSceneRuntimeProfile,
} from '@axrone/scene-runtime/scene-profile';

export const scene3DRuntimeProfile = get3DSceneRuntimeProfile;