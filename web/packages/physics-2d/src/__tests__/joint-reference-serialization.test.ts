import { describe, it, expect } from 'vitest';
import { DistanceJoint2D } from '../components/distance-joint2d';
import { FixedJoint2D } from '../components/fixed-joint2d';
import { HingeJoint2D } from '../components/hinge-joint2d';
import { SliderJoint2D } from '../components/slider-joint2d';
import { SpringJoint2D } from '../components/spring-joint2d';
import { WheelJoint2D } from '../components/wheel-joint2d';
import { MotorJoint2D } from '../components/motor-joint2d';
import { MouseJoint2D } from '../components/mouse-joint2d';
import { GearJoint2D } from '../components/gear-joint2d';
import { RopeJoint2D } from '../components/rope-joint2d';
import { Rigidbody2D } from '../components/rigidbody2d';

/**
 * Serialisation contract tests for 2D joint component references.
 *
 * The Editor stores component references (`connectedBody`, `jointA`, `jointB`)
 * as **strings**, with `""` meaning "no reference". The engine stores them as
 * typed object references (`Rigidbody2D | null`, `Joint2D | null`).
 *
 * These tests verify:
 * 1. `serialize()` emits `""` for null references (Editor convention).
 * 2. `deserialize()` normalises `""`, `null`, `undefined`, whitespace → `null`.
 * 3. GearJoint `jointA`/`jointB` follow the same contract.
 * 4. GearJoint pending state is NOT triggered by `""` (no reference ≠ pending).
 * 5. Round-trip: null → serialize → `""` → deserialize → null.
 * 6. All 10 joint types inherit the base class contract.
 */

// ─── Base class (Joint2D) connectedBody contract ────────────────────────────

describe('Joint2D reference serialisation contract', () => {
    describe('serialize() emits connectedBody: "" for null reference', () => {
        it('DistanceJoint2D serializes connectedBody as ""', () => {
            const joint = new DistanceJoint2D();
            const data = joint.serialize();
            expect(data).toHaveProperty('connectedBody', '');
        });

        it('FixedJoint2D serializes connectedBody as ""', () => {
            const joint = new FixedJoint2D();
            const data = joint.serialize();
            expect(data).toHaveProperty('connectedBody', '');
        });

        it('HingeJoint2D serializes connectedBody as ""', () => {
            const joint = new HingeJoint2D();
            const data = joint.serialize();
            expect(data).toHaveProperty('connectedBody', '');
        });

        it('GearJoint2D serializes connectedBody, jointA, jointB as ""', () => {
            const joint = new GearJoint2D();
            const data = joint.serialize();
            expect(data).toHaveProperty('connectedBody', '');
            expect(data).toHaveProperty('jointA', '');
            expect(data).toHaveProperty('jointB', '');
        });

        it('RopeJoint2D serializes connectedBody as ""', () => {
            const joint = new RopeJoint2D();
            const data = joint.serialize();
            expect(data).toHaveProperty('connectedBody', '');
        });
    });

    describe('deserialize() normalises empty references to null', () => {
        it('"" → null (Editor default)', () => {
            const joint = new DistanceJoint2D();
            joint.deserialize({ connectedBody: '' });
            expect(joint.connectedBody).toBeNull();
        });

        it('null → null', () => {
            const joint = new DistanceJoint2D();
            joint.deserialize({ connectedBody: null });
            expect(joint.connectedBody).toBeNull();
        });

        it('undefined → does not overwrite default null', () => {
            const joint = new DistanceJoint2D();
            joint.deserialize({});
            expect(joint.connectedBody).toBeNull();
        });

        it('whitespace-only "   " → null', () => {
            const joint = new DistanceJoint2D();
            joint.deserialize({ connectedBody: '   ' });
            expect(joint.connectedBody).toBeNull();
        });

        it('tab/newline whitespace → null', () => {
            const joint = new HingeJoint2D();
            joint.deserialize({ connectedBody: '\t\n' });
            expect(joint.connectedBody).toBeNull();
        });
    });

    describe('round-trip: null → serialize → deserialize → null', () => {
        it('DistanceJoint2D round-trips connectedBody', () => {
            const original = new DistanceJoint2D();
            const data = original.serialize();
            expect(data.connectedBody).toBe('');

            const restored = new DistanceJoint2D();
            restored.deserialize(data);
            expect(restored.connectedBody).toBeNull();
        });

        it('SpringJoint2D round-trips connectedBody', () => {
            const original = new SpringJoint2D();
            const data = original.serialize();
            const restored = new SpringJoint2D();
            restored.deserialize(data);
            expect(restored.connectedBody).toBeNull();
        });

        it('SliderJoint2D round-trips connectedBody', () => {
            const original = new SliderJoint2D();
            const data = original.serialize();
            const restored = new SliderJoint2D();
            restored.deserialize(data);
            expect(restored.connectedBody).toBeNull();
        });

        it('WheelJoint2D round-trips connectedBody', () => {
            const original = new WheelJoint2D();
            const data = original.serialize();
            const restored = new WheelJoint2D();
            restored.deserialize(data);
            expect(restored.connectedBody).toBeNull();
        });

        it('MotorJoint2D round-trips connectedBody', () => {
            const original = new MotorJoint2D();
            const data = original.serialize();
            const restored = new MotorJoint2D();
            restored.deserialize(data);
            expect(restored.connectedBody).toBeNull();
        });

        it('MouseJoint2D round-trips connectedBody', () => {
            const original = new MouseJoint2D();
            const data = original.serialize();
            const restored = new MouseJoint2D();
            restored.deserialize(data);
            expect(restored.connectedBody).toBeNull();
        });

        it('RopeJoint2D round-trips connectedBody', () => {
            const original = new RopeJoint2D();
            const data = original.serialize();
            const restored = new RopeJoint2D();
            restored.deserialize(data);
            expect(restored.connectedBody).toBeNull();
        });
    });

    describe('deserialize does not crash on unexpected types', () => {
        it('number → stored as-is (scene loader handles resolution)', () => {
            const joint = new DistanceJoint2D();
            // A number is not a valid reference but should not crash.
            joint.deserialize({ connectedBody: 42 });
            // The normalizer passes through non-string, non-null values.
            expect(joint.connectedBody).toBe(42);
        });

        it('object (resolved ref) → stored as-is', () => {
            const joint = new DistanceJoint2D();
            const fakeRef = { entityId: 'test-entity' } as unknown as Rigidbody2D;
            joint.deserialize({ connectedBody: fakeRef });
            expect(joint.connectedBody).toBe(fakeRef);
        });
    });
});

