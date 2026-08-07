import { describe, expect, it } from 'vitest';
import { RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES } from '../capabilities';
import { INPUT_CAPABILITY_PACKAGE } from '@axrone/input';
import { RENDER_3D_CAPABILITY_PACKAGE } from '@axrone/render-3d';

describe('runtime-profile-3d capabilities', () => {
    it('freezes the capability package list to prevent external mutation', () => {
        expect(Object.isFrozen(RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES)).toBe(true);
    });

    it('exposes the exact 3d capability seam in declaration order', () => {
        expect([...RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES]).toEqual([
            '@axrone/scene-runtime',
            '@axrone/scene-3d',
            '@axrone/input',
            '@axrone/asset-core',
            '@axrone/asset-gltf',
            '@axrone/asset-ui',
            '@axrone/render-3d',
            '@axrone/render-webgl2',
            '@axrone/physics-core',
            '@axrone/physics-3d',
            '@axrone/ui',
            '@axrone/ui-webgl2',
        ]);
    });

    it('contains no duplicate entries', () => {
        const unique = new Set(RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES);
        expect(unique.size).toBe(RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES.length);
    });

    it('excludes 2d-only capability packages', () => {
        expect(RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES).not.toContain('@axrone/asset-2d');
        expect(RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES).not.toContain('@axrone/render-2d');
        expect(RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES).not.toContain('@axrone/scene-2d');
        expect(RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES).not.toContain('@axrone/physics-2d');
    });

    it('resolves imported capability constants to their expected strings', () => {
        expect(INPUT_CAPABILITY_PACKAGE).toBe('@axrone/input');
        expect(RENDER_3D_CAPABILITY_PACKAGE).toBe('@axrone/render-3d');

        expect(RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES).toContain(INPUT_CAPABILITY_PACKAGE);
        expect(RUNTIME_PROFILE_3D_CAPABILITY_PACKAGES).toContain(RENDER_3D_CAPABILITY_PACKAGE);
    });
});
