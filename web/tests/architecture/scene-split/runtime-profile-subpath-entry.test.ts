import { describe, expect, it } from 'vitest';
import * as runtimeProfileCoreCapabilities from '@axrone/runtime-profile-core/capabilities';
import * as runtimeProfileCoreProfile from '@axrone/runtime-profile-core/profile';
import * as runtimeProfile2DCapabilities from '@axrone/runtime-profile-2d/capabilities';
import * as runtimeProfile2DProfile from '@axrone/runtime-profile-2d/profile';
import * as runtimeProfile3DCapabilities from '@axrone/runtime-profile-3d/capabilities';
import * as runtimeProfile3DProfile from '@axrone/runtime-profile-3d/profile';
import * as runtimeProfileFullCapabilities from '@axrone/runtime-profile-full/capabilities';
import * as runtimeProfileFullProfile from '@axrone/runtime-profile-full/profile';

describe('runtime profile subpath entries', () => {
    it('keeps capability exports separate from profile factories', () => {
        expect(runtimeProfileCoreCapabilities.RUNTIME_PROFILE_CORE_CAPABILITY_PACKAGES).toBeDefined();
        expect('getCoreSceneRuntimeProfile' in runtimeProfileCoreCapabilities).toBe(false);
        expect(runtimeProfileCoreProfile.getCoreSceneRuntimeProfile).toBeDefined();
        expect('RUNTIME_PROFILE_CORE_CAPABILITY_PACKAGES' in runtimeProfileCoreProfile).toBe(false);

        expect(runtimeProfile2DCapabilities.RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES).toBeDefined();
        expect('get2DSceneRuntimeProfile' in runtimeProfile2DCapabilities).toBe(false);
        expect(runtimeProfile2DProfile.get2DSceneRuntimeProfile).toBeDefined();
        expect('RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES' in runtimeProfile2DProfile).toBe(false);

        expect(runtimeProfile3DCapabilities.RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES).toBeDefined();
        expect('get3DSceneRuntimeProfile' in runtimeProfile3DCapabilities).toBe(false);
        expect(runtimeProfile3DProfile.get3DSceneRuntimeProfile).toBeDefined();
        expect('RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES' in runtimeProfile3DProfile).toBe(false);

        expect(runtimeProfileFullCapabilities.RUNTIME_PROFILE_FULL_CAPABILITY_PACKAGES).toBeDefined();
        expect('getDefaultSceneRuntimeProfile' in runtimeProfileFullCapabilities).toBe(false);
        expect(runtimeProfileFullProfile.getDefaultSceneRuntimeProfile).toBeDefined();
        expect('RUNTIME_PROFILE_FULL_CAPABILITY_PACKAGES' in runtimeProfileFullProfile).toBe(false);
    });
});