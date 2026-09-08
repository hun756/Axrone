import { describe, it, expect } from 'vitest';
import { FixedJoint3D } from '../components/fixed-joint3d';
import { HingeJoint3D } from '../components/hinge-joint3d';
import { CharacterJoint3D } from '../components/character-joint3d';

/**
 * NaN / Infinity guard tests for Joint3D deserialization normalizers.
 *
 * `typeof NaN === 'number'` is `true` in JavaScript, so without an explicit
 * `Number.isFinite()` guard, `[NaN, 0, 0]` would pass through as
 * `{x: NaN, y: 0, z: 0}` and silently corrupt the solver.
 *
 * 3D ↔ 2D consistency: both packages MUST guard identically. The JSDoc
 * cross-references between normalizeVec3Value and normalizeVec2Value
 * enforce this contract. 2D counterpart: `joint2d-nan-guard.test.ts`.
 */

describe('Joint3D normalizeVec3Value rejects non-finite values', () => {
    it('array [NaN, 0, 0] → safe default for anchor', () => {
        const joint = new FixedJoint3D();
        joint.deserialize({ anchor: [NaN, 0, 0] });
        expect(joint.anchor.x).toBe(0);
        expect(joint.anchor.y).toBe(0);
        expect(joint.anchor.z).toBe(0);
    });

    it('array [Infinity, 0, 0] → safe default', () => {
        const joint = new FixedJoint3D();
        joint.deserialize({ anchor: [Infinity, 0, 0] });
        expect(Number.isFinite(joint.anchor.x)).toBe(true);
        expect(joint.anchor.x).toBe(0);
    });

    it('array [-Infinity, 0, 0] → safe default', () => {
        const joint = new FixedJoint3D();
        joint.deserialize({ anchor: [-Infinity, 0, 0] });
        expect(Number.isFinite(joint.anchor.x)).toBe(true);
    });

    it('object {x: NaN, y: 0, z: 0} → safe default', () => {
        const joint = new FixedJoint3D();
        joint.deserialize({ anchor: { x: NaN, y: 0, z: 0 } });
        expect(Number.isFinite(joint.anchor.x)).toBe(true);
        expect(joint.anchor.x).toBe(0);
    });

    it('partial array [NaN] with length < 3 → full fallback', () => {
        const joint = new FixedJoint3D();
        joint.deserialize({ anchor: [NaN] });
        expect(Number.isFinite(joint.anchor.x)).toBe(true);
    });

    it('axis [NaN, 0, 0] → axis fallback (1, 0, 0)', () => {
        const joint = new HingeJoint3D();
        joint.deserialize({ axis: [NaN, 0, 0] });
        expect(joint.axis.x).toBe(1);
        expect(joint.axis.y).toBe(0);
        expect(joint.axis.z).toBe(0);
    });

    it('mixed valid/NaN: [1, NaN, 3] → only NaN component falls back', () => {
        const joint = new FixedJoint3D();
        joint.deserialize({ anchor: [1, NaN, 3] });
        expect(joint.anchor.x).toBe(1);
        expect(joint.anchor.y).toBe(0);
        expect(joint.anchor.z).toBe(3);
    });
});

describe('Joint3D secondaryAxis with non-finite values via normalizeVec3Value', () => {
    it('secondaryAxis [NaN, 0, 0] → each NaN component falls back independently', () => {
        const joint = new CharacterJoint3D();
        joint.deserialize({ secondaryAxis: [NaN, 0, 0] });
        // normalizeVec3Value fallback for secondaryAxis is (0, 1, 0),
        // but NaN→0(fallback), 0→0(valid), 0→0(valid) = (0, 0, 0)
        // because 0 IS finite, only NaN is replaced.
        expect(joint.secondaryAxis.x).toBe(0); // NaN → fallbackX=0
        expect(joint.secondaryAxis.y).toBe(0); // 0 is finite, kept as-is
        expect(joint.secondaryAxis.z).toBe(0); // 0 is finite, kept as-is
    });
});
