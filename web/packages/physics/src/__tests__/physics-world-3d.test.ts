import { describe, expect, it } from 'vitest';
import {
    BodyManager3D,
    ConstraintManager3D,
    PhysicsWorld3D,
    ShapeManager3D,
} from '@axrone/physics';

describe('PhysicsWorld3D modular structure', () => {
    it('exposes dedicated manager instances through the world facade', () => {
        const world = new PhysicsWorld3D();

        expect(world.getBodyManager()).toBeInstanceOf(BodyManager3D);
        expect(world.getShapeManager()).toBeInstanceOf(ShapeManager3D);
        expect(world.getConstraintManager()).toBeInstanceOf(ConstraintManager3D);
    });

    it('steps dynamic bodies through the extracted manager boundary', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: -10, z: 0 } });
        const bodyId = world.getBodyManager().createBody({
            type: 2,
            position: { x: 0, y: 10, z: 0 },
            linearVelocity: { x: 1, y: 0, z: 0 },
        });

        world.step(0.5);

        const position = world.getBodyManager().getPosition(bodyId);
        const velocity = world.getBodyManager().getLinearVelocity(bodyId);

        expect(position.x).toBeCloseTo(0.5, 5);
        expect(position.y).toBeCloseTo(7.5, 5);
        expect(velocity.y).toBeCloseTo(-5, 5);
    });

    it('keeps shape and constraint managers usable after extraction', () => {
        const world = new PhysicsWorld3D();
        const bodyA = world.getBodyManager().createBody({ type: 2 });
        const bodyB = world.getBodyManager().createBody({
            type: 2,
            position: { x: 1, y: 0, z: 0 },
        });

        const shapeId = world.getShapeManager().createSphere(bodyA, {
            center: { x: 0, y: 0, z: 0 },
            radius: 0.5,
        });
        const constraintId = world.getConstraintManager().createFixed({
            bodyIdA: bodyA,
            bodyIdB: bodyB,
            localAnchorA: { x: 0, y: 0, z: 0 },
            localAnchorB: { x: 0, y: 0, z: 0 },
        });

        expect(world.getShapeManager().getBodyForShape(shapeId)).toBe(bodyA);
        expect(world.getConstraintManager().getConstraintsForBody(bodyA)).toContain(constraintId);
        expect(world.getConstraintManager().getConstraintsForBody(bodyB)).toContain(constraintId);
    });

    it('exposes truthful body facades over manager-backed 3d runtime state', () => {
        const world = new PhysicsWorld3D();
        const bodyId = world.createBody({
            type: 2,
            position: { x: 1, y: 2, z: 3 },
            linearDamping: 0.25,
            angularDamping: 0.5,
            fixedRotation: true,
            bullet: true,
            userData: { label: 'hero' },
        });
        world.createSphereShape(bodyId, {
            center: { x: 0, y: 0, z: 0 },
            radius: 0.75,
        });

        const body = world.getBody(bodyId);

        expect(body).not.toBeNull();
        expect(body?.getPosition()).toEqual({ x: 1, y: 2, z: 3 });
        expect(body?.linearDamping).toBeCloseTo(0.25, 5);
        expect(body?.angularDamping).toBeCloseTo(0.5, 5);
        expect(body?.isFixedRotation()).toBe(true);
        expect(body?.isBullet()).toBe(true);
        expect(body?.shapes).toHaveLength(1);
        expect(body?.userData).toEqual({ label: 'hero' });

        body?.applyImpulseToCenter({ x: 2, y: 0, z: 0 });

        expect(world.getBodyManager().getLinearVelocity(bodyId).x).toBeCloseTo(2, 5);
    });

    it('supports world-created 3d shapes for queries and ray casts', () => {
        const world = new PhysicsWorld3D();
        const bodyId = world.createBody({
            type: 0,
            position: { x: 0, y: 0, z: 0 },
        });
        const sphereId = world.createSphereShape(
            bodyId,
            { center: { x: 0, y: 0, z: 0 }, radius: 1 },
            { density: 2 },
            { categoryBits: 0x2, maskBits: 0xffff, groupIndex: 0 },
            { userData: 'sphere' }
        );
        const boxId = world.createBoxShape(bodyId, {
            center: { x: 3, y: 0, z: 0 },
            halfExtents: { x: 0.5, y: 0.5, z: 0.5 },
        });

        const sphere = world.getShape(sphereId);
        const rayHit = world.rayCastClosest(
            { x: -5, y: 0, z: 0 },
            { x: 1, y: 0, z: 0 },
            10,
            { categoryBits: 0x2 }
        );

        expect(sphere).not.toBeNull();
        expect(sphere?.userData).toBe('sphere');
        expect(sphere?.computeMassData(2).mass).toBeCloseTo((4 / 3) * Math.PI * 2, 5);
        expect(world.queryPointAll({ x: 0.25, y: 0, z: 0 })).toContain(sphereId);
        expect(world.queryAABBAll({ x: 2.4, y: -1, z: -1 }, { x: 3.6, y: 1, z: 1 })).toContain(boxId);
        expect(rayHit?.shapeId).toBe(sphereId);
        expect(rayHit?.point.x).toBeCloseTo(-1, 5);
    });

    it('exposes supported 3d constraints through world-level facades', () => {
        const world = new PhysicsWorld3D();
        const bodyA = world.createBody({ type: 2, position: { x: 0, y: 0, z: 0 } });
        const bodyB = world.createBody({ type: 2, position: { x: 2, y: 0, z: 0 } });
        const constraintId = world.createHingeConstraint({
            bodyIdA: bodyA,
            bodyIdB: bodyB,
            localAnchorA: { x: 0.5, y: 0, z: 0 },
            localAnchorB: { x: -0.5, y: 0, z: 0 },
            localAxisA: { x: 0, y: 1, z: 0 },
            localAxisB: { x: 0, y: 1, z: 0 },
            enableLimit: true,
            lowerLimit: -0.25,
            upperLimit: 0.25,
            collideConnected: true,
            userData: { label: 'hinge' },
        });

        const constraint = world.getConstraint(constraintId);

        expect(constraint).not.toBeNull();
        expect(constraint?.collideConnected).toBe(true);
        expect(constraint?.userData).toEqual({ label: 'hinge' });
        expect(constraint?.getAnchorA()).toEqual({ x: 0.5, y: 0, z: 0 });
        expect(constraint?.getAnchorB()).toEqual({ x: 1.5, y: 0, z: 0 });

        constraint?.setEnabled(false);

        expect(constraint?.isEnabled()).toBe(false);
    });
});
