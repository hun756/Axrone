import { describe, it, expect } from 'vitest';
import { Rigidbody3D } from '../../components/rigidbody3d';
import { PhysicsWorld3D } from '@axrone/physics-3d';

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

        it('mass: 0 does not change body type — body remains dynamic', () => {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: -10, z: 0 } });
            const rb = new Rigidbody3D();
            rb.initialize(world, { mass: 0 });
            // Body type should still be dynamic (2), NOT static (0)
            expect(rb.bodyType).toBe(2);
            // Mass should be clamped to 0.0001, not 0
            expect(rb.mass).toBeCloseTo(0.0001, 4);
            // invMass should be finite (no NaN/Infinity)
            const invMass = world.getBodyManager().getInverseMass(rb.bodyId);
            expect(Number.isFinite(invMass)).toBe(true);
            expect(invMass).toBeGreaterThan(0);
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

    describe('force/impulse methods with physics world', () => {
        function createConnectedRigidbody() {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
            const rb = new Rigidbody3D();
            rb.initialize(world, { mass: 1 });
            return { world, rb };
        }

        it('addForce increases linear velocity (C14 fix proof)', () => {
            const { rb } = createConnectedRigidbody();
            rb.addForce({ x: 60, y: 0, z: 0 });
            // Apply accumulated forces (simulates fixedUpdate)
            (rb as any)._applyAccumulatedForces(1 / 60);
            const vel = rb.velocity;
            // force=60, dt=1/60, mass=1 → Δvel = 60*(1/60)/1 = 1
            expect(vel.x).toBeCloseTo(1, 3);
        });

        it('addTorque changes angular velocity via world integration', () => {
            const { world, rb } = createConnectedRigidbody();
            rb.addTorque({ x: 0, y: 60, z: 0 });
            // Apply accumulated torques (simulates fixedUpdate)
            (rb as any)._applyAccumulatedForces(1 / 60);
            // Integrate forces via world step (torques → angular velocity)
            world.step(1 / 60);
            const angVel = rb.angularVelocity;
            expect(Math.abs(angVel.y)).toBeGreaterThan(0);
        });

        it('addRelativeForce transforms direction by rotation', () => {
            const { rb } = createConnectedRigidbody();
            // With identity rotation, relative forward (0,0,1) = world forward (0,0,1)
            rb.addRelativeForce({ x: 0, y: 0, z: 60 });
            (rb as any)._applyAccumulatedForces(1 / 60);
            const vel = rb.velocity;
            // Should be in world Z direction
            expect(vel.z).toBeCloseTo(1, 3);
            expect(Math.abs(vel.x)).toBeLessThan(0.001);
        });

        it('addExplosionForce applies distance-attenuated force', () => {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
            // Near body
            const rbNear = new Rigidbody3D();
            rbNear.initialize(world, { mass: 1 });
            // Far body
            const rbFar = new Rigidbody3D();
            rbFar.initialize(world, { mass: 1 });
            // Position them at different distances from explosion
            world.getBodyManager().setPosition(rbNear.bodyId, { x: 1, y: 0, z: 0 });
            world.getBodyManager().setPosition(rbFar.bodyId, { x: 8, y: 0, z: 0 });

            // Explosion at origin, radius 10
            rbNear.addExplosionForce(100, { x: 0, y: 0, z: 0 }, 10);
            rbFar.addExplosionForce(100, { x: 0, y: 0, z: 0 }, 10);
            (rbNear as any)._applyAccumulatedForces(1 / 60);
            (rbFar as any)._applyAccumulatedForces(1 / 60);

            const velNear = rbNear.velocity;
            const velFar = rbFar.velocity;
            const speedNear = Math.sqrt(velNear.x ** 2 + velNear.y ** 2 + velNear.z ** 2);
            const speedFar = Math.sqrt(velFar.x ** 2 + velFar.y ** 2 + velFar.z ** 2);
            // Near body must be affected more than far body
            expect(speedNear).toBeGreaterThan(speedFar);
        });
    });

    describe('sleep management with physics world', () => {
        it('wakeUp sets body awake and clears sleeping state', () => {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: -10, z: 0 } });
            const rb = new Rigidbody3D();
            rb.initialize(world, { mass: 1 });

            // Put to sleep first
            rb.sleep();
            expect(rb.isSleeping).toBe(true);
            expect(world.getBodyManager().isAwake(rb.bodyId)).toBe(false);

            // Wake up
            rb.wakeUp();
            expect(rb.isSleeping).toBe(false);
            expect(world.getBodyManager().isAwake(rb.bodyId)).toBe(true);
        });

        it('sleep sets body asleep and zeros velocity', () => {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: -10, z: 0 } });
            const rb = new Rigidbody3D();
            rb.initialize(world, { mass: 1 });

            // Give it some velocity
            world.getBodyManager().setLinearVelocity(rb.bodyId, { x: 5, y: 5, z: 5 });

            rb.sleep();
            expect(rb.isSleeping).toBe(true);
            expect(world.getBodyManager().isAwake(rb.bodyId)).toBe(false);

            // Velocity should be zeroed
            const vel = world.getBodyManager().getLinearVelocity(rb.bodyId);
            expect(vel.x).toBe(0);
            expect(vel.y).toBe(0);
            expect(vel.z).toBe(0);
        });
    });
});
