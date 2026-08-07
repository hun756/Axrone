import { describe, it, expect } from 'vitest';
import { Rigidbody2D, RigidbodyType2D } from '../../components/rigidbody2d';

describe('Rigidbody2D', () => {
    function create() { return new Rigidbody2D(); }

    describe('default values', () => {
        it('has Dynamic body type by default', () => {
            expect(create().bodyType).toBe(RigidbodyType2D.Dynamic);
        });

        it('has mass 1', () => {
            expect(create().mass).toBe(1);
        });

        it('has linearDamping 0.01', () => {
            expect(create().linearDamping).toBe(0.01);
        });

        it('has angularDamping 0.01', () => {
            expect(create().angularDamping).toBe(0.01);
        });

        it('has gravityScale 1', () => {
            expect(create().gravityScale).toBe(1);
        });

        it('has fixedRotation false', () => {
            expect(create().fixedRotation).toBe(false);
        });

        it('has bullet false', () => {
            expect(create().bullet).toBe(false);
        });

        it('has null bodyId before start', () => {
            expect(create().bodyId).toBeNull();
        });
    });

    describe('property setters', () => {
        it('sets bodyType', () => {
            const rb = create();
            rb.bodyType = RigidbodyType2D.Static;
            expect(rb.bodyType).toBe(RigidbodyType2D.Static);
        });

        it('ignores non-positive mass', () => {
            const rb = create();
            rb.mass = 0;
            expect(rb.mass).toBe(1);
            rb.mass = -5;
            expect(rb.mass).toBe(1);
        });

        it('sets valid mass', () => {
            const rb = create();
            rb.mass = 5;
            expect(rb.mass).toBe(5);
        });

        it('clamps negative damping to 0', () => {
            const rb = create();
            rb.linearDamping = -1;
            expect(rb.linearDamping).toBe(0);
        });

        it('sets gravityScale', () => {
            const rb = create();
            rb.gravityScale = 2.5;
            expect(rb.gravityScale).toBe(2.5);
        });

        it('toggles fixedRotation', () => {
            const rb = create();
            rb.fixedRotation = true;
            expect(rb.fixedRotation).toBe(true);
        });

        it('toggles bullet', () => {
            const rb = create();
            rb.bullet = true;
            expect(rb.bullet).toBe(true);
        });
    });

    describe('velocity (without physics world)', () => {
        it('returns zero linearVelocity', () => {
            const rb = create();
            expect(rb.linearVelocity.x).toBe(0);
            expect(rb.linearVelocity.y).toBe(0);
        });

        it('stores linearVelocity locally', () => {
            const rb = create();
            rb.linearVelocity = { x: 3, y: 4 } as any;
            expect(rb.linearVelocity.x).toBe(3);
            expect(rb.linearVelocity.y).toBe(4);
        });

        it('returns zero angularVelocity', () => {
            expect(create().angularVelocity).toBe(0);
        });

        it('stores angularVelocity locally', () => {
            const rb = create();
            rb.angularVelocity = 2.5;
            expect(rb.angularVelocity).toBe(2.5);
        });
    });

    describe('sleep management', () => {
        it('isSleepingAllowed returns true by default', () => {
            expect(create().isSleepingAllowed()).toBe(true);
        });

        it('setSleepingAllowed toggles', () => {
            const rb = create();
            rb.setSleepingAllowed(false);
            expect(rb.isSleepingAllowed()).toBe(false);
        });

        it('isAwake returns false without physics world', () => {
            expect(create().isAwake()).toBe(false);
        });
    });

    describe('position/rotation without physics world', () => {
        it('getPosition returns zero', () => {
            const pos = create().getPosition();
            expect(pos.x).toBe(0);
            expect(pos.y).toBe(0);
        });

        it('getRotation returns 0', () => {
            expect(create().getRotation()).toBe(0);
        });

        it('setPosition does not throw', () => {
            const rb = create();
            expect(() => rb.setPosition({ x: 1, y: 2 } as any)).not.toThrow();
        });

        it('setRotation does not throw', () => {
            const rb = create();
            expect(() => rb.setRotation(1.5)).not.toThrow();
        });
    });

    describe('force/impulse methods without physics world', () => {
        it('applyForce does not throw', () => {
            expect(() => create().applyForce({ x: 1, y: 0 } as any)).not.toThrow();
        });

        it('applyForceToCenter does not throw', () => {
            expect(() => create().applyForceToCenter({ x: 1, y: 0 } as any)).not.toThrow();
        });

        it('applyTorque does not throw', () => {
            expect(() => create().applyTorque(5)).not.toThrow();
        });

        it('applyLinearImpulse does not throw', () => {
            expect(() => create().applyLinearImpulse({ x: 1, y: 0 } as any)).not.toThrow();
        });

        it('applyAngularImpulse does not throw', () => {
            expect(() => create().applyAngularImpulse(1)).not.toThrow();
        });
    });

    describe('serialize / deserialize', () => {
        it('serialize returns all properties', () => {
            const rb = create();
            rb.mass = 3;
            rb.gravityScale = 2;
            rb.bullet = true;
            const data = rb.serialize();
            expect(data.mass).toBe(3);
            expect(data.gravityScale).toBe(2);
            expect(data.bullet).toBe(true);
            expect(data.bodyType).toBe(RigidbodyType2D.Dynamic);
        });

        it('deserialize restores properties', () => {
            const rb = create();
            rb.deserialize({
                bodyType: RigidbodyType2D.Kinematic,
                mass: 10,
                linearDamping: 0.5,
                gravityScale: 0,
                fixedRotation: true,
                bullet: true,
                allowSleep: false,
            });
            expect(rb.bodyType).toBe(RigidbodyType2D.Kinematic);
            expect(rb.mass).toBe(10);
            expect(rb.linearDamping).toBe(0.5);
            expect(rb.gravityScale).toBe(0);
            expect(rb.fixedRotation).toBe(true);
            expect(rb.bullet).toBe(true);
        });

        it('deserialize uses defaults for missing fields', () => {
            const rb = create();
            rb.deserialize({});
            expect(rb.bodyType).toBe(RigidbodyType2D.Dynamic);
            expect(rb.mass).toBe(1);
        });
    });
});
