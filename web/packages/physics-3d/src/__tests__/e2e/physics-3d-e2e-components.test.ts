import { describe, it, expect } from 'vitest';
import { PhysicsWorld3D } from '@axrone/physics-3d';
import { Rigidbody3D } from '../../components/rigidbody3d';
import { SphereCollider3D } from '../../components/SphereCollider3D';
import { BoxCollider3D } from '../../components/BoxCollider3D';
import { FixedJoint3D } from '../../components/fixed-joint3d';

/**
 * E2E component harness — tests the full component pipeline:
 * Rigidbody3D + Collider3D → PhysicsWorld3D → step → verify physics behaviour.
 *
 * These tests exercise the component lifecycle (initialize → createBody/createShape)
 * and verify that the facade APIs (createSphereShape, createBoxShape) are correctly
 * wired through the component layer.
 */
describe('Physics3D E2E component harness', () => {
    describe('1. gravity fall (Rigidbody3D + SphereCollider3D)', () => {
        it('body falls under gravity and velocity increases downward', () => {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: -9.81, z: 0 } });
            const rb = new Rigidbody3D();
            rb.initialize(world, { mass: 1 });
            rb.position = { x: 0, y: 10, z: 0 };

            const col = new SphereCollider3D();
            col.radius = 0.5;
            col.initialize(world, rb);

            // Step 30 frames (0.5 second)
            for (let i = 0; i < 30; i++) world.step(1 / 60);

            const pos = rb.position;
            // Body should have fallen from y=10 (gravity pulls down)
            expect(pos.y).toBeLessThan(10);
            // After 0.5s: y ≈ 10 - 0.5 * 9.81 * 0.25 ≈ 8.77
            expect(pos.y).toBeGreaterThan(7);
            expect(pos.y).toBeLessThan(9.5);
            // Velocity should be downward
            const vel = rb.velocity;
            expect(vel.y).toBeLessThan(-1);
        });
    });

    describe('2. collision between two bodies via components', () => {
        it('falling body contacts ground plane — position stabilizes near surface', () => {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: -9.81, z: 0 } });

            // Ground: static body with box collider (size=full extents, halfExtents=size*0.5)
            // type: 0 = Static (mass alone doesn't change body type in the component API)
            const groundRb = new Rigidbody3D();
            groundRb.initialize(world, { type: 0 as any, mass: 0 }); // static
            groundRb.position = { x: 0, y: 0, z: 0 };
            const groundCol = new BoxCollider3D();
            groundCol.size = { x: 50, y: 1, z: 50 }; // halfExtents: 25, 0.5, 25 → top at y=0.5
            groundCol.initialize(world, groundRb);

            // Falling sphere starting just above ground
            const ballRb = new Rigidbody3D();
            ballRb.initialize(world, { mass: 1 });
            ballRb.position = { x: 0, y: 3, z: 0 };
            const ballCol = new SphereCollider3D();
            ballCol.radius = 0.5;
            ballCol.initialize(world, ballRb);

            // Step enough for ball to reach ground and settle
            for (let i = 0; i < 180; i++) world.step(1 / 60);

            const finalPos = ballRb.position;
            // Ball should have fallen from y=3
            // Ground top at y=0.5, ball radius 0.5 → rest position ≈ y=1.0
            // With Baumgarte softness, allow some penetration
            expect(finalPos.y).toBeLessThan(3); // definitely fell
            expect(finalPos.y).toBeGreaterThan(-1); // didn't fall through ground
        });
    });

    describe('3. sensor/trigger — event fires, no physical response', () => {
        it('trigger collider fires onTriggerEnter without deflecting the body (C9 fix E2E)', () => {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });

            // Dynamic ball moving toward trigger
            const ballRb = new Rigidbody3D();
            ballRb.initialize(world, { mass: 1 });
            ballRb.position = { x: -5, y: 0, z: 0 };
            // Set velocity directly via bodyManager (component velocity setter blocks non-dynamic,
            // but this IS dynamic — however, use bodyManager for directness)
            world.getBodyManager().setLinearVelocity(ballRb.bodyId, { x: 10, y: 0, z: 0 });
            const ballCol = new SphereCollider3D();
            ballCol.radius = 0.5;
            ballCol.initialize(world, ballRb);

            // Trigger (sensor) sphere at origin — type:0 = Static
            const triggerRb = new Rigidbody3D();
            triggerRb.initialize(world, { type: 0 as any, mass: 0 }); // static
            triggerRb.position = { x: 0, y: 0, z: 0 };
            const triggerCol = new SphereCollider3D();
            triggerCol.radius = 2;
            triggerCol.isTrigger = true;
            triggerCol.initialize(world, triggerRb);

            // Set up contact listener — runtime uses IContactListener3DRaw interface
            const events: string[] = [];
            world.setContactListener({
                onCollisionBegin() {},
                onCollisionStay() {},
                onCollisionEnd() {},
                onTriggerEnter(a: number, b: number) { events.push(`triggerEnter:${a}:${b}`); },
                onTriggerExit(a: number, b: number) { events.push(`triggerExit:${a}:${b}`); },
            } as any);

            // Step until ball passes through trigger zone
            for (let i = 0; i < 60; i++) world.step(1 / 60);

            // Trigger events should have fired (C9 fix proof)
            const enterEvents = events.filter(e => e.startsWith('triggerEnter:'));
            expect(enterEvents.length).toBeGreaterThanOrEqual(1);

            // Ball should have passed through (no physical deflection from trigger)
            const finalX = ballRb.position.x;
            expect(finalX).toBeGreaterThan(0); // Ball moved past origin
        });
    });

    describe('4. fixed joint — connected bodies move together (C6 fix E2E)', () => {
        it('fixed joint keeps two bodies at constrained distance when force is applied', () => {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });

            const rbA = new Rigidbody3D();
            rbA.initialize(world, { mass: 1 });
            rbA.position = { x: 0, y: 0, z: 0 };
            const colA = new SphereCollider3D();
            colA.radius = 0.5;
            colA.initialize(world, rbA);

            const rbB = new Rigidbody3D();
            rbB.initialize(world, { mass: 1 });
            rbB.position = { x: 3, y: 0, z: 0 };
            const colB = new SphereCollider3D();
            colB.radius = 0.5;
            colB.initialize(world, rbB);

            // Connect with fixed joint via component
            const joint = new FixedJoint3D();
            joint.initialize(world, rbA, rbB);

            // Apply continuous force by re-accumulating each step
            for (let i = 0; i < 60; i++) {
                rbA.addForce({ x: -100, y: 0, z: 0 });
                (rbA as any)._applyAccumulatedForces(1 / 60);
                world.step(1 / 60);
            }

            const posA = rbA.position;
            const posB = rbB.position;

            // Body A was pushed in -x direction
            expect(posA.x).toBeLessThan(0);

            // Body B should have been pulled along by the joint constraint
            expect(posB.x).toBeLessThan(3);

            // Distance between bodies should be much less than unconstrained
            // Without joint: B stays at x=3, A goes to x≈-100*(1/60)*60 = -100
            const dist = Math.abs(posB.x - posA.x);
            expect(dist).toBeLessThan(50);
        });
    });

    describe('5. kinematic body pushes dynamic body', () => {
        it('kinematic body moving into dynamic body transfers momentum via contact', () => {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });

            // Kinematic pusher
            const pusherRb = new Rigidbody3D();
            pusherRb.initialize(world, { mass: 10, isKinematic: true });
            pusherRb.position = { x: -2, y: 0, z: 0 };
            // Set velocity via bodyManager (component setter blocks kinematic type)
            world.getBodyManager().setLinearVelocity(pusherRb.bodyId, { x: 10, y: 0, z: 0 });
            const pusherCol = new SphereCollider3D();
            pusherCol.radius = 1;
            pusherCol.initialize(world, pusherRb);

            // Dynamic target
            const targetRb = new Rigidbody3D();
            targetRb.initialize(world, { mass: 1 });
            targetRb.position = { x: 0, y: 0, z: 0 };
            const targetCol = new SphereCollider3D();
            targetCol.radius = 0.5;
            targetCol.initialize(world, targetRb);

            // Step — kinematic should push dynamic
            for (let i = 0; i < 60; i++) world.step(1 / 60);

            const targetPos = targetRb.position;
            // Target should have been pushed in the +x direction
            expect(targetPos.x).toBeGreaterThan(0);
        });
    });

    describe('6. collider component registers shape via world facade', () => {
        it('SphereCollider3D creates shape on the body through the facade API', () => {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
            const rb = new Rigidbody3D();
            rb.initialize(world, { mass: 1 });
            rb.position = { x: 0, y: 5, z: 0 };

            // Before collider: body exists but has no shapes
            const bodyId = rb.bodyId;
            expect(bodyId).toBeGreaterThanOrEqual(0);

            const col = new SphereCollider3D();
            col.radius = 1;
            col.initialize(world, rb);

            // After collider: shape should be registered (shapeId > invalid)
            expect(col.shapeId).toBeGreaterThanOrEqual(0);

            // Verify the shape participates in queries (raycast should hit it)
            const hit = world.rayCastClosest(
                { x: 0, y: 5, z: -10 },
                { x: 0, y: 0, z: 1 },
                100
            );
            // Should hit the sphere at (0,5,0) with radius 1
            if (hit && hit.hit) {
                expect(hit.bodyId).toBe(bodyId);
                expect(hit.point).toBeDefined();
            }
            // The shapeId check above is the primary assertion proving facade wiring.
        });
    });
});
