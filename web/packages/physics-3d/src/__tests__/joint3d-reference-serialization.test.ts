import { describe, it, expect } from 'vitest';
import { FixedJoint3D } from '../components/fixed-joint3d';
import { HingeJoint3D } from '../components/hinge-joint3d';
import { SpringJoint3D } from '../components/spring-joint3d';
import { SliderJoint3D } from '../components/slider-joint3d';
import { CharacterJoint3D } from '../components/character-joint3d';
import { ConfigurableJoint3D } from '../components/configurable-joint3d';
import { Rigidbody3D } from '../components/rigidbody3d';
import { PhysicsWorld3D } from '../core/physics-world-3d';

/**
 * Serialisation contract tests for 3D joint component references.
 *
 * The Editor stores component references (`connectedBody`) as **strings**,
 * with `""` meaning "no reference". The engine stores them as typed object
 * references (`Rigidbody3D | null`).
 *
 * These tests verify:
 * 1. `serialize()` emits `""` for null references (Editor convention).
 * 2. `deserialize()` normalises `""`, `null`, `undefined`, whitespace → `null`.
 * 3. Round-trip: null → serialize → `""` → deserialize → null.
 * 4. All 6 joint types inherit the base class contract.
 * 5. Non-empty string references are stored as-is (scene loader resolves).
 * 6. Discriminative: a valid connectedBody actually creates a constraint.
 * 7. Scalar properties survive round-trip alongside references.
 */

// ─── Base class (Joint3D) connectedBody contract ────────────────────────────

describe('Joint3D reference serialisation contract', () => {
    describe('serialize() emits connectedBody: "" for null reference', () => {
        it('FixedJoint3D serializes connectedBody as ""', () => {
            const joint = new FixedJoint3D();
            const data = joint.serialize();
            expect(data).toHaveProperty('connectedBody', '');
        });

        it('HingeJoint3D serializes connectedBody as ""', () => {
            const joint = new HingeJoint3D();
            const data = joint.serialize();
            expect(data).toHaveProperty('connectedBody', '');
        });

        it('SpringJoint3D serializes connectedBody as ""', () => {
            const joint = new SpringJoint3D();
            const data = joint.serialize();
            expect(data).toHaveProperty('connectedBody', '');
        });

        it('SliderJoint3D serializes connectedBody as ""', () => {
            const joint = new SliderJoint3D();
            const data = joint.serialize();
            expect(data).toHaveProperty('connectedBody', '');
        });

        it('CharacterJoint3D serializes connectedBody as ""', () => {
            const joint = new CharacterJoint3D();
            const data = joint.serialize();
            expect(data).toHaveProperty('connectedBody', '');
        });

        it('ConfigurableJoint3D serializes connectedBody as ""', () => {
            const joint = new ConfigurableJoint3D();
            const data = joint.serialize();
            expect(data).toHaveProperty('connectedBody', '');
        });
    });

    describe('deserialize() normalises empty references to null', () => {
        it('"" → null (Editor default)', () => {
            const joint = new FixedJoint3D();
            joint.deserialize({ connectedBody: '' });
            expect(joint.connectedBody).toBeNull();
        });

        it('null → null', () => {
            const joint = new FixedJoint3D();
            joint.deserialize({ connectedBody: null });
            expect(joint.connectedBody).toBeNull();
        });

        it('undefined (field missing) → stays null', () => {
            const joint = new FixedJoint3D();
            joint.deserialize({});
            expect(joint.connectedBody).toBeNull();
        });

        it('whitespace-only "   " → null', () => {
            const joint = new FixedJoint3D();
            joint.deserialize({ connectedBody: '   ' });
            expect(joint.connectedBody).toBeNull();
        });

        it('tab/newline whitespace → null', () => {
            const joint = new HingeJoint3D();
            joint.deserialize({ connectedBody: '\t\n' });
            expect(joint.connectedBody).toBeNull();
        });
    });

    describe('round-trip: null → serialize → deserialize → null', () => {
        it('FixedJoint3D round-trips connectedBody', () => {
            const original = new FixedJoint3D();
            const data = original.serialize();
            expect(data.connectedBody).toBe('');

            const restored = new FixedJoint3D();
            restored.deserialize(data);
            expect(restored.connectedBody).toBeNull();
        });

        it('HingeJoint3D round-trips connectedBody', () => {
            const original = new HingeJoint3D();
            const data = original.serialize();
            const restored = new HingeJoint3D();
            restored.deserialize(data);
            expect(restored.connectedBody).toBeNull();
        });

        it('SpringJoint3D round-trips connectedBody', () => {
            const original = new SpringJoint3D();
            const data = original.serialize();
            const restored = new SpringJoint3D();
            restored.deserialize(data);
            expect(restored.connectedBody).toBeNull();
        });

        it('SliderJoint3D round-trips connectedBody', () => {
            const original = new SliderJoint3D();
            const data = original.serialize();
            const restored = new SliderJoint3D();
            restored.deserialize(data);
            expect(restored.connectedBody).toBeNull();
        });

        it('CharacterJoint3D round-trips connectedBody', () => {
            const original = new CharacterJoint3D();
            const data = original.serialize();
            const restored = new CharacterJoint3D();
            restored.deserialize(data);
            expect(restored.connectedBody).toBeNull();
        });

        it('ConfigurableJoint3D round-trips connectedBody', () => {
            const original = new ConfigurableJoint3D();
            const data = original.serialize();
            const restored = new ConfigurableJoint3D();
            restored.deserialize(data);
            expect(restored.connectedBody).toBeNull();
        });
    });

    describe('deserialize does not crash on unexpected types', () => {
        it('non-empty string → stored as-is (scene loader resolves)', () => {
            const joint = new FixedJoint3D();
            joint.deserialize({ connectedBody: 'entity-42' });
            // The normalizer passes through non-empty strings.
            expect(joint.connectedBody).toBe('entity-42');
        });

        it('object (resolved ref) → stored as-is', () => {
            const joint = new FixedJoint3D();
            const fakeRef = { bodyId: 99 } as unknown as Rigidbody3D;
            joint.deserialize({ connectedBody: fakeRef });
            expect(joint.connectedBody).toBe(fakeRef);
        });
    });
});

