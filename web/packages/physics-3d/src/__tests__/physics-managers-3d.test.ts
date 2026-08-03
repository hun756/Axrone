import { describe, it, expect, beforeEach } from 'vitest';
import { BodyManager3D, ShapeManager3D, ConstraintManager3D, PhysicsError3D } from '../core/physics-managers-3d';

describe('BodyManager3D', () => {
    let bm: BodyManager3D;

    beforeEach(() => {
        bm = new BodyManager3D(64);
    });

    describe('createBody', () => {
        it('creates a dynamic body with default position', () => {
            const id = bm.createBody({ type: 2 });
            expect(id).toBeDefined();
            expect(bm.bodyCount).toBe(1);
            const pos = bm.getPosition(id);
            expect(pos.x).toBe(0);
            expect(pos.y).toBe(0);
            expect(pos.z).toBe(0);
        });

        it('creates a body with custom position', () => {
            const id = bm.createBody({ type: 2, position: { x: 1, y: 2, z: 3 } });
            const pos = bm.getPosition(id);
            expect(pos.x).toBeCloseTo(1, 5);
            expect(pos.y).toBeCloseTo(2, 5);
            expect(pos.z).toBeCloseTo(3, 5);
        });

        it('creates a body with initial velocity', () => {
            const id = bm.createBody({ type: 2, linearVelocity: { x: 5, y: 0, z: -1 } });
            const vel = bm.getLinearVelocity(id);
            expect(vel.x).toBeCloseTo(5, 5);
            expect(vel.z).toBeCloseTo(-1, 5);
        });

        it('creates a body with rotation (identity quaternion default)', () => {
            const id = bm.createBody({ type: 2 });
            const rot = bm.getRotation(id);
            expect(rot.w).toBeCloseTo(1, 5);
            expect(rot.x).toBeCloseTo(0, 5);
        });

        it('assigns unique ids to successive bodies', () => {
            const a = bm.createBody({ type: 2 });
            const b = bm.createBody({ type: 2 });
            expect(a).not.toBe(b);
        });

        it('throws when capacity is exceeded', () => {
            const tiny = new BodyManager3D(2);
            tiny.createBody({ type: 2 });
            tiny.createBody({ type: 2 });
            expect(() => tiny.createBody({ type: 2 })).toThrow(PhysicsError3D);
        });
    });

    describe('destroyBody', () => {
        it('removes a body and decrements count', () => {
            const id = bm.createBody({ type: 2 });
            bm.destroyBody(id);
            expect(bm.bodyCount).toBe(0);
        });

        it('makes the body unfindable after destruction', () => {
            const id = bm.createBody({ type: 2 });
            bm.destroyBody(id);
            expect(bm.hasBody(id)).toBe(false);
        });

        it('is a no-op for unknown body ids', () => {
            expect(() => bm.destroyBody(999n as any)).not.toThrow();
        });

        it('allows reuse of slots after destruction', () => {
            const a = bm.createBody({ type: 2 });
            bm.destroyBody(a);
            const b = bm.createBody({ type: 2 });
            expect(bm.bodyCount).toBe(1);
            expect(bm.hasBody(b)).toBe(true);
        });
    });

    describe('position and velocity', () => {
        it('sets and gets position', () => {
            const id = bm.createBody({ type: 2 });
            bm.setPosition(id, { x: 10, y: 20, z: 30 });
            const pos = bm.getPosition(id);
            expect(pos.x).toBeCloseTo(10, 5);
            expect(pos.y).toBeCloseTo(20, 5);
            expect(pos.z).toBeCloseTo(30, 5);
        });

        it('sets and gets linear velocity', () => {
            const id = bm.createBody({ type: 2 });
            bm.setLinearVelocity(id, { x: 1, y: 2, z: 3 });
            const vel = bm.getLinearVelocity(id);
            expect(vel.x).toBeCloseTo(1, 5);
            expect(vel.y).toBeCloseTo(2, 5);
        });

        it('sets and gets angular velocity', () => {
            const id = bm.createBody({ type: 2 });
            bm.setAngularVelocity(id, { x: 0.1, y: 0.2, z: 0.3 });
            const vel = bm.getAngularVelocity(id);
            expect(vel.x).toBeCloseTo(0.1, 5);
        });

        it('returns zero for unknown body position', () => {
            const pos = bm.getPosition(999n as any);
            expect(pos.x).toBe(0);
        });
    });

    describe('body type and flags', () => {
        it('reports correct body type', () => {
            const dynamic = bm.createBody({ type: 2 });
            const static_ = bm.createBody({ type: 0 });
            expect(bm.getBodyType(dynamic)).toBe(2);
            expect(bm.getBodyType(static_)).toBe(0);
        });

        it('toggles enabled flag', () => {
            const id = bm.createBody({ type: 2 });
            expect(bm.isEnabled(id)).toBe(true);
            bm.setEnabled(id, false);
            expect(bm.isEnabled(id)).toBe(false);
        });

        it('toggles fixed rotation flag', () => {
            const id = bm.createBody({ type: 2, fixedRotation: true });
            expect(bm.isFixedRotation(id)).toBe(true);
            bm.setFixedRotation(id, false);
            expect(bm.isFixedRotation(id)).toBe(false);
        });

        it('toggles bullet flag', () => {
            const id = bm.createBody({ type: 2, bullet: true });
            expect(bm.isBullet(id)).toBe(true);
            bm.setBullet(id, false);
            expect(bm.isBullet(id)).toBe(false);
        });

        it('toggles awake flag', () => {
            const id = bm.createBody({ type: 2 });
            expect(bm.isAwake(id)).toBe(true);
            bm.setAwake(id, false);
            expect(bm.isAwake(id)).toBe(false);
        });
    });

    describe('mass and inertia', () => {
        it('sets and gets mass with inverse mass auto-computed', () => {
            const id = bm.createBody({ type: 2 });
            bm.setMass(id, 4);
            expect(bm.getMass(id)).toBeCloseTo(4, 5);
            expect(bm.getInverseMass(id)).toBeCloseTo(0.25, 5);
        });

        it('sets inverse mass to 0 when mass is 0', () => {
            const id = bm.createBody({ type: 2 });
            bm.setMass(id, 0);
            expect(bm.getInverseMass(id)).toBe(0);
        });

        it('sets and gets inertia tensor', () => {
            const id = bm.createBody({ type: 2 });
            bm.setInertiaTensor(id, { x: 2, y: 3, z: 4 });
            const inertia = bm.getInertiaTensor(id);
            expect(inertia.x).toBeCloseTo(2, 5);
            expect(inertia.y).toBeCloseTo(3, 5);
            expect(inertia.z).toBeCloseTo(4, 5);
        });
    });

    describe('damping and gravity scale', () => {
        it('sets and gets linear damping', () => {
            const id = bm.createBody({ type: 2, linearDamping: 0.5 });
            expect(bm.getLinearDamping(id)).toBeCloseTo(0.5, 5);
            bm.setLinearDamping(id, 0.9);
            expect(bm.getLinearDamping(id)).toBeCloseTo(0.9, 5);
        });

        it('sets and gets angular damping', () => {
            const id = bm.createBody({ type: 2, angularDamping: 0.3 });
            expect(bm.getAngularDamping(id)).toBeCloseTo(0.3, 5);
        });

        it('sets and gets gravity scale', () => {
            const id = bm.createBody({ type: 2, gravityScale: 2.5 });
            expect(bm.getGravityScale(id)).toBeCloseTo(2.5, 5);
            bm.setGravityScale(id, 0);
            expect(bm.getGravityScale(id)).toBe(0);
        });
    });

    describe('user data', () => {
        it('stores and retrieves user data', () => {
            const id = bm.createBody({ type: 2, userData: { label: 'hero' } });
            expect(bm.getUserData(id)).toEqual({ label: 'hero' });
        });

        it('allows setting user data after creation', () => {
            const id = bm.createBody({ type: 2 });
            bm.setUserData(id, 'custom');
            expect(bm.getUserData(id)).toBe('custom');
        });

        it('deletes user data when set to undefined', () => {
            const id = bm.createBody({ type: 2, userData: 'x' });
            bm.setUserData(id, undefined);
            expect(bm.getUserData(id)).toBeUndefined();
        });
    });

    describe('force and impulse application', () => {
        it('applyForceToCenter changes linear velocity', () => {
            const id = bm.createBody({ type: 2 });
            bm.setMass(id, 2);
            bm.applyForceToCenter(id, { x: 10, y: 0, z: 0 });
            const vel = bm.getLinearVelocity(id);
            expect(vel.x).toBeGreaterThan(0);
        });

        it('applyImpulseToCenter changes linear velocity', () => {
            const id = bm.createBody({ type: 2 });
            bm.setMass(id, 1);
            bm.applyImpulseToCenter(id, { x: 5, y: 0, z: 0 });
            const vel = bm.getLinearVelocity(id);
            expect(vel.x).toBeCloseTo(5, 5);
        });

        it('does not affect static bodies', () => {
            const id = bm.createBody({ type: 0 });
            bm.applyForceToCenter(id, { x: 100, y: 0, z: 0 });
            const vel = bm.getLinearVelocity(id);
            expect(vel.x).toBe(0);
        });

        it('wakes the body after applying force', () => {
            const id = bm.createBody({ type: 2 });
            bm.setAwake(id, false);
            bm.applyForceToCenter(id, { x: 1, y: 0, z: 0 });
            expect(bm.isAwake(id)).toBe(true);
        });
    });

    describe('getBodyIds', () => {
        it('returns all created body ids', () => {
            const a = bm.createBody({ type: 2 });
            const b = bm.createBody({ type: 0 });
            const ids = bm.getBodyIds();
            expect(ids).toContain(a);
            expect(ids).toContain(b);
            expect(ids).toHaveLength(2);
        });
    });

    describe('dispose', () => {
        it('clears internal maps so bodies are no longer findable', () => {
            const id = bm.createBody({ type: 2 });
            bm[Symbol.dispose]();
            // After dispose, the id-to-index map is cleared so hasBody returns false
            expect(bm.hasBody(id)).toBe(false);
        });
    });
});

