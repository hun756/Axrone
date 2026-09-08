import { describe, it, expect } from 'vitest';
import { DistanceJoint2D } from '../components/distance-joint2d';

/**
 * NaN / Infinity guard tests for Joint2D normalizeVec2Value.
 *
 * 3D counterpart: `joint-normalizer-nan-guard.test.ts` in physics-3d.
 * Both packages MUST guard identically — see JSDoc cross-references.
 */

describe('Joint2D normalizeVec2Value rejects non-finite values', () => {
    it('array [NaN, 0] → safe default for anchorA', () => {
        const joint = new DistanceJoint2D();
        joint.deserialize({ anchorA: [NaN, 0] });
        expect(Number.isFinite(joint.anchorA.x)).toBe(true);
        expect(joint.anchorA.x).toBe(0);
        expect(joint.anchorA.y).toBe(0);
    });

    it('array [Infinity, 0] → safe default', () => {
        const joint = new DistanceJoint2D();
        joint.deserialize({ anchorA: [Infinity, 0] });
        expect(Number.isFinite(joint.anchorA.x)).toBe(true);
        expect(joint.anchorA.x).toBe(0);
    });

    it('object {x: NaN, y: 5} → x falls back, y preserved', () => {
        const joint = new DistanceJoint2D();
        joint.deserialize({ anchorA: { x: NaN, y: 5 } });
        expect(Number.isFinite(joint.anchorA.x)).toBe(true);
        expect(joint.anchorA.y).toBe(5);
    });

    it('array [-Infinity, 3] → x falls back, y preserved', () => {
        const joint = new DistanceJoint2D();
        joint.deserialize({ anchorA: [-Infinity, 3] });
        expect(Number.isFinite(joint.anchorA.x)).toBe(true);
        expect(joint.anchorA.y).toBe(3);
    });
});