// ─── Scalar properties survive round-trip alongside references ──────────────

describe('Joint3D scalar properties survive round-trip', () => {
    it('FixedJoint3D preserves scalars when connectedBody is ""', () => {
        const joint = new FixedJoint3D();
        joint.deserialize({
            connectedBody: '',
            anchor: { x: 1, y: 2, z: 3 },
            connectedAnchor: { x: 4, y: 5, z: 6 },
            autoConfigureConnectedAnchor: false,
            axis: { x: 0, y: 1, z: 0 },
            secondaryAxis: { x: 0, y: 0, z: 1 },
            breakForce: 500,
            breakTorque: 200,
            enableCollision: true,
            enablePreprocessing: false,
            massScale: 2.0,
            connectedMassScale: 3.0,
            enabled: false,
        });
        expect(joint.connectedBody).toBeNull();
        expect(joint.anchor).toEqual({ x: 1, y: 2, z: 3 });
        expect(joint.connectedAnchor).toEqual({ x: 4, y: 5, z: 6 });
        expect(joint.autoConfigureConnectedAnchor).toBe(false);
        expect(joint.axis).toEqual({ x: 0, y: 1, z: 0 });
        expect(joint.secondaryAxis).toEqual({ x: 0, y: 0, z: 1 });
        expect(joint.breakForce).toBe(500);
        expect(joint.breakTorque).toBe(200);
        expect(joint.enableCollision).toBe(true);
        expect(joint.enablePreprocessing).toBe(false);
        expect(joint.massScale).toBe(2.0);
        expect(joint.connectedMassScale).toBe(3.0);
    });

    it('HingeJoint3D preserves base scalars through round-trip', () => {
        const original = new HingeJoint3D();
        const data = original.serialize();
        // Verify scalars are emitted
        expect(data).toHaveProperty('breakForce', Infinity);
        expect(data).toHaveProperty('breakTorque', Infinity);
        expect(data).toHaveProperty('enableCollision', false);
        expect(data).toHaveProperty('massScale', 1);
        expect(data).toHaveProperty('connectedMassScale', 1);
    });
});

// ─── Discriminative: valid connectedBody creates constraint ─────────────────

describe('Joint3D discriminative: valid connectedBody creates constraint', () => {
    function createInitializedRigidbody(world: PhysicsWorld3D): Rigidbody3D {
        const rb = new Rigidbody3D();
        rb.initialize(world, { type: 2 /* Dynamic */ });
        return rb;
    }

    it('FixedJoint3D with valid connectedBody creates a constraint', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
        const rbA = createInitializedRigidbody(world);
        const rbB = createInitializedRigidbody(world);

        const joint = new FixedJoint3D();
        joint.initialize(world, rbA, rbB);

        // Constraint must be created (not INVALID_CONSTRAINT_ID)
        expect(joint.constraintId).not.toBe(-1);
        expect(joint.connectedBody).toBe(rbB);
    });

    it('FixedJoint3D with null connectedBody does NOT create a constraint', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
        const rbA = createInitializedRigidbody(world);

        const joint = new FixedJoint3D();
        // Initialize without connectedBody
        joint.initialize(world, rbA);

        // Constraint must NOT be created
        expect(joint.constraintId).toBe(-1);
        expect(joint.connectedBody).toBeNull();
    });

    it('deserialize("") then initialize does NOT create constraint (bug regression)', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
        const rbA = createInitializedRigidbody(world);

        const joint = new FixedJoint3D();
        // Simulate Editor sending "" for connectedBody
        joint.deserialize({ connectedBody: '' });
        expect(joint.connectedBody).toBeNull();

        // Now initialize — should NOT create constraint since connectedBody is null
        joint.initialize(world, rbA);
        expect(joint.constraintId).toBe(-1);
    });
});

// ─── breakForce / breakTorque Infinity ↔ 1e18 contract ──────────────────────

describe('Joint3D breakForce/breakTorque Infinity contract', () => {
    it('default breakForce is Infinity (unbreakable)', () => {
        const joint = new FixedJoint3D();
        expect(joint.breakForce).toBe(Infinity);
        expect(joint.breakTorque).toBe(Infinity);
    });

    it('serialize emits Infinity (JSON.stringify will produce null)', () => {
        const joint = new FixedJoint3D();
        const data = joint.serialize();
        expect(data.breakForce).toBe(Infinity);
        expect(data.breakTorque).toBe(Infinity);
        // JSON.stringify(Infinity) → null — this is why Editor uses 1e18
        expect(JSON.stringify(data.breakForce)).toBe('null');
    });

    it('deserialize 1e18 (Editor convention) → finite but practically unbreakable', () => {
        const joint = new FixedJoint3D();
        joint.deserialize({ breakForce: 1e18, breakTorque: 1e18 });
        expect(joint.breakForce).toBe(1e18);
        expect(joint.breakTorque).toBe(1e18);
        expect(Number.isFinite(joint.breakForce)).toBe(true);
    });

    it('deserialize Infinity → stays Infinity', () => {
        const joint = new FixedJoint3D();
        joint.deserialize({ breakForce: Infinity, breakTorque: Infinity });
        expect(joint.breakForce).toBe(Infinity);
        expect(joint.breakTorque).toBe(Infinity);
    });
});
