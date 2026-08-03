import { describe, it, expect } from 'vitest';
import { Rigidbody3D } from '../../components/rigidbody3d';

describe('Rigidbody3D', () => {
    function create() { return new Rigidbody3D(); }

    describe('default values', () => {
        it('has mass 1', () => { expect(create().mass).toBe(1); });
        it('has linearDamping 0', () => { expect(create().linearDamping).toBe(0); });
        it('has angularDamping 0.05', () => { expect(create().angularDamping).toBe(0.05); });
        it('has gravityScale 1', () => { expect(create().gravityScale).toBe(1); });
        it('has useGravity true', () => { expect(create().useGravity).toBe(true); });
        it('has isKinematic false', () => { expect(create().isKinematic).toBe(false); });
        it('has detectCollisions true', () => { expect(create().detectCollisions).toBe(true); });
        it('has maxAngularVelocity 50', () => { expect(create().maxAngularVelocity).toBe(50); });
        it('has maxDepenetrationVelocity 10', () => { expect(create().maxDepenetrationVelocity).toBe(10); });
        it('has sleepThreshold 0.005', () => { expect(create().sleepThreshold).toBe(0.005); });
        it('has isSleeping false', () => { expect(create().isSleeping).toBe(false); });
        it('has bodyId -1 (invalid)', () => { expect(create().bodyId).toBe(-1); });
    });

    describe('property setters', () => {
        it('sets mass (clamps non-positive to 0.0001)', () => {
            const rb = create();
            rb.mass = 5;
            expect(rb.mass).toBe(5);
            rb.mass = 0;
            expect(rb.mass).toBeCloseTo(0.0001, 4);
            rb.mass = -10;
            expect(rb.mass).toBeCloseTo(0.0001, 4);
        });

        it('clamps negative linearDamping to 0', () => {
            const rb = create();
            rb.linearDamping = -1;
            expect(rb.linearDamping).toBe(0);
        });

        it('clamps negative angularDamping to 0', () => {
            const rb = create();
            rb.angularDamping = -0.5;
            expect(rb.angularDamping).toBe(0);
        });

        it('sets gravityScale', () => {
            const rb = create();
            rb.gravityScale = 2.5;
            expect(rb.gravityScale).toBe(2.5);
        });

        it('toggles useGravity', () => {
            const rb = create();
            rb.useGravity = false;
            expect(rb.useGravity).toBe(false);
        });

        it('toggles isKinematic', () => {
            const rb = create();
            rb.isKinematic = true;
            expect(rb.isKinematic).toBe(true);
        });

        it('sets detectCollisions', () => {
            const rb = create();
            rb.detectCollisions = false;
            expect(rb.detectCollisions).toBe(false);
        });

        it('clamps negative maxAngularVelocity to 0', () => {
            const rb = create();
            rb.maxAngularVelocity = -5;
            expect(rb.maxAngularVelocity).toBe(0);
        });

        it('clamps negative maxDepenetrationVelocity to 0', () => {
            const rb = create();
            rb.maxDepenetrationVelocity = -1;
            expect(rb.maxDepenetrationVelocity).toBe(0);
        });

        it('clamps negative sleepThreshold to 0', () => {
            const rb = create();
            rb.sleepThreshold = -1;
            expect(rb.sleepThreshold).toBe(0);
        });
    });

    describe('velocity without physics world', () => {
        it('returns zero velocity', () => {
            const v = create().velocity;
            expect(v.x).toBe(0);
            expect(v.y).toBe(0);
            expect(v.z).toBe(0);
        });

        it('returns zero angularVelocity', () => {
            const v = create().angularVelocity;
            expect(v.x).toBe(0);
            expect(v.y).toBe(0);
            expect(v.z).toBe(0);
        });
    });

    describe('force/impulse methods without physics world', () => {
        it('addForce does not throw', () => {
            expect(() => create().addForce({ x: 1, y: 0, z: 0 })).not.toThrow();
        });
        it('addTorque does not throw', () => {
            expect(() => create().addTorque({ x: 0, y: 1, z: 0 })).not.toThrow();
        });
        it('addRelativeForce does not throw', () => {
            expect(() => create().addRelativeForce({ x: 0, y: 0, z: 1 })).not.toThrow();
        });
        it('addExplosionForce does not throw', () => {
            expect(() => create().addExplosionForce(10, { x: 0, y: 5, z: 0 }, 5)).not.toThrow();
        });
    });

    describe('sleep management', () => {
        it('wakeUp does not throw', () => {
            expect(() => create().wakeUp()).not.toThrow();
        });
        it('sleep does not throw', () => {
            expect(() => create().sleep()).not.toThrow();
        });
    });
});
