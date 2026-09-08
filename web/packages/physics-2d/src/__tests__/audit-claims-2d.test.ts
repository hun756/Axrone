import { describe, it, expect, beforeEach } from 'vitest';
import { PhysicsWorld2D } from '@axrone/physics-2d';
import { BodyType, CollisionFilter } from '@axrone/physics-core';

/**
 * Audit claim verification tests.
 * 4 claims from the 3D audit that may have 2D counterparts.
 */

// ─── İDDİA 1: applyForce accumulated forces not integrated ──────────────
describe('Audit Claim 1: applyForce force accumulation', () => {
    let world: PhysicsWorld2D;

    beforeEach(() => {
        world = new PhysicsWorld2D({
            gravity: { x: 0, y: 0 }, // zero gravity to isolate force effect
            bodyCapacity: 64,
            shapeCapacity: 64,
            contactCapacity: 64,
            constraintCapacity: 64,
        });
    });

    it('applyForce should change dynamic body velocity after step', () => {
        const bodyId = world.createBody({
            type: BodyType.Dynamic,
            position: { x: 0, y: 0 },
            gravityScale: 0,
        });
        world.createCircleShape(bodyId, { radius: 1, density: 1 });

        // Apply horizontal force
        const bm = world.getBodyManager();
        bm.applyForce(bodyId, { x: 100, y: 0 });

        // Step
        world.step(1 / 60);

        // Body should have gained velocity in force direction
        const vel = bm.getLinearVelocity(bodyId);
        expect(vel.x).toBeGreaterThan(0);
    });

    it('applyTorque should change angular velocity after step', () => {
        const bodyId = world.createBody({
            type: BodyType.Dynamic,
            position: { x: 0, y: 0 },
            gravityScale: 0,
        });
        world.createCircleShape(bodyId, { radius: 1, density: 1 });

        const bm = world.getBodyManager();
        bm.applyTorque(bodyId, 50);

        world.step(1 / 60);

        const angVel = bm.getAngularVelocity(bodyId);
        expect(Math.abs(angVel)).toBeGreaterThan(0);
    });

    it('clearForces should prevent force from being applied in next step', () => {
        const bodyId = world.createBody({
            type: BodyType.Dynamic,
            position: { x: 0, y: 0 },
            gravityScale: 0,
        });
        world.createCircleShape(bodyId, { radius: 1, density: 1 });

        const bm = world.getBodyManager();

        // Apply force and step with autoClearForces disabled
        world.setAutoClearForces(false);
        bm.applyForce(bodyId, { x: 100, y: 0 });
        world.step(1 / 60);

        const vel1 = bm.getLinearVelocity(bodyId);
        const vx1 = vel1.x;

        // Now clear forces manually, step again without applying new force
        world.clearForces();
        world.step(1 / 60);

        const vel2 = bm.getLinearVelocity(bodyId);
        // Velocity should not increase further (no new force applied)
        // It should remain roughly the same (only damping could reduce it)
        expect(vel2.x).toBeLessThanOrEqual(vx1 + 0.001);
    });

    it('force integration should be frame-rate independent (F * dt * invMass)', () => {
        // Two simulations: one at 60fps, one at 30fps (2 steps of 1/60)
        // After same total time, velocities should be similar
        const force = { x: 60, y: 0 }; // 60N on 1kg body → ~1 m/s per second

        // Sim A: single step at dt=1/60
        const worldA = new PhysicsWorld2D({
            gravity: { x: 0, y: 0 },
            bodyCapacity: 16, shapeCapacity: 16, contactCapacity: 16, constraintCapacity: 16,
        });
        const bodyA = worldA.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 }, gravityScale: 0 });
        worldA.createCircleShape(bodyA, { radius: 1, density: 1 });
        worldA.getBodyManager().applyForce(bodyA, force);
        worldA.step(1 / 60);
        const velA = worldA.getBodyManager().getLinearVelocity(bodyA);

        // Sim B: two steps at dt=1/120 each (total = 1/60)
        const worldB = new PhysicsWorld2D({
            gravity: { x: 0, y: 0 },
            bodyCapacity: 16, shapeCapacity: 16, contactCapacity: 16, constraintCapacity: 16,
        });
        const bodyB = worldB.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 }, gravityScale: 0 });
        worldB.createCircleShape(bodyB, { radius: 1, density: 1 });
        // Apply force before each sub-step
        worldB.getBodyManager().applyForce(bodyB, force);
        worldB.step(1 / 120);
        worldB.getBodyManager().applyForce(bodyB, force);
        worldB.step(1 / 120);
        const velB = worldB.getBodyManager().getLinearVelocity(bodyB);

        // Velocities should be approximately equal (within 10% tolerance)
        expect(Math.abs(velA.x - velB.x)).toBeLessThan(Math.abs(velA.x) * 0.1 + 0.01);
    });
});

// ─── İDDİA 2: Kinematic body pushing dynamic ────────────────────────────
describe('Audit Claim 2: Kinematic body pushes dynamic', () => {
    let world: PhysicsWorld2D;

    beforeEach(() => {
        world = new PhysicsWorld2D({
            gravity: { x: 0, y: 0 },
            bodyCapacity: 64,
            shapeCapacity: 64,
            contactCapacity: 64,
            constraintCapacity: 64,
        });
    });

    it('kinematic body with velocity should push dynamic body on contact', () => {
        // Dynamic body at rest
        const dynId = world.createBody({
            type: BodyType.Dynamic,
            position: { x: 0.8, y: 0 }, // overlapping with kinematic (gap < 0)
        });
        world.createCircleShape(dynId, { radius: 0.5, density: 1 });

        // Kinematic body moving toward dynamic (already overlapping)
        const kinId = world.createBody({
            type: BodyType.Kinematic,
            position: { x: 0, y: 0 },
        });
        world.createCircleShape(kinId, { radius: 0.5, density: 1 });
        world.getBodyManager().setLinearVelocity(kinId, { x: 5, y: 0 });

        // Step multiple times to allow contact and response
        for (let i = 0; i < 30; i++) {
            world.step(1 / 60);
        }

        // Dynamic body should have been pushed (gained positive x velocity or moved)
        const dynVel = world.getBodyManager().getLinearVelocity(dynId);
        const dynPos = world.getBodyManager().getPosition(dynId);

        // Either velocity or position should show the push happened
        const wasPushed = dynVel.x > 0.01 || dynPos.x > 0.81;
        expect(wasPushed).toBe(true);
    });
});

