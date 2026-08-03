import { describe, expect, it } from 'vitest';
import {
    SCENE_3D_RUNTIME_PROFILE_ID,
    get3DSceneRuntimeProfile,
    scene3DRuntimeProfile,
} from '../profile';
import * as barrelExports from '../index';

describe('runtime-profile-3d profile', () => {
    it('pins the profile ID to the stable scene/3d-default identifier', () => {
        expect(SCENE_3D_RUNTIME_PROFILE_ID).toBe('scene/3d-default');
    });

    it('aliases scene3DRuntimeProfile to the get3DSceneRuntimeProfile factory', () => {
        expect(scene3DRuntimeProfile).toBe(get3DSceneRuntimeProfile);
    });

    it('returns a profile object with the correct id and resolveRegistry function', () => {
        const profile = get3DSceneRuntimeProfile();

        expect(typeof profile.id).toBe('string');
        expect(profile.id).toBe('scene/3d-default');
        expect(typeof profile.resolveRegistry).toBe('function');
    });

    it('returns the same frozen profile instance on repeated calls', () => {
        const first = get3DSceneRuntimeProfile();
        const second = get3DSceneRuntimeProfile();

        expect(first).toBe(second);
        expect(Object.isFrozen(first)).toBe(true);
    });

    it('resolves a 3d registry with Camera, MeshRenderer, and Terrain but no Animator', () => {
        const registry = get3DSceneRuntimeProfile().resolveRegistry({});

        expect(registry.Camera).toBeDefined();
        expect(registry.MeshRenderer).toBeDefined();
        expect(registry.Terrain).toBeDefined();
        expect('Animator' in registry).toBe(false);
    });

    it('barrel index re-exports all capability and profile symbols', () => {
        expect(barrelExports.RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES).toBeDefined();
        expect(barrelExports.get3DSceneRuntimeProfile).toBeDefined();
        expect(barrelExports.scene3DRuntimeProfile).toBeDefined();
        expect(barrelExports.SCENE_3D_RUNTIME_PROFILE_ID).toBeDefined();
    });
});