describe('ShapeManager3D', () => {
    let sm: ShapeManager3D;
    let bodyId: bigint;

    beforeEach(() => {
        sm = new ShapeManager3D(64);
        bodyId = 1n as any;
    });

    describe('createSphere', () => {
        it('creates a sphere shape and links it to the body', () => {
            const sid = sm.createSphere(bodyId, { center: { x: 0, y: 0, z: 0 }, radius: 1 });
            expect(sid).toBeDefined();
            expect(sm.shapeCount).toBe(1);
            expect(sm.getBodyForShape(sid)).toBe(bodyId);
        });
    });

    describe('createBox', () => {
        it('creates a box shape', () => {
            const sid = sm.createBox(bodyId, { center: { x: 0, y: 0, z: 0 }, halfExtents: { x: 1, y: 1, z: 1 } });
            expect(sm.getShapeType(sid)).toBeGreaterThan(0);
        });
    });

    describe('createCapsule', () => {
        it('creates a capsule shape', () => {
            const sid = sm.createCapsule(bodyId, { p1: { x: 0, y: -1, z: 0 }, p2: { x: 0, y: 1, z: 0 }, radius: 0.5 });
            expect(sm.getBodyForShape(sid)).toBe(bodyId);
        });
    });

    describe('createCylinder', () => {
        it('creates a cylinder shape', () => {
            const sid = sm.createCylinder(bodyId, { center: { x: 0, y: 0, z: 0 }, radius: 0.5, height: 2 });
            expect(sm.shapeCount).toBe(1);
        });
    });

    describe('createCone', () => {
        it('creates a cone shape', () => {
            const sid = sm.createCone(bodyId, { center: { x: 0, y: 0, z: 0 }, radius: 0.5, height: 2 });
            expect(sm.shapeCount).toBe(1);
        });
    });

    describe('destroyShape', () => {
        it('removes the shape and decrements count', () => {
            const sid = sm.createSphere(bodyId, { center: { x: 0, y: 0, z: 0 }, radius: 1 });
            sm.destroyShape(sid);
            expect(sm.shapeCount).toBe(0);
        });

        it('unlinks the shape from its body', () => {
            const sid = sm.createSphere(bodyId, { center: { x: 0, y: 0, z: 0 }, radius: 1 });
            sm.destroyShape(sid);
            expect(sm.getShapesForBody(bodyId)).toHaveLength(0);
        });
    });

    describe('getShapesForBody', () => {
        it('returns all shapes attached to a body', () => {
            const s1 = sm.createSphere(bodyId, { center: { x: 0, y: 0, z: 0 }, radius: 1 });
            const s2 = sm.createBox(bodyId, { center: { x: 0, y: 0, z: 0 }, halfExtents: { x: 1, y: 1, z: 1 } });
            const shapes = sm.getShapesForBody(bodyId);
            expect(shapes).toContain(s1);
            expect(shapes).toContain(s2);
            expect(shapes).toHaveLength(2);
        });
    });

    describe('material and filter', () => {
        it('applies custom material values', () => {
            const sid = sm.createSphere(bodyId, { center: { x: 0, y: 0, z: 0 }, radius: 1 }, { friction: 0.8, restitution: 0.5, density: 2 });
            const mat = sm.getMaterial(sid);
            expect(mat.friction).toBeCloseTo(0.8, 5);
            expect(mat.restitution).toBeCloseTo(0.5, 5);
            expect(mat.density).toBeCloseTo(2, 5);
        });

        it('uses defaults when no material provided', () => {
            const sid = sm.createSphere(bodyId, { center: { x: 0, y: 0, z: 0 }, radius: 1 });
            const mat = sm.getMaterial(sid);
            expect(mat.friction).toBeCloseTo(0.5, 5);
            expect(mat.density).toBeCloseTo(1, 5);
        });

        it('applies custom collision filter', () => {
            const sid = sm.createSphere(bodyId, { center: { x: 0, y: 0, z: 0 }, radius: 1 }, undefined, { categoryBits: 0x02, maskBits: 0x04, groupIndex: 1 });
            const filter = sm.getFilter(sid);
            expect(filter.categoryBits).toBe(0x02);
            expect(filter.maskBits).toBe(0x04);
            expect(filter.groupIndex).toBe(1);
        });
    });

    describe('sensor flag', () => {
        it('marks shape as sensor when option set', () => {
            const sid = sm.createSphere(bodyId, { center: { x: 0, y: 0, z: 0 }, radius: 1 }, undefined, undefined, { isSensor: true });
            expect(sm.isSensor(sid)).toBe(true);
        });

        it('defaults to non-sensor', () => {
            const sid = sm.createSphere(bodyId, { center: { x: 0, y: 0, z: 0 }, radius: 1 });
            expect(sm.isSensor(sid)).toBe(false);
        });
    });

    describe('dispose', () => {
        it('clears internal maps so shapes are no longer findable', () => {
            const sid = sm.createSphere(bodyId, { center: { x: 0, y: 0, z: 0 }, radius: 1 });
            sm[Symbol.dispose]();
            expect(sm.getShapesForBody(bodyId)).toHaveLength(0);
        });
    });
});

