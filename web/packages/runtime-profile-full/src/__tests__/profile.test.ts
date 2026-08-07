import { describe, expect, it } from 'vitest';
import {
    DEFAULT_SCENE_RUNTIME_PROFILE_ID,
    getDefaultSceneRuntimeProfile,
    fullSceneRuntimeProfile,
} from '../profile';
import * as barrelExports from '../index';

describe('runtime-profile-full profile', () => {
    it('pins the profile ID to the stable scene/full-3d-default identifier', () => {
        expect(DEFAULT_SCENE_RUNTIME_PROFILE_ID).toBe('scene/full-3d-default');
    });

    it('aliases fullSceneRuntimeProfile to the getDefaultSceneRuntimeProfile factory', () => {
        expect(fullSceneRuntimeProfile).toBe(getDefaultSceneRuntimeProfile);
    });

    it('returns a profile object with the correct id and resolveRegistry function', () => {
        const profile = getDefaultSceneRuntimeProfile();

        expect(typeof profile.id).toBe('string');
        expect(profile.id).toBe('scene/full-3d-default');
        expect(typeof profile.resolveRegistry).toBe('function');
    });

    it('returns the same frozen profile instance on repeated calls', () => {
        const first = getDefaultSceneRuntimeProfile();
        const second = getDefaultSceneRuntimeProfile();

        expect(first).toBe(second);
        expect(Object.isFrozen(first)).toBe(true);
    });

    it('resolves a full registry with Camera, MeshRenderer, Terrain, Animator, and DirectionalLight', () => {
        const registry = getDefaultSceneRuntimeProfile().resolveRegistry({});

        expect(registry.Camera).toBeDefined();
        expect(registry.MeshRenderer).toBeDefined();
        expect(registry.Terrain).toBeDefined();
        expect(registry.Animator).toBeDefined();
        expect(registry.DirectionalLight).toBeDefined();
    });

    it('barrel index re-exports all capability and profile symbols', () => {
        expect(barrelExports.RUNTIME_PROFILE_FULL_CAPABILITY_PACKAGES).toBeDefined();
        expect(barrelExports.getDefaultSceneRuntimeProfile).toBeDefined();
        expect(barrelExports.fullSceneRuntimeProfile).toBeDefined();
        expect(barrelExports.DEFAULT_SCENE_RUNTIME_PROFILE_ID).toBeDefined();
    });
});