// ─── İDDİA 3: isSensor flag not used in collision detection ─────────────
describe('Audit Claim 3: Sensor flag honored in collision response', () => {
    let world: PhysicsWorld2D;

    beforeEach(() => {
        world = new PhysicsWorld2D({
            gravity: { x: 0, y: -10 },
            bodyCapacity: 64,
            shapeCapacity: 64,
            contactCapacity: 64,
            constraintCapacity: 64,
        });
    });

    it('sensor shape should generate contact events but NO physical response', () => {
        // Dynamic body falling
        const dynId = world.createBody({
            type: BodyType.Dynamic,
            position: { x: 0, y: 2 },
        });
        world.createCircleShape(dynId, { radius: 0.5, density: 1 });

        // Static sensor below
        const sensorBodyId = world.createBody({
            type: BodyType.Static,
            position: { x: 0, y: 0 },
        });
        world.createCircleShape(sensorBodyId, { radius: 1, density: 1, isSensor: true });

        // Track sensor events (sensor contacts now fire onSensorEnter, not onCollisionBegin)
        let sensorEntered = false;
        world.setContactListener({
            onSensorEnter: (event) => {
                if (event.visitorBodyId === dynId || event.sensorBodyId === dynId) {
                    sensorEntered = true;
                }
            },
            onSensorExit: () => {},
            onSensorStay: () => {},
        });

        // Record initial velocity (only gravity)
        const bm = world.getBodyManager();
        const velBefore = bm.getLinearVelocity(dynId);

        // Step to let body fall and overlap with sensor
        for (let i = 0; i < 60; i++) {
            world.step(1 / 60);
        }

        // Sensor contact should have been detected
        expect(sensorEntered).toBe(true);

        // But the sensor should NOT provide physical support
        // Dynamic body should have fallen THROUGH (or at least not been stopped by sensor)
        // With sensor: body continues falling under gravity
        // Without sensor (solid): body would be stopped/resting on top
        const velAfter = bm.getLinearVelocity(dynId);
        const posAfter = bm.getPosition(dynId);

        // Body should still be moving downward or have passed through
        // If sensor works correctly, body should NOT be resting at y ≈ 1.5 (on top of sensor)
        // It should have fallen through or be at a lower position
        // Key assertion: velocity should NOT be near zero from being supported
        // (it may have some velocity from gravity, or be below the sensor)
        const isSupported = Math.abs(velAfter.y) < 0.1 && posAfter.y > 0.5;
        expect(isSupported).toBe(false);
    });

    it('non-sensor shape should provide physical collision response', () => {
        // Same setup but WITHOUT sensor flag
        const dynId = world.createBody({
            type: BodyType.Dynamic,
            position: { x: 0, y: 2 },
        });
        world.createCircleShape(dynId, { radius: 0.5, density: 1 });

        const solidBodyId = world.createBody({
            type: BodyType.Static,
            position: { x: 0, y: 0 },
        });
        // NOT a sensor (default false)
        world.createCircleShape(solidBodyId, { radius: 1, density: 1 });

        const bm = world.getBodyManager();

        // Step to let body fall and collide
        for (let i = 0; i < 120; i++) {
            world.step(1 / 60);
        }

        // Body should be supported (resting on top of solid)
        const pos = bm.getPosition(dynId);
        const vel = bm.getLinearVelocity(dynId);

        // Body should be above or on the solid body, with near-zero velocity
        expect(pos.y).toBeGreaterThan(0.5);
        expect(Math.abs(vel.y)).toBeLessThan(0.5);
    });
});

// ─── İDDİA 4: MAX_TRANSLATION used as velocity clamp ────────────────────
describe('Audit Claim 4: MAX_TRANSLATION semantics', () => {
    let world: PhysicsWorld2D;

    beforeEach(() => {
        world = new PhysicsWorld2D({
            gravity: { x: 0, y: -100 }, // 10x gravity
            bodyCapacity: 64,
            shapeCapacity: 64,
            contactCapacity: 64,
            constraintCapacity: 64,
        });
    });

    it('10x gravity should produce velocity > 2 units/s (not clamped by MAX_TRANSLATION)', () => {
        const bodyId = world.createBody({
            type: BodyType.Dynamic,
            position: { x: 0, y: 100 }, // high up so it doesn't hit anything
        });
        world.createCircleShape(bodyId, { radius: 0.5, density: 1 });

        // Step for 1 second — with 10x gravity (100 m/s²), velocity should reach ~100 m/s
        for (let i = 0; i < 60; i++) {
            world.step(1 / 60);
        }

        const vel = world.getBodyManager().getLinearVelocity(bodyId);
        // If MAX_TRANSLATION=2.0 is used as velocity clamp, |vel.y| would be ≤ 2.0
        // If correctly used as position delta clamp, velocity should be much higher
        // After 1 second at 100 m/s², velocity ≈ 100 m/s (minus some damping)
        expect(Math.abs(vel.y)).toBeGreaterThan(5);
    });
});
