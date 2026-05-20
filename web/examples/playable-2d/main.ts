import {
    SCENE_2D_RUNTIME_PROFILE_ID,
    scene2DRuntimeProfile,
} from '@axrone/runtime-profile-2d/profile';

export interface Playable2DReferenceDescriptor {
    readonly profileId: string;
    readonly registryFactory: string;
}

export const PLAYABLE_2D_REFERENCE: Playable2DReferenceDescriptor = Object.freeze({
    profileId: scene2DRuntimeProfile().id,
    registryFactory: SCENE_2D_RUNTIME_PROFILE_ID,
});

Reflect.set(globalThis, '__AXRONE_PLAYABLE_2D_REFERENCE__', PLAYABLE_2D_REFERENCE);