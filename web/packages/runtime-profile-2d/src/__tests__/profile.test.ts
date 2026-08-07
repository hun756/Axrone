import { describe, expect, it } from 'vitest';
import {
    SCENE_2D_RUNTIME_PROFILE_ID,
    get2DSceneRuntimeProfile,
    scene2DRuntimeProfile,
} from '../profile';
import * as barrelExports from '../index';

describe('runtime-profile-2d profile', () => {
    it('pins the profile ID to the stable scene/2d-default identifier', () => {
        expect(SCENE_2D_RUNTIME_PROFILE_ID).toBe('scene/2d-default');
    });

    it('aliases scene2DRuntimeProfile to the get2DSceneRuntimeProfile factory', () => {
        expect(scene2DRuntimeProfile).toBe(get2DSceneRuntimeProfile);
    });

    it('returns a profile object with the correct id and resolveRegistry function', () => {
        const profile = get2DSceneRuntimeProfile();

        expect(typeof profile.id).toBe('string');
        expect(profile.id).toBe('scene/2d-default');
        expect(typeof profile.resolveRegistry).toBe('function');
    });

    it('returns the same frozen profile instance on repeated calls', () => {
        const first = get2DSceneRuntimeProfile();
        const second = get2DSceneRuntimeProfile();

        expect(first).toBe(second);
        expect(Object.isFrozen(first)).toBe(true);
    });

    it('resolves a 2d registry with Camera and Animator but no MeshRenderer, Terrain, or DirectionalLight', () => {
        const registry = get2DSceneRuntimeProfile().resolveRegistry({});

        expect(registry.Camera).toBeDefined();
        expect(registry.Animator).toBeDefined();
        expect('MeshRenderer' in registry).toBe(false);
        expect('Terrain' in registry).toBe(false);
        expect('DirectionalLight' in registry).toBe(false);
    });

    it('barrel index re-exports all capability and profile symbols', () => {
        expect(barrelExports.RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES).toBeDefined();
        expect(barrelExports.get2DSceneRuntimeProfile).toBeDefined();
        expect(barrelExports.scene2DRuntimeProfile).toBeDefined();
        expect(barrelExports.SCENE_2D_RUNTIME_PROFILE_ID).toBeDefined();
    });
});
