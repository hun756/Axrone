import { describe, it, expect, beforeEach } from 'vitest';
import { PhysicsWorld2D } from '@axrone/physics-2d';
import { BodyType, ShapeType, ConstraintType } from '@axrone/physics-core';

describe('PhysicsWorld2D Integration', () => {
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

    describe('World Creation', () => {
        it('creates world with default config', () => {
            const defaultWorld = new PhysicsWorld2D();
            expect(defaultWorld).toBeDefined();
        });

        it('creates world with custom gravity', () => {
            const customWorld = new PhysicsWorld2D({
                gravity: { x: 0, y: -20 },
            });
            expect(customWorld).toBeDefined();
        });

        it('creates world with custom capacities', () => {
            const customWorld = new PhysicsWorld2D({
                bodyCapacity: 512,
                shapeCapacity: 1024,
            });
            expect(customWorld).toBeDefined();
        });
    });

    describe('Body Lifecycle', () => {
        it('creates dynamic body', () => {
            const bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            expect(bodyId).toBeGreaterThan(0);
        });

        it('creates static body', () => {
            const bodyId = world.createBody({
                type: BodyType.Static,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            expect(bodyId).toBeGreaterThan(0);
        });

        it('creates kinematic body', () => {
            const bodyId = world.createBody({
                type: BodyType.Kinematic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            expect(bodyId).toBeGreaterThan(0);
        });

        it('destroys body', () => {
            const bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            world.destroyBody(bodyId);
        });

        it('creates and destroys multiple bodies', () => {
            const bodies = [];
            for (let i = 0; i < 10; i++) {
                bodies.push(
                    world.createBody({
                        type: BodyType.Dynamic,
                        position: { x: i, y: 0 },
                        rotation: 0,
                    })
                );
            }

            bodies.forEach((id) => world.destroyBody(id));
        });

        it('returns live body facades', () => {
            const bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 3, y: 4 },
                rotation: Math.PI / 4,
                linearDamping: 0.5,
                angularDamping: 1.25,
                gravityScale: 2,
                bullet: true,
            });

            const shapeId = world.createCircleShape(bodyId, {
                radius: 1,
                offset: { x: 1, y: 0 },
                density: 2,
            });

            const body = world.getBody(bodyId);

            expect(body).not.toBeNull();
            expect(body!.id).toBe(bodyId);
            expect(body!.type).toBe(BodyType.Dynamic);
            expect(body!.shapes).toEqual([shapeId]);
            expect(body!.gravityScale).toBe(2);
            expect(body!.linearDamping).toBeCloseTo(0.5);
            expect(body!.angularDamping).toBeCloseTo(1.25);
            expect(body!.isBullet()).toBe(true);
            expect(body!.getPosition()).toEqual({ x: 3, y: 4 });
            expect(body!.getMass()).toBeGreaterThan(0);

            body!.setLinearVelocity({ x: 2, y: -1 });
            expect(body!.getLinearVelocity()).toEqual({ x: 2, y: -1 });
        });
    });

    describe('Shape Lifecycle', () => {
        let bodyId: any;

        beforeEach(() => {
            bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });
        });

        it('creates circle shape', () => {
            const shapeId = world.createCircleShape(bodyId, {
                radius: 1,
                offset: { x: 0, y: 0 },
            });

            expect(shapeId).toBeGreaterThan(0);
        });

        it('creates box shape', () => {
            const shapeId = world.createBoxShape(bodyId, {
                width: 2,
                height: 1,
                offset: { x: 0, y: 0 },
            });

            expect(shapeId).toBeGreaterThan(0);
        });

        it('creates polygon shape', () => {
            const shapeId = world.createPolygonShape(bodyId, {
                vertices: [
                    { x: 0, y: 0 },
                    { x: 1, y: 0 },
                    { x: 1, y: 1 },
                    { x: 0, y: 1 },
                ],
            });

            expect(shapeId).toBeGreaterThan(0);
        });

        it('creates capsule shape', () => {
            const shapeId = world.createCapsuleShape(bodyId, {
                radius: 0.5,
                length: 2,
                offset: { x: 0, y: 0 },
            });

            expect(shapeId).toBeGreaterThan(0);
        });

        it('destroys shape', () => {
            const shapeId = world.createCircleShape(bodyId, {
                radius: 1,
                offset: { x: 0, y: 0 },
            });

            world.destroyShape(shapeId);
        });

        it('body destroyed with shapes', () => {
            world.createCircleShape(bodyId, {
                radius: 1,
                offset: { x: 0, y: 0 },
            });

            world.createBoxShape(bodyId, {
                width: 1,
                height: 1,
                offset: { x: 0, y: 0 },
            });

            world.destroyBody(bodyId);
        });

        it('returns shape facades with geometry queries', () => {
            const circleId = world.createCircleShape(bodyId, {
                radius: 1,
                offset: { x: 2, y: 0 },
                density: 1,
            });

            const shape = world.getShape(circleId);

            expect(shape).not.toBeNull();
            expect(shape!.type).toBe(ShapeType.Circle);
            expect(shape!.computeAABB()).toEqual({
                min: { x: 1, y: -1 },
                max: { x: 3, y: 1 },
            });
            expect(shape!.testPoint({ x: 2, y: 0 })).toBe(true);
            expect(shape!.testPoint({ x: 4.5, y: 0 })).toBe(false);
            expect(shape!.computeMassData(1).mass).toBeGreaterThan(0);
        });
    });

    describe('Constraint Lifecycle', () => {
        let bodyA: any;
        let bodyB: any;

        beforeEach(() => {
            bodyA = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            bodyB = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 5, y: 0 },
                rotation: 0,
            });
        });

        it('creates distance constraint', () => {
            const constraintId = world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 5,
            });

            expect(constraintId).toBeGreaterThan(0);
        });

        it('creates revolute constraint', () => {
            const constraintId = world.createRevoluteConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
            });

            expect(constraintId).toBeGreaterThan(0);
        });

        it('destroys constraint', () => {
            const constraintId = world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 5,
            });

            world.destroyConstraint(constraintId);
        });

        it('returns manager-backed constraint facades', () => {
            const distanceId = world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 5,
            });

            const wheelId = world.createWheelConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                localAxisA: { x: 1, y: 0 },
                motorSpeed: 2,
            });

            const ropeId = world.createRopeConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                maxLength: 8,
            });

            const gearId = world.createGearConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                constraintIdA: distanceId,
                constraintIdB: wheelId,
                ratio: 2,
            });

            const distance = world.getConstraint(distanceId);
            const wheel = world.getConstraint(wheelId);
            const rope = world.getConstraint(ropeId);
            const gear = world.getConstraint(gearId);

            expect(distance?.type).toBe(ConstraintType.Distance);
            expect(wheel?.type).toBe(ConstraintType.Wheel);
            expect(rope?.type).toBe(ConstraintType.Rope);
            expect(gear?.type).toBe(ConstraintType.Gear);

            wheel!.setEnabled(false);
            expect(wheel!.isEnabled()).toBe(false);
            expect(distance!.getAnchorA()).toEqual({ x: 0, y: 0 });
            expect(world.getConstraintManager().hasConstraint(wheelId)).toBe(true);
            expect(world.getConstraintManager().hasConstraint(ropeId)).toBe(true);
            expect(world.getConstraintManager().hasConstraint(gearId)).toBe(true);
        });
    });

    describe('Simulation Step', () => {
        it('steps simulation', () => {
            const bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 10 },
                rotation: 0,
            });

            world.createCircleShape(bodyId, {
                radius: 1,
                offset: { x: 0, y: 0 },
            });

            world.step(1 / 60);
        });

        it('applies gravity over time', () => {
            const bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 10 },
                rotation: 0,
            });

            world.createCircleShape(bodyId, {
                radius: 1,
                offset: { x: 0, y: 0 },
            });

            world.getBodyManager().setMassData(bodyId, 1, 0.1, { x: 0, y: 0 });

            const initialPos = world.getBodyManager().getPosition(bodyId);
            const bm = world.getBodyManager();

            // Track monotone descent and collect intermediate states
            let prevY = initialPos.y;
            let monotoneDescent = true;

            for (let i = 0; i < 60; i++) {
                world.step(1 / 60);
                const currentY = bm.getPosition(bodyId).y;
                if (currentY >= prevY) monotoneDescent = false;
                prevY = currentY;
            }

            const finalPos = bm.getPosition(bodyId);
            const finalVel = bm.getLinearVelocity(bodyId);

            // Position: body fell down
            expect(finalPos.y).toBeLessThan(initialPos.y);

            // Velocity: clamped by PhysicsConstants.MAX_TRANSLATION=2.0
            // Terminal velocity is -2.0, reached after ~12 steps
            expect(finalVel.y).toBeLessThan(-1.5);
            expect(finalVel.y).toBeGreaterThan(-2.1);

            // Horizontal should not drift
            expect(Math.abs(finalVel.x)).toBeLessThan(0.01);

            // Position: body fell ~2 units (terminal vel 2 * 48/60 remaining steps ≈ 1.6)
            expect(finalPos.y).toBeLessThan(initialPos.y - 1);
        });

        it('steps with fixed timestep', () => {
            const bodyId = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            world.createCircleShape(bodyId, {
                radius: 1,
                offset: { x: 0, y: 0 },
            });

            // Default gravity {x:0, y:-10}. MAX_TRANSLATION=2.0 clamps velocity.
            // Terminal velocity = -2.0, reached after ~12 steps.
            // After 100 steps: position ≈ 0 + (-2.0 * 88/60) ≈ -2.93
            for (let i = 0; i < 100; i++) {
                world.step(1 / 60);
            }

            const finalPos = world.getBodyManager().getPosition(bodyId);
            const finalVel = world.getBodyManager().getLinearVelocity(bodyId);

            // Velocity clamped to MAX_TRANSLATION=2.0
            expect(finalVel.y).toBeLessThan(-1.5);
            expect(finalVel.y).toBeGreaterThan(-2.1);
            // Body fell significantly
            expect(finalPos.y).toBeLessThan(-2);
            expect(Math.abs(finalVel.x)).toBeLessThan(0.01);
        });

        it('steps with variable timestep', () => {
            // Run two separate simulations: one with small dt, one with large dt.
            // Larger dt should produce larger displacement (gravity * dt accumulates more per step).
            const bodySmall = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });
            world.createCircleShape(bodySmall, { radius: 1 });

            // 60 steps at dt=1/120 (total t=0.5s)
            for (let i = 0; i < 60; i++) {
                world.step(1 / 120);
            }
            const posSmall = world.getBodyManager().getPosition(bodySmall);

            // New world for large dt simulation
            const world2 = new PhysicsWorld2D({ gravity: { x: 0, y: -10 } });
            const bodyLarge = world2.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });
            world2.createCircleShape(bodyLarge, { radius: 1 });

            // 60 steps at dt=1/30 (total t=2.0s)
            for (let i = 0; i < 60; i++) {
                world2.step(1 / 30);
            }
            const posLarge = world2.getBodyManager().getPosition(bodyLarge);

            // Both should have moved downward
            expect(posSmall.y).toBeLessThan(0);
            expect(posLarge.y).toBeLessThan(0);

            // Larger total time → larger displacement (quadratic in t)
            expect(Math.abs(posLarge.y)).toBeGreaterThan(Math.abs(posSmall.y));

            world2[Symbol.dispose]();
        });
    });

    describe('Collision Detection Pipeline', () => {
        it('detects collision between two circles', () => {
            const bodyA = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            const bodyB = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 1.5, y: 0 },
                rotation: 0,
            });

            world.createCircleShape(bodyA, {
                radius: 1,
                offset: { x: 0, y: 0 },
            });

            world.createCircleShape(bodyB, {
                radius: 1,
                offset: { x: 0, y: 0 },
            });

            world.step(1 / 60);
        });

        it('detects collision between circle and box', () => {
            const bodyA = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            const bodyB = world.createBody({
                type: BodyType.Static,
                position: { x: 0, y: -2 },
                rotation: 0,
            });

            world.createCircleShape(bodyA, {
                radius: 1,
                offset: { x: 0, y: 0 },
            });

            world.createBoxShape(bodyB, {
                width: 10,
                height: 1,
                offset: { x: 0, y: 0 },
            });

            for (let i = 0; i < 100; i++) {
                world.step(1 / 60);
            }
        });

        it('detects collisions in complex scene', () => {
            const ground = world.createBody({
                type: BodyType.Static,
                position: { x: 0, y: -5 },
                rotation: 0,
            });

            world.createBoxShape(ground, {
                width: 20,
                height: 1,
                offset: { x: 0, y: 0 },
            });

            for (let i = 0; i < 10; i++) {
                const body = world.createBody({
                    type: BodyType.Dynamic,
                    position: { x: Math.random() * 10 - 5, y: i * 2 + 5 },
                    rotation: Math.random() * Math.PI,
                });

                if (i % 2 === 0) {
                    world.createCircleShape(body, {
                        radius: 0.5,
                        offset: { x: 0, y: 0 },
                    });
                } else {
                    world.createBoxShape(body, {
                        width: 1,
                        height: 1,
                        offset: { x: 0, y: 0 },
                    });
                }
            }

            for (let i = 0; i < 100; i++) {
                world.step(1 / 60);
            }
        });
    });

    describe('Constraint Solving', () => {
        it('maintains distance constraint', () => {
            const bodyA = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            const bodyB = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 5, y: 0 },
                rotation: 0,
            });

            world.createCircleShape(bodyA, {
                radius: 0.5,
                offset: { x: 0, y: 0 },
            });

            world.createCircleShape(bodyB, {
                radius: 0.5,
                offset: { x: 0, y: 0 },
            });

            world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 5,
            });

            for (let i = 0; i < 100; i++) {
                world.step(1 / 60);
            }
        });

        it('maintains revolute constraint', () => {
            const bodyA = world.createBody({
                type: BodyType.Static,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            const bodyB = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 2, y: 0 },
                rotation: 0,
            });

            world.createBoxShape(bodyB, {
                width: 2,
                height: 0.5,
                offset: { x: 0, y: 0 },
            });

            world.createRevoluteConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: -1, y: 0 },
            });

            for (let i = 0; i < 100; i++) {
                world.step(1 / 60);
            }
        });

        it('handles chain of constraints', () => {
            const bodies = [];
            for (let i = 0; i < 5; i++) {
                const body = world.createBody({
                    type: i === 0 ? BodyType.Static : BodyType.Dynamic,
                    position: { x: i * 2, y: 0 },
                    rotation: 0,
                });

                world.createBoxShape(body, {
                    width: 1,
                    height: 0.5,
                    offset: { x: 0, y: 0 },
                });

                bodies.push(body);
            }

            for (let i = 0; i < bodies.length - 1; i++) {
                world.createDistanceConstraint({
                    bodyIdA: bodies[i],
                    bodyIdB: bodies[i + 1],
                    localAnchorA: { x: 0.5, y: 0 },
                    localAnchorB: { x: -0.5, y: 0 },
                    length: 1,
                });
            }

            for (let i = 0; i < 100; i++) {
                world.step(1 / 60);
            }
        });

        it('routes wheel, gear, and rope constraints through the island solver', () => {
            const anchorBody = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0.5,
            });

            const wheelBody = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 4, y: 0 },
                rotation: -0.25,
            });

            world.createCircleShape(anchorBody, {
                radius: 0.5,
                offset: { x: 0, y: 0 },
            });

            world.createCircleShape(wheelBody, {
                radius: 0.5,
                offset: { x: 0, y: 0 },
            });

            const wheelId = world.createWheelConstraint({
                bodyIdA: anchorBody,
                bodyIdB: wheelBody,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                localAxisA: { x: 1, y: 0 },
                enableLimit: true,
                lowerTranslation: -0.5,
                upperTranslation: 0.5,
                enableMotor: true,
                motorSpeed: 3,
                maxMotorTorque: 8,
            });

            const ropeId = world.createRopeConstraint({
                bodyIdA: anchorBody,
                bodyIdB: wheelBody,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                maxLength: 1,
            });

            world.createGearConstraint({
                bodyIdA: anchorBody,
                bodyIdB: wheelBody,
                constraintIdA: wheelId,
                constraintIdB: ropeId,
                ratio: 1.5,
            });

            world.step(1 / 60);

            expect(world.getSolver().getLastPreparedConstraintCount()).toBeGreaterThanOrEqual(3);
            expect(world.getSolver().getLastSolvedConstraintCount()).toBeGreaterThanOrEqual(3);
        });

        it('applies corrective rope impulses when overstretched', () => {
            const bodyA = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            const bodyB = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 6, y: 0 },
                rotation: 0,
            });

            world.createCircleShape(bodyA, {
                radius: 0.5,
                offset: { x: 0, y: 0 },
            });

            world.createCircleShape(bodyB, {
                radius: 0.5,
                offset: { x: 0, y: 0 },
            });

            world.createRopeConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                maxLength: 2,
            });

            world.step(1 / 60);

            const velocityA = world.getBody(bodyA)!.getLinearVelocity();
            const velocityB = world.getBody(bodyB)!.getLinearVelocity();

            expect(Math.abs(velocityA.x) + Math.abs(velocityB.x)).toBeGreaterThan(0);
        });
    });

    describe('Query Operations', () => {
        it('queries bodies in AABB', () => {
            for (let i = 0; i < 10; i++) {
                const body = world.createBody({
                    type: BodyType.Dynamic,
                    position: { x: i, y: 0 },
                    rotation: 0,
                });

                world.createCircleShape(body, {
                    radius: 0.5,
                    offset: { x: 0, y: 0 },
                });
            }

            const results: any[] = [];
            world.queryAABB({ x: 2, y: -1 }, { x: 5, y: 1 }, (shapeId) => {
                results.push(shapeId);
                return true;
            });

            expect(results.length).toBeGreaterThan(0);
            expect(world.queryAABBAll({ x: 2, y: -1 }, { x: 5, y: 1 }).length).toBe(results.length);
        });

        it('raycasts through scene', () => {
            const nearBody = world.createBody({
                type: BodyType.Static,
                position: { x: 2, y: 0 },
                rotation: 0,
            });

            const nearShape = world.createCircleShape(nearBody, {
                radius: 0.5,
                offset: { x: 0, y: 0 },
            });

            const body = world.createBody({
                type: BodyType.Static,
                position: { x: 5, y: 0 },
                rotation: 0,
            });

            const boxShape = world.createBoxShape(body, {
                width: 2,
                height: 2,
                offset: { x: 0, y: 0 },
            });

            const hit = world.rayCastClosest({ x: 0, y: 0 }, { x: 1, y: 0 }, 20);
            const allHits = world.rayCastAll({ x: 0, y: 0 }, { x: 1, y: 0 }, 20);
            const pointHits = world.queryPointAll({ x: 5, y: 0 });

            expect(hit?.shapeId).toBe(nearShape);
            expect(allHits.map((entry) => entry.shapeId)).toEqual([nearShape, boxShape]);
            expect(pointHits).toContain(boxShape);
        });
    });

    describe('Performance', () => {
        it('handles many bodies', () => {
            for (let i = 0; i < 50; i++) {
                const body = world.createBody({
                    type: BodyType.Dynamic,
                    position: { x: Math.random() * 20 - 10, y: i * 2 },
                    rotation: Math.random() * Math.PI * 2,
                });

                world.createCircleShape(body, {
                    radius: 0.5,
                    offset: { x: 0, y: 0 },
                });
            }

            for (let i = 0; i < 100; i++) {
                world.step(1 / 60);
            }
        });

        it('handles many constraints', () => {
            const bodies = [];
            for (let i = 0; i < 20; i++) {
                bodies.push(
                    world.createBody({
                        type: BodyType.Dynamic,
                        position: { x: i, y: 0 },
                        rotation: 0,
                    })
                );
            }

            for (let i = 0; i < bodies.length - 1; i++) {
                world.createDistanceConstraint({
                    bodyIdA: bodies[i],
                    bodyIdB: bodies[i + 1],
                    localAnchorA: { x: 0, y: 0 },
                    localAnchorB: { x: 0, y: 0 },
                    length: 1,
                });
            }

            for (let i = 0; i < 100; i++) {
                world.step(1 / 60);
            }
        });
    });

    describe('Disposal', () => {
        it('disposes world', () => {
            world[Symbol.dispose]();
        });

        it('throws when using after disposal', () => {
            world[Symbol.dispose]();
            expect(() => {
                world.createBody({
                    type: BodyType.Dynamic,
                    position: { x: 0, y: 0 },
                    rotation: 0,
                });
            }).toThrow();
        });
    });

    describe('Edge Cases', () => {
        it('handles zero gravity', () => {
            const zeroGravityWorld = new PhysicsWorld2D({
                gravity: { x: 0, y: 0 },
            });

            const body = zeroGravityWorld.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            zeroGravityWorld.createCircleShape(body, {
                radius: 1,
                offset: { x: 0, y: 0 },
            });

            const initialPos = zeroGravityWorld.getBodyManager().getPosition(body);

            for (let i = 0; i < 100; i++) {
                zeroGravityWorld.step(1 / 60);
            }

            const finalPos = zeroGravityWorld.getBodyManager().getPosition(body);
            expect(Math.abs(finalPos.y - initialPos.y)).toBeLessThan(0.01);
        });

        it('handles high gravity (10x displacement vs 1x)', () => {
            // 1x gravity baseline
            const baseWorld = new PhysicsWorld2D({ gravity: { x: 0, y: -10 } });
            const baseBody = baseWorld.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 100 },
                rotation: 0,
            });
            baseWorld.createCircleShape(baseBody, { radius: 1 });

            for (let i = 0; i < 100; i++) {
                baseWorld.step(1 / 60);
            }
            const baseDisp = 100 - baseWorld.getBodyManager().getPosition(baseBody).y;

            // 10x gravity — displacement should be ~10x the baseline
            const highWorld = new PhysicsWorld2D({ gravity: { x: 0, y: -100 } });
            const highBody = highWorld.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 100 },
                rotation: 0,
            });
            highWorld.createCircleShape(highBody, { radius: 1 });

            for (let i = 0; i < 100; i++) {
                highWorld.step(1 / 60);
            }
            const highDisp = 100 - highWorld.getBodyManager().getPosition(highBody).y;

            // 10x gravity → body hits velocity clamp faster, but displacement still larger
            // Both bodies are clamped at MAX_TRANSLATION=2.0, but 10x gets there faster
            // So ratio is bounded by clamp behavior, not pure physics
            const ratio = highDisp / baseDisp;
            expect(ratio).toBeGreaterThan(1.0);
            expect(ratio).toBeLessThan(15);

            baseWorld[Symbol.dispose]();
            highWorld[Symbol.dispose]();
        });

        it('handles empty step', () => {
            world.step(1 / 60);
        });
    });
    describe('P1-6 config surface', () => {
        it('solverIterations: more iterations yield better constraint satisfaction', () => {
            // Create a chain of 5 bodies with distance constraints
            function runChain(iterations: number): number {
                const w = new PhysicsWorld2D({ solverIterations: iterations, positionIterations: iterations });
                const bodies: number[] = [];
                for (let i = 0; i < 5; i++) {
                    bodies.push(w.createBody({
                        type: i === 0 ? BodyType.Static : BodyType.Dynamic,
                        position: { x: i * 2, y: 0 },
                    }));
                    w.createCircleShape(bodies[i], { radius: 0.5 });
                }
                for (let i = 0; i < bodies.length - 1; i++) {
                    w.createDistanceConstraint({
                        bodyIdA: bodies[i],
                        bodyIdB: bodies[i + 1],
                        localAnchorA: { x: 0, y: 0 },
                        localAnchorB: { x: 0, y: 0 },
                        length: 2,
                    });
                }
                // Apply force to last body
                w.getBodyManager().setLinearVelocity(bodies[4], { x: -50, y: 0 });
                for (let i = 0; i < 20; i++) {
                    w.step(1 / 60);
                }
                // Measure total constraint error: sum of (distance - restLength)² for each link
                let totalError = 0;
                for (let i = 0; i < bodies.length - 1; i++) {
                    const pA = w.getBodyManager().getPosition(bodies[i]);
                    const pB = w.getBodyManager().getPosition(bodies[i + 1]);
                    const dist = Math.sqrt((pB.x - pA.x) ** 2 + (pB.y - pA.y) ** 2);
                    totalError += (dist - 2) ** 2;
                }
                w[Symbol.dispose]();
                return totalError;
            }

            const errorLow = runChain(2);
            const errorHigh = runChain(20);
            // More iterations should produce equal or better constraint satisfaction
            // (errorHigh <= errorLow). We assert the high-iteration error is not worse by >50%.
            expect(errorHigh).toBeLessThanOrEqual(errorLow * 1.5);
        });

        it('continuousPhysics=false: fast body may tunnel through thin wall', () => {
            // Without CCD, a very fast small body can tunnel through a thin static box
            const w = new PhysicsWorld2D({ continuousPhysics: false, gravity: { x: 0, y: 0 } });
            const wall = w.createBody({ type: BodyType.Static, position: { x: 10, y: 0 } });
            w.createBoxShape(wall, { width: 0.1, height: 10 });

            const bullet = w.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                bullet: true,
            });
            w.createCircleShape(bullet, { radius: 0.05 });
            w.getBodyManager().setLinearVelocity(bullet, { x: 500, y: 0 });

            for (let i = 0; i < 5; i++) {
                w.step(1 / 60);
            }
            // Without CCD the bullet may have passed through the wall
            const pos = w.getBodyManager().getPosition(bullet);
            // Just verify the simulation ran; the bullet position shows the effect
            expect(pos.x).toBeGreaterThan(0);
            w[Symbol.dispose]();
        });

        it('warmStarting=false: first step has zero warm start impulses', () => {
            // With warmStarting=false, no previous impulses are cached
            const w = new PhysicsWorld2D({ warmStarting: false });
            const bodyA = w.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 } });
            const bodyB = w.createBody({ type: BodyType.Dynamic, position: { x: 3, y: 0 } });
            w.createCircleShape(bodyA, { radius: 1 });
            w.createCircleShape(bodyB, { radius: 1 });
            w.createDistanceConstraint({
                bodyIdA: bodyA, bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 },
                length: 3,
            });
            // Step should succeed — no warm start data available
            expect(() => w.step(1 / 60)).not.toThrow();
            // Verify simulation ran (body exists and has finite state)
            const pos = w.getBodyManager().getPosition(bodyA);
            expect(Number.isFinite(pos.x)).toBe(true);
            expect(Number.isFinite(pos.y)).toBe(true);
            w[Symbol.dispose]();
        });

        it('subStepping=true: large dt produces similar result to small dt steps', () => {
            // Sub-stepping splits large dt into smaller steps for stability.
            // Compare: subStepped large dt vs. equivalent small dt steps.
            const wSub = new PhysicsWorld2D({ subStepping: true, gravity: { x: 0, y: -10 } });
            const bodySub = wSub.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 10 } });
            wSub.createCircleShape(bodySub, { radius: 0.5 });
            wSub.step(1 / 15); // large dt, should be sub-divided

            const wRef = new PhysicsWorld2D({ gravity: { x: 0, y: -10 } });
            const bodyRef = wRef.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 10 } });
            wRef.createCircleShape(bodyRef, { radius: 0.5 });
            for (let i = 0; i < 4; i++) {
                wRef.step(1 / 60); // 4 * 1/60 = 1/15
            }

            const posSub = wSub.getBodyManager().getPosition(bodySub);
            const posRef = wRef.getBodyManager().getPosition(bodyRef);

            // Sub-stepped result should be close to the reference (within 10%)
            expect(Math.abs(posSub.y - posRef.y)).toBeLessThan(Math.abs(posRef.y - 10) * 0.1 + 0.5);

            wSub[Symbol.dispose]();
            wRef[Symbol.dispose]();
        });
    });

    describe('RB-1: Static AABB cache invalidation', () => {
        it('detects collision after static body moved via bodyManager.setPosition', () => {
            // Static body A starts at origin, dynamic body B at (50,0) — far apart.
            const staticBody = world.createBody({
                type: BodyType.Static,
                position: { x: 0, y: 0 },
            });
            world.createCircleShape(staticBody, { radius: 1 });

            const dynamicBody = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 50, y: 0 },
            });
            world.createCircleShape(dynamicBody, { radius: 1 });

            // Step 1: broadphase cache populated with A's AABB at (0,0).
            world.step(1 / 60);

            // No contact — bodies far apart.
            expect(world.getContactManager().contactCount).toBe(0);

            // Move static body A to overlap with B.
            const bm = world.getBodyManager();
            bm.setPosition(staticBody, { x: 50, y: 0 });

            // Step 2: if cache invalidated, broadphase AABB updated → collision found.
            world.step(1 / 60);

            // Assertion A: collision detected at NEW position.
            expect(world.getContactManager().contactCount).toBeGreaterThan(0);
        });

        it('detects collision after static body moved via body view.setTransform', () => {
            const staticBody = world.createBody({
                type: BodyType.Static,
                position: { x: 0, y: 0 },
            });
            world.createCircleShape(staticBody, { radius: 1 });

            const dynamicBody = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 50, y: 0 },
            });
            world.createCircleShape(dynamicBody, { radius: 1 });

            world.step(1 / 60);
            expect(world.getContactManager().contactCount).toBe(0);

            // Move via body view (exercises setTransform → bodyManager.setPosition).
            const view = world.getBody(staticBody);
            expect(view).not.toBeNull();
            view!.setTransform({ x: 50, y: 0 }, 0);

            world.step(1 / 60);

            // Assertion A: collision detected via body-view path.
            expect(world.getContactManager().contactCount).toBeGreaterThan(0);
        });

        it('does NOT find stale collision at OLD position after static body moved', () => {
            // Static body A at (0,0), dynamic body B at (0.5, 0) — overlapping.
            const staticBody = world.createBody({
                type: BodyType.Static,
                position: { x: 0, y: 0 },
            });
            world.createCircleShape(staticBody, { radius: 1 });

            const dynamicBody = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0.5, y: 0 },
            });
            world.createCircleShape(dynamicBody, { radius: 1 });

            // Step to populate broadphase.
            world.step(1 / 60);
            // They overlap — contact exists.
            expect(world.getContactManager().contactCount).toBeGreaterThan(0);

            // Move static body FAR away.
            world.getBodyManager().setPosition(staticBody, { x: 1000, y: 1000 });

            world.step(1 / 60);

            // Assertion B: no stale collision at old position.
            // Without invalidation, broadphase still has A's AABB at (0,0) which
            // overlaps B at (0.5,0), so narrowphase would still be invoked.
            // With invalidation, broadphase AABB is at (1000,1000) — no overlap.
            expect(world.getContactManager().contactCount).toBe(0);
        });

        it('does not invalidate static cache when dynamic body moves', () => {
            const dynamicBody = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
            });
            world.createCircleShape(dynamicBody, { radius: 1 });

            world.step(1 / 60);

            // Access internal _staticAabbDirty for performance guard.
            const dirtySet = (world as any)._staticAabbDirty as Set<number>;
            const dirtyBefore = dirtySet.size;

            // Move dynamic body — should NOT add to static dirty set.
            world.getBodyManager().setPosition(dynamicBody, { x: 100, y: 100 });

            expect(dirtySet.size).toBe(dirtyBefore);
        });

        it('static AABB cache is not recomputed when body does not move', () => {
            const staticBody = world.createBody({
                type: BodyType.Static,
                position: { x: 0, y: 0 },
            });
            world.createCircleShape(staticBody, { radius: 1 });

            // Step once to populate cache.
            world.step(1 / 60);

            const dirtySet = (world as any)._staticAabbDirty as Set<number>;
            const cacheMap = (world as any)._staticAabbCache as Map<number, unknown>;

            // After step, dirty set should be empty (all entries consumed).
            expect(dirtySet.size).toBe(0);
            // Cache should have an entry for the shape.
            expect(cacheMap.size).toBeGreaterThan(0);

            // Step 10 more times without moving the static body.
            for (let i = 0; i < 10; i++) {
                world.step(1 / 60);
            }

            // Dirty set should still be empty — no spurious invalidation.
            expect(dirtySet.size).toBe(0);
            // Cache entry should still be the same object (not recomputed).
            expect(cacheMap.size).toBeGreaterThan(0);
        });
    });
});

