import { describe, expect, it } from 'vitest';
import {
    CORE_SCENE_RUNTIME_PROFILE_ID,
    getCoreSceneRuntimeProfile,
    coreSceneRuntimeProfile,
} from '../profile';
import * as barrelExports from '../index';

describe('runtime-profile-core profile', () => {
    it('pins the profile ID to the stable scene/core-default identifier', () => {
        expect(CORE_SCENE_RUNTIME_PROFILE_ID).toBe('scene/core-default');
    });

    it('aliases coreSceneRuntimeProfile to the getCoreSceneRuntimeProfile factory', () => {
        expect(coreSceneRuntimeProfile).toBe(getCoreSceneRuntimeProfile);
    });

    it('returns a profile object with the correct id and resolveRegistry function', () => {
        const profile = getCoreSceneRuntimeProfile();

        expect(typeof profile.id).toBe('string');
        expect(profile.id).toBe('scene/core-default');
        expect(typeof profile.resolveRegistry).toBe('function');
    });

    it('returns the same frozen profile instance on repeated calls', () => {
        const first = getCoreSceneRuntimeProfile();
        const second = getCoreSceneRuntimeProfile();

        expect(first).toBe(second);
        expect(Object.isFrozen(first)).toBe(true);
    });

    it('resolves a core registry with Hierarchy and Transform but no Camera or MeshRenderer', () => {
        const registry = getCoreSceneRuntimeProfile().resolveRegistry({});

        expect(registry.Hierarchy).toBeDefined();
        expect(registry.Transform).toBeDefined();
        expect('Camera' in registry).toBe(false);
        expect('MeshRenderer' in registry).toBe(false);
    });

    it('barrel index re-exports all capability and profile symbols', () => {
        expect(barrelExports.RUNTIME_PROFILE_CORE_CAPABILITY_PACKAGES).toBeDefined();
        expect(barrelExports.getCoreSceneRuntimeProfile).toBeDefined();
        expect(barrelExports.coreSceneRuntimeProfile).toBeDefined();
        expect(barrelExports.CORE_SCENE_RUNTIME_PROFILE_ID).toBeDefined();
    });
});
