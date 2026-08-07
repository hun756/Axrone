import { describe, expect, it } from 'vitest';
import { RUNTIME_PROFILE_FULL_CAPABILITY_PACKAGES } from '../capabilities';
import { RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES } from '@axrone/runtime-profile-2d/capabilities';
import { RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES } from '@axrone/runtime-profile-3d/capabilities';

describe('runtime-profile-full capabilities', () => {
    it('freezes the capability package list to prevent external mutation', () => {
        expect(Object.isFrozen(RUNTIME_PROFILE_FULL_CAPABILITY_PACKAGES)).toBe(true);
    });

    it('exposes the exact deduplicated union of 2d and 3d capabilities in insertion order', () => {
        expect([...RUNTIME_PROFILE_FULL_CAPABILITY_PACKAGES]).toEqual([
            '@axrone/scene-runtime',
            '@axrone/scene-2d',
            '@axrone/input',
            '@axrone/asset-2d',
            '@axrone/render-2d',
            '@axrone/physics-core',
            '@axrone/physics-2d',
            '@axrone/ui',
            '@axrone/scene-3d',
            '@axrone/asset-core',
            '@axrone/asset-gltf',
            '@axrone/asset-ui',
            '@axrone/render-3d',
            '@axrone/render-webgl2',
            '@axrone/physics-3d',
            '@axrone/ui-webgl2',
        ]);
    });

    it('contains no duplicate entries after Set deduplication', () => {
        const unique = new Set(RUNTIME_PROFILE_FULL_CAPABILITY_PACKAGES);
        expect(unique.size).toBe(RUNTIME_PROFILE_FULL_CAPABILITY_PACKAGES.length);
    });

    it('covers every entry from both the 2d and 3d capability lists', () => {
        for (const capability of RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES) {
            expect(RUNTIME_PROFILE_FULL_CAPABILITY_PACKAGES).toContain(capability);
        }
        for (const capability of RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES) {
            expect(RUNTIME_PROFILE_FULL_CAPABILITY_PACKAGES).toContain(capability);
        }
    });

    it('deduplicates shared packages so each appears exactly once', () => {
        const sharedPackages = ['@axrone/scene-runtime', '@axrone/input', '@axrone/physics-core', '@axrone/ui'];

        for (const sharedPackage of sharedPackages) {
            const occurrences = RUNTIME_PROFILE_FULL_CAPABILITY_PACKAGES.filter(
                (entry) => entry === sharedPackage,
            );
            expect(occurrences).toHaveLength(1);
        }
    });
});