// ─── GearJoint2D jointA/jointB contract ─────────────────────────────────────

describe('GearJoint2D jointA/jointB serialisation contract', () => {
    describe('deserialize normalises empty references to null', () => {
        it('jointA: "" → null', () => {
            const gear = new GearJoint2D();
            gear.deserialize({ jointA: '', jointB: '' });
            expect(gear.jointA).toBeNull();
            expect(gear.jointB).toBeNull();
        });

        it('jointA: whitespace → null', () => {
            const gear = new GearJoint2D();
            gear.deserialize({ jointA: '  ', jointB: '\t' });
            expect(gear.jointA).toBeNull();
            expect(gear.jointB).toBeNull();
        });

        it('missing jointA/jointB → stays null', () => {
            const gear = new GearJoint2D();
            gear.deserialize({ ratio: 2.0 });
            expect(gear.jointA).toBeNull();
            expect(gear.jointB).toBeNull();
            expect(gear.ratio).toBe(2.0);
        });

        it('null → null', () => {
            const gear = new GearJoint2D();
            gear.deserialize({ jointA: null, jointB: null });
            expect(gear.jointA).toBeNull();
            expect(gear.jointB).toBeNull();
        });
    });

    describe('round-trip: jointA/jointB', () => {
        it('GearJoint2D round-trips jointA and jointB', () => {
            const original = new GearJoint2D();
            const data = original.serialize();
            expect(data.jointA).toBe('');
            expect(data.jointB).toBe('');

            const restored = new GearJoint2D();
            restored.deserialize(data);
            expect(restored.jointA).toBeNull();
            expect(restored.jointB).toBeNull();
        });
    });

    describe('pending state interaction', () => {
        it('"" references do NOT trigger pending state', () => {
            const gear = new GearJoint2D();
            // Simulate deserializing from Editor data with empty references.
            gear.deserialize({ connectedBody: '', jointA: '', jointB: '' });
            // After deserializing "", all references should be null.
            expect(gear.jointA).toBeNull();
            expect(gear.jointB).toBeNull();
            // isPending should be false — "" means "no reference", not "waiting".
            expect(gear.isPending).toBe(false);
        });

        it('tryResolve returns true when not pending', () => {
            const gear = new GearJoint2D();
            gear.deserialize({ jointA: '', jointB: '' });
            // Not pending → tryResolve returns true immediately.
            expect(gear.tryResolve()).toBe(true);
        });
    });
});

// ─── Scalar properties survive round-trip alongside references ──────────────

describe('Joint scalar properties survive round-trip', () => {
    it('DistanceJoint2D preserves scalars when connectedBody is ""', () => {
        const joint = new DistanceJoint2D();
        joint.deserialize({
            connectedBody: '',
            distance: 5.0,
            stiffness: 100,
            damping: 0.5,
            enableCollision: true,
            breakForce: 500,
            breakTorque: 200,
            enabled: false,
        });
        expect(joint.connectedBody).toBeNull();
        expect(joint.enableCollision).toBe(true);
        expect(joint.breakForce).toBe(500);
        expect(joint.breakTorque).toBe(200);
    });

    it('GearJoint2D preserves ratio when jointA/jointB are ""', () => {
        const gear = new GearJoint2D();
        gear.deserialize({
            connectedBody: '',
            jointA: '',
            jointB: '',
            ratio: 3.5,
            enableCollision: true,
        });
        expect(gear.connectedBody).toBeNull();
        expect(gear.jointA).toBeNull();
        expect(gear.jointB).toBeNull();
        expect(gear.ratio).toBe(3.5);
        expect(gear.enableCollision).toBe(true);
    });
});