describe('ConstraintManager3D', () => {
    let cm: ConstraintManager3D;
    const bodyA = 1n as any;
    const bodyB = 2n as any;

    beforeEach(() => {
        cm = new ConstraintManager3D(64);
    });

    describe('createFixedConstraint', () => {
        it('creates a fixed constraint linking two bodies', () => {
            const cid = cm.createFixedConstraint({
                bodyIdA: bodyA, bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
            });
            expect(cid).toBeDefined();
            expect(cm.constraintCount).toBe(1);
        });
    });

    describe('createHingeConstraint', () => {
        it('creates a hinge constraint', () => {
            const cid = cm.createHingeConstraint({
                bodyIdA: bodyA, bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                localAxisA: { x: 0, y: 1, z: 0 },
                localAxisB: { x: 0, y: 1, z: 0 },
            });
            expect(cm.getConstraintType(cid)).toBe(2);
        });
    });

    describe('createSliderConstraint', () => {
        it('creates a slider constraint', () => {
            const cid = cm.createSliderConstraint({
                bodyIdA: bodyA, bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                localAxisA: { x: 1, y: 0, z: 0 },
            });
            expect(cm.getConstraintType(cid)).toBe(3);
        });
    });

    describe('createSpringConstraint', () => {
        it('creates a spring constraint', () => {
            const cid = cm.createSpringConstraint({
                bodyIdA: bodyA, bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                restLength: 2,
                stiffness: 20,
                damping: 1,
            });
            expect(cm.getConstraintType(cid)).toBe(6);
        });
    });

    describe('getConstraintsForBody', () => {
        it('returns constraints linked to a body', () => {
            const c1 = cm.createFixedConstraint({
                bodyIdA: bodyA, bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
            });
            const constraints = cm.getConstraintsForBody(bodyA);
            expect(constraints).toContain(c1);
        });

        it('returns empty array for body with no constraints', () => {
            expect(cm.getConstraintsForBody(99n as any)).toHaveLength(0);
        });
    });

    describe('destroyConstraint', () => {
        it('removes the constraint and decrements count', () => {
            const cid = cm.createFixedConstraint({
                bodyIdA: bodyA, bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
            });
            cm.destroyConstraint(cid);
            expect(cm.constraintCount).toBe(0);
        });

        it('removes constraint from body lookups', () => {
            const cid = cm.createFixedConstraint({
                bodyIdA: bodyA, bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
            });
            cm.destroyConstraint(cid);
            expect(cm.getConstraintsForBody(bodyA)).toHaveLength(0);
            expect(cm.getConstraintsForBody(bodyB)).toHaveLength(0);
        });
    });

    describe('alias methods', () => {
        it('createFixed delegates to createFixedConstraint', () => {
            const cid = cm.createFixed({
                bodyIdA: bodyA, bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
            });
            expect(cm.constraintCount).toBe(1);
        });
    });

    describe('dispose', () => {
        it('clears internal maps so constraints are no longer findable', () => {
            const cid = cm.createFixedConstraint({
                bodyIdA: bodyA, bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
            });
            cm[Symbol.dispose]();
            expect(cm.getConstraintsForBody(bodyA)).toHaveLength(0);
        });
    });
});
