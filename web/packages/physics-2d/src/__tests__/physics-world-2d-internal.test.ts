import { describe, it, expect, beforeEach } from 'vitest';
import { PhysicsWorld2D } from '@axrone/physics-2d';
import { BodyType, ShapeType, ConstraintType } from '@axrone/physics-core';

describe('PhysicsWorld2D Internal Module Integration', () => {
    let world: PhysicsWorld2D;

    beforeEach(() => {
        world = new PhysicsWorld2D({
            gravity: { x: 0, y: -10 },
            bodyCapacity: 256,
            shapeCapacity: 256,
            contactCapacity: 256,
            constraintCapacity: 256,
        });
    });

    describe('Body view through world facade', () => {
        it('getBody returns live facade reflecting manager state', () => {
            const bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 1, y: 2 },
                linearVelocity: { x: 3, y: 4 },
            });

            const body = world.getBody(bodyId);
            expect(body).not.toBeNull();
            expect(body!.id).toBe(bodyId);
            expect(body!.type).toBe(BodyType.Dynamic);
            expect(body!.getPosition()).toEqual({ x: 1, y: 2 });
            expect(body!.getLinearVelocity()).toEqual({ x: 3, y: 4 });
        });

        it('body facade setters update manager state', () => {
            const bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
            });

            const body = world.getBody(bodyId)!;
            body.setPosition({ x: 5, y: 10 });
            body.setLinearVelocity({ x: -1, y: 2 });

            expect(world.getBodyManager().getPosition(bodyId)).toEqual({ x: 5, y: 10 });
            expect(world.getBodyManager().getLinearVelocity(bodyId)).toEqual({ x: -1, y: 2 });
        });

        it('body facade reflects gravity scale changes', () => {
            const bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                gravityScale: 2,
            });

            const body = world.getBody(bodyId)!;
            expect(body.gravityScale).toBe(2);
        });

        it('body facade reports shapes array', () => {
            const bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
            });

            const shape1 = world.createCircleShape(bodyId, { radius: 1, offset: { x: 0, y: 0 } });
            const shape2 = world.createBoxShape(bodyId, { width: 2, height: 1, offset: { x: 1, y: 0 } });

            const body = world.getBody(bodyId)!;
            expect(body.shapes).toContain(shape1);
            expect(body.shapes).toContain(shape2);
            expect(body.shapes.length).toBe(2);
        });

        it('getBody returns null for destroyed body', () => {
            const bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
            });
            world.destroyBody(bodyId);
            expect(world.getBody(bodyId)).toBeNull();
        });

        it('getBodies returns all live bodies', () => {
            const b1 = world.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 } });
            const b2 = world.createBody({ type: BodyType.Static, position: { x: 5, y: 0 } });
            const b3 = world.createBody({ type: BodyType.Kinematic, position: { x: 10, y: 0 } });

            const bodies = world.getBodies();
            expect(bodies.size).toBe(3);
            expect(bodies.has(b1)).toBe(true);
            expect(bodies.has(b2)).toBe(true);
            expect(bodies.has(b3)).toBe(true);
        });
    });

    describe('Shape store through world facade', () => {
        it('getShape returns view with correct type', () => {
            const bodyId = world.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 } });
            const circleId = world.createCircleShape(bodyId, { radius: 1, offset: { x: 0, y: 0 } });
            const boxId = world.createBoxShape(bodyId, { width: 2, height: 1, offset: { x: 0, y: 0 } });

            expect(world.getShape(circleId)!.type).toBe(ShapeType.Circle);
            expect(world.getShape(boxId)!.type).toBe(ShapeType.Box);
        });

        it('shape view computeAABB reflects body position', () => {
            const bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 5, y: 5 },
            });
            const shapeId = world.createCircleShape(bodyId, {
                radius: 1,
                offset: { x: 0, y: 0 },
            });

            const shape = world.getShape(shapeId)!;
            const aabb = shape.computeAABB();
            expect(aabb.min.x).toBeCloseTo(4, 5);
            expect(aabb.min.y).toBeCloseTo(4, 5);
            expect(aabb.max.x).toBeCloseTo(6, 5);
            expect(aabb.max.y).toBeCloseTo(6, 5);
        });

        it('shape view testPoint works through world', () => {
            const bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
            });
            const shapeId = world.createCircleShape(bodyId, {
                radius: 2,
                offset: { x: 0, y: 0 },
            });

            const shape = world.getShape(shapeId)!;
            expect(shape.testPoint({ x: 0, y: 0 })).toBe(true);
            expect(shape.testPoint({ x: 1, y: 1 })).toBe(true);
            expect(shape.testPoint({ x: 5, y: 5 })).toBe(false);
        });

        it('shape view computeMassData returns positive mass', () => {
            const bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
            });
            const shapeId = world.createCircleShape(bodyId, {
                radius: 1,
                offset: { x: 0, y: 0 },
                density: 2,
            });

            const shape = world.getShape(shapeId)!;
            const massData = shape.computeMassData(2);
            expect(massData.mass).toBeGreaterThan(0);
        });

        it('destroyShape removes from shape store', () => {
            const bodyId = world.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 } });
            const shapeId = world.createCircleShape(bodyId, { radius: 1, offset: { x: 0, y: 0 } });

            expect(world.getShape(shapeId)).not.toBeNull();
            world.destroyShape(shapeId);
            expect(world.getShape(shapeId)).toBeNull();
        });

        it('capsule shape creates valid view', () => {
            const bodyId = world.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 } });
            const shapeId = world.createCapsuleShape(bodyId, {
                radius: 0.5,
                length: 2,
                offset: { x: 0, y: 0 },
            });

            const shape = world.getShape(shapeId)!;
            expect(shape.type).toBe(ShapeType.Capsule);
            expect(shape.computeMassData(1).mass).toBeGreaterThan(0);
        });

        it('polygon shape creates valid view', () => {
            const bodyId = world.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 } });
            const shapeId = world.createPolygonShape(bodyId, {
                vertices: [
                    { x: -1, y: -1 },
                    { x: 1, y: -1 },
                    { x: 1, y: 1 },
                    { x: -1, y: 1 },
                ],
            });

            const shape = world.getShape(shapeId)!;
            expect(shape.type).toBe(ShapeType.Polygon);
            expect(shape.testPoint({ x: 0, y: 0 })).toBe(true);
        });
    });

    describe('Constraint store through world facade', () => {
        it('getConstraint returns view with correct type', () => {
            const bodyA = world.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 } });
            const bodyB = world.createBody({ type: BodyType.Dynamic, position: { x: 5, y: 0 } });

            const distanceId = world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 5,
            });

            const constraint = world.getConstraint(distanceId)!;
            expect(constraint).not.toBeNull();
            expect(constraint.type).toBe(ConstraintType.Distance);
        });

        it('constraint enable/disable works through world', () => {
            const bodyA = world.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 } });
            const bodyB = world.createBody({ type: BodyType.Dynamic, position: { x: 5, y: 0 } });

            const constraintId = world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 5,
            });

            const constraint = world.getConstraint(constraintId)!;
            expect(constraint.isEnabled()).toBe(true);

            constraint.setEnabled(false);
            expect(constraint.isEnabled()).toBe(false);

            constraint.setEnabled(true);
            expect(constraint.isEnabled()).toBe(true);
        });

        it('constraint anchor queries work through world', () => {
            const bodyA = world.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 } });
            const bodyB = world.createBody({ type: BodyType.Dynamic, position: { x: 5, y: 0 } });

            const constraintId = world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 1, y: 0 },
                localAnchorB: { x: -1, y: 0 },
                length: 3,
            });

            const constraint = world.getConstraint(constraintId)!;
            const anchorA = constraint.getAnchorA();
            const anchorB = constraint.getAnchorB();

            // Anchors should be in world space
            expect(anchorA.x).toBeCloseTo(1, 5);
            expect(anchorB.x).toBeCloseTo(4, 5);
        });

        it('destroyConstraint removes from store', () => {
            const bodyA = world.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 } });
            const bodyB = world.createBody({ type: BodyType.Dynamic, position: { x: 5, y: 0 } });

            const constraintId = world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 5,
            });

            expect(world.getConstraint(constraintId)).not.toBeNull();
            world.destroyConstraint(constraintId);
            expect(world.getConstraint(constraintId)).toBeNull();
        });

        it('multiple constraint types coexist', () => {
            const bodyA = world.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 } });
            const bodyB = world.createBody({ type: BodyType.Dynamic, position: { x: 5, y: 0 } });

            const distanceId = world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 5,
            });

            const revoluteId = world.createRevoluteConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
            });

            const ropeId = world.createRopeConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                maxLength: 8,
            });

            expect(world.getConstraint(distanceId)!.type).toBe(ConstraintType.Distance);
            expect(world.getConstraint(revoluteId)!.type).toBe(ConstraintType.Revolute);
            expect(world.getConstraint(ropeId)!.type).toBe(ConstraintType.Rope);

            expect(world.getStatistics().constraintCount).toBe(3);
        });
    });

    describe('Edge cases through world facade', () => {
        it('destroyBody cascades to shapes, constraints, and contacts', () => {
            const bodyA = world.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 } });
            const bodyB = world.createBody({ type: BodyType.Dynamic, position: { x: 3, y: 0 } });
            world.createCircleShape(bodyA, { radius: 1, offset: { x: 0, y: 0 } });
            world.createCircleShape(bodyB, { radius: 1, offset: { x: 0, y: 0 } });
            world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 3,
            });

            expect(world.getStatistics().bodyCount).toBe(2);
            expect(world.getStatistics().shapeCount).toBe(2);
            expect(world.getStatistics().constraintCount).toBe(1);

            world.destroyBody(bodyA);

            expect(world.getStatistics().bodyCount).toBe(1);
            expect(world.getStatistics().shapeCount).toBe(1);
            expect(world.getStatistics().constraintCount).toBe(0);
        });

        it('queryAABB callback can be short-circuited', () => {
            for (let i = 0; i < 10; i++) {
                const body = world.createBody({
                    type: BodyType.Static,
                    position: { x: i * 2, y: 0 },
                });
                world.createCircleShape(body, { radius: 0.5, offset: { x: 0, y: 0 } });
            }

            let callCount = 0;
            world.queryAABB({ x: -1, y: -1 }, { x: 25, y: 1 }, () => {
                callCount++;
                return false; // Stop after first
            });

            expect(callCount).toBe(1);
        });

        it('rayCast callback can clip fraction', () => {
            const body1 = world.createBody({ type: BodyType.Static, position: { x: 2, y: 0 } });
            world.createCircleShape(body1, { radius: 0.5, offset: { x: 0, y: 0 } });

            const body2 = world.createBody({ type: BodyType.Static, position: { x: 5, y: 0 } });
            world.createCircleShape(body2, { radius: 0.5, offset: { x: 0, y: 0 } });

            let hitCount = 0;
            world.rayCast({ x: 0, y: 0 }, { x: 1, y: 0 }, 20, () => {
                hitCount++;
                return 0; // Terminate
            });

            expect(hitCount).toBe(1);
        });

        it('world validate returns true for healthy world', () => {
            expect(world.validate()).toBe(true);
        });

        it('world dispose cleans up internal stores', () => {
            const bodyId = world.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 } });
            world.createCircleShape(bodyId, { radius: 1, offset: { x: 0, y: 0 } });

            world[Symbol.dispose]();

            // Step should be no-op after dispose
            expect(() => world.step(1 / 60)).not.toThrow();
        });

        it('statistics reflect correct counts after mixed operations', () => {
            const b1 = world.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 } });
            const b2 = world.createBody({ type: BodyType.Dynamic, position: { x: 5, y: 0 } });
            const s1 = world.createCircleShape(b1, { radius: 1, offset: { x: 0, y: 0 } });
            world.createBoxShape(b2, { width: 2, height: 1, offset: { x: 0, y: 0 } });

            expect(world.getStatistics().bodyCount).toBe(2);
            expect(world.getStatistics().shapeCount).toBe(2);

            world.destroyShape(s1);
            expect(world.getStatistics().shapeCount).toBe(1);

            world.destroyBody(b2);
            expect(world.getStatistics().bodyCount).toBe(1);
            expect(world.getStatistics().shapeCount).toBe(0);
        });
    });

    describe('Simulation with internal modules', () => {
        it('body-view mass data updates after shape creation', () => {
            const bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
            });

            const bodyBefore = world.getBody(bodyId)!;
            const massBefore = bodyBefore.getMass();

            world.createCircleShape(bodyId, {
                radius: 2,
                offset: { x: 0, y: 0 },
                density: 5,
            });

            const bodyAfter = world.getBody(bodyId)!;
            const massAfter = bodyAfter.getMass();

            expect(massAfter).toBeGreaterThan(massBefore);
        });

        it('constraint store tracks constraints per body', () => {
            const bodyA = world.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 } });
            const bodyB = world.createBody({ type: BodyType.Dynamic, position: { x: 3, y: 0 } });
            const bodyC = world.createBody({ type: BodyType.Dynamic, position: { x: 6, y: 0 } });

            world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 3,
            });

            world.createDistanceConstraint({
                bodyIdA: bodyB,
                bodyIdB: bodyC,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 3,
            });

            // Body B should be involved in 2 constraints
            expect(world.getStatistics().constraintCount).toBe(2);
        });

        it('shape store queryAABBAll returns shapes overlapping region', () => {
            const body1 = world.createBody({ type: BodyType.Static, position: { x: 0, y: 0 } });
            const s1 = world.createCircleShape(body1, { radius: 1, offset: { x: 0, y: 0 } });

            const body2 = world.createBody({ type: BodyType.Static, position: { x: 10, y: 10 } });
            const s2 = world.createCircleShape(body2, { radius: 1, offset: { x: 0, y: 0 } });

            const nearHits = world.queryAABBAll({ x: -2, y: -2 }, { x: 2, y: 2 });
            expect(nearHits).toContain(s1);
            expect(nearHits).not.toContain(s2);

            const farHits = world.queryAABBAll({ x: 8, y: 8 }, { x: 12, y: 12 });
            expect(farHits).toContain(s2);
            expect(farHits).not.toContain(s1);
        });

        it('shape store rayCastAll returns sorted hits', () => {
            const body1 = world.createBody({ type: BodyType.Static, position: { x: 2, y: 0 } });
            const s1 = world.createCircleShape(body1, { radius: 0.5, offset: { x: 0, y: 0 } });

            const body2 = world.createBody({ type: BodyType.Static, position: { x: 5, y: 0 } });
            const s2 = world.createCircleShape(body2, { radius: 0.5, offset: { x: 0, y: 0 } });

            const hits = world.rayCastAll({ x: 0, y: 0 }, { x: 1, y: 0 }, 20);
            expect(hits.length).toBe(2);
            expect(hits[0].shapeId).toBe(s1);
            expect(hits[1].shapeId).toBe(s2);
            expect(hits[0].fraction).toBeLessThan(hits[1].fraction);
        });
    });
});
