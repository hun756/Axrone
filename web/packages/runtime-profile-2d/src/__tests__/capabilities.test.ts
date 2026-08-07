import { describe, expect, it } from 'vitest';
import { RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES } from '../capabilities';
import { INPUT_CAPABILITY_PACKAGE } from '@axrone/input';
import { ASSET_2D_CAPABILITY_PACKAGE } from '@axrone/asset-2d';
import { RENDER_2D_CAPABILITY_PACKAGE } from '@axrone/render-2d';

describe('runtime-profile-2d capabilities', () => {
    it('freezes the capability package list to prevent external mutation', () => {
        expect(Object.isFrozen(RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES)).toBe(true);
    });

    it('exposes the exact 2d capability seam in declaration order', () => {
        expect([...RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES]).toEqual([
            '@axrone/scene-runtime',
            '@axrone/scene-2d',
            '@axrone/input',
            '@axrone/asset-2d',
            '@axrone/render-2d',
            '@axrone/physics-core',
            '@axrone/physics-2d',
            '@axrone/ui',
        ]);
    });

    it('contains no duplicate entries', () => {
        const unique = new Set(RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES);
        expect(unique.size).toBe(RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES.length);
    });

    it('excludes 3d-only capability packages', () => {
        expect(RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES).not.toContain('@axrone/asset-gltf');
        expect(RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES).not.toContain('@axrone/render-3d');
        expect(RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES).not.toContain('@axrone/render-webgl2');
        expect(RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES).not.toContain('@axrone/scene-3d');
    });

    it('resolves imported capability constants to their expected strings', () => {
        expect(INPUT_CAPABILITY_PACKAGE).toBe('@axrone/input');
        expect(ASSET_2D_CAPABILITY_PACKAGE).toBe('@axrone/asset-2d');
        expect(RENDER_2D_CAPABILITY_PACKAGE).toBe('@axrone/render-2d');

        expect(RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES).toContain(INPUT_CAPABILITY_PACKAGE);
        expect(RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES).toContain(ASSET_2D_CAPABILITY_PACKAGE);
        expect(RUNTIME_PROFILE_2D_CAPABILITY_PACKAGES).toContain(RENDER_2D_CAPABILITY_PACKAGE);
    });
});
