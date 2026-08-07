import { describe, expect, it } from 'vitest';
import {
    getRender3DCapability,
    RENDER_3D_CAPABILITY_ID,
    RENDER_3D_CAPABILITY_PACKAGE,
    RENDER_3D_OWNER_PACKAGE,
} from '../capabilities';

describe('render-3d capabilities', () => {
    it('getRender3DCapability returns a frozen object with correct fields', () => {
        const cap = getRender3DCapability();
        expect(Object.isFrozen(cap)).toBe(true);
        expect(cap.id).toBe('render/3d');
        expect(cap.packageName).toBe('@axrone/render-3d');
        expect(cap.ownerPackage).toBe('@axrone/render-core');
    });

    it('exported constants match expected values', () => {
        expect(RENDER_3D_CAPABILITY_ID).toBe('render/3d');
        expect(RENDER_3D_CAPABILITY_PACKAGE).toBe('@axrone/render-3d');
        expect(RENDER_3D_OWNER_PACKAGE).toBe('@axrone/render-core');
    });
});
